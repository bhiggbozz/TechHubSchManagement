//using Microsoft.Extensions.Configuration;
//using Serilog;
//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Linq;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading.Tasks;
//using TechHub.Core;
//using TechHub.Core.Model;
//using TechHub.Service.Interface;
//using TechHub.Service.Service.DatabaseService;

//namespace TechHub.Service.Service;
//public class RefreshTokenService : IRefreshTokenService
//{
//	private readonly IRefreshTokenRepository _refreshTokenRepo;
//	private readonly IQueryRepository<Users> _userRepo;
//	private readonly IJwtService _jwtService;      // your existing JWT generator
//	private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;
//	private readonly ILogger _logger;
//	private readonly IConfiguration _configuration;

//	public RefreshTokenService(
//		IRefreshTokenRepository refreshTokenRepo,
//		IQueryRepository<Users> userRepo,
//		IJwtService jwtService,
//		IDbTransactionScopeFactory dbTransactionScopeFactory,
//		ILogger logger,
//		IConfiguration configuration)
//	{
//		_refreshTokenRepo = refreshTokenRepo;
//		_userRepo = userRepo;
//		_jwtService = jwtService;
//		_dbTransactionScopeFactory = dbTransactionScopeFactory;
//		_logger = logger;
//		_configuration = configuration;
//	}

//	// ── Called at login time to issue the first refresh token ──────────────
//	public async Task<string> GenerateAndStoreRefreshToken(
//		IDbTransaction transaction, IDbConnection connection,
//		Guid userId, Guid schoolId)
//	{
//		var tokenValue = GenerateSecureToken();
//		var expiryDays = int.Parse(
//			_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

//		var refreshToken = new RefreshToken
//		{
//			Id = Guid.NewGuid(),
//			UserId = userId,
//			SchoolId = schoolId,
//			Token = tokenValue,
//			ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
//			CreatedAt = DateTime.UtcNow,
//			IsRevoked = false
//		};

//		await _refreshTokenRepo.Create(transaction, connection, refreshToken);
//		return tokenValue;
//	}

//	// ── The actual refresh endpoint logic ───────────────────────────────────
//	public async Task<BaseResponse> RefreshAsync(string incomingToken)
//	{
//		try
//		{
//			if (string.IsNullOrWhiteSpace(incomingToken))
//			{
//				return new BaseResponse
//				{
//					ResponseCode = ResponseCode.BadRequest,
//					ResponseMessage = "Refresh token is required",
//					Status = "failed"
//				};
//			}

//			// 1. Lookup token
//			var stored = await _refreshTokenRepo.GetByToken(incomingToken);
//			if (stored is null)
//			{
//				_logger.Warning("Refresh token not found - Token: {Token}", incomingToken);
//				return new BaseResponse
//				{
//					ResponseCode = ResponseCode.Unauthorized,
//					ResponseMessage = "Invalid refresh token",
//					Status = "failed"
//				};
//			}

//			// 2. Validate token state
//			if (stored.IsRevoked)
//			{
//				// Possible token reuse attack — revoke entire family for this user
//				_logger.Warning(
//					"Revoked refresh token reuse detected - UserId: {UserId}, TokenId: {TokenId}",
//					stored.UserId, stored.Id);

//				using var revokeScope = _dbTransactionScopeFactory.Create("DbConnectionString");
//				try
//				{
//					await _refreshTokenRepo.RevokeAllForUser(
//						revokeScope.Transaction, revokeScope.Connection, stored.UserId);
//					await revokeScope.CommitAsync();
//				}
//				catch (Exception ex)
//				{
//					_logger.Error(ex, "Failed to revoke token family for UserId: {UserId}", stored.UserId);
//					await revokeScope.RollbackAsync();
//				}

//				return new BaseResponse
//				{
//					ResponseCode = ResponseCode.Unauthorized,
//					ResponseMessage = "Refresh token has already been used or revoked",
//					Status = "failed"
//				};
//			}

//			if (stored.IsExpired)
//			{
//				_logger.Warning(
//					"Expired refresh token used - UserId: {UserId}, ExpiredAt: {ExpiresAt}",
//					stored.UserId, stored.ExpiresAt);
//				return new BaseResponse
//				{
//					ResponseCode = ResponseCode.Unauthorized,
//					ResponseMessage = "Refresh token has expired, please log in again",
//					Status = "failed"
//				};
//			}

//			// 3. Load user
//			var user = await _userRepo.Get(stored.UserId);
//			if (user is null || !user.IsActive)
//			{
//				_logger.Warning(
//					"Refresh attempted for inactive/missing user - UserId: {UserId}", stored.UserId);
//				return new BaseResponse
//				{
//					ResponseCode = ResponseCode.Unauthorized,
//					ResponseMessage = "User account is inactive or does not exist",
//					Status = "failed"
//				};
//			}

//			// 4. Issue new access + refresh token (rotation)
//			var newAccessToken = _jwtService.GenerateAccessToken(user);
//			var newRefreshValue = GenerateSecureToken();
//			var expiryDays = int.Parse(
//				_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

//			var newRefreshToken = new RefreshToken
//			{
//				Id = Guid.NewGuid(),
//				UserId = user.Id,
//				SchoolId = stored.SchoolId,
//				Token = newRefreshValue,
//				ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
//				CreatedAt = DateTime.UtcNow,
//				IsRevoked = false
//			};

//			// 5. Revoke old, store new — atomically
//			using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
//			try
//			{
//				await _refreshTokenRepo.Revoke(
//					scope.Transaction, scope.Connection,
//					stored.Id, replacedByToken: newRefreshValue);

//				await _refreshTokenRepo.Create(
//					scope.Transaction, scope.Connection, newRefreshToken);

//				await scope.CommitAsync();
//			}
//			catch (Exception ex)
//			{
//				_logger.Error(ex, "Failed to rotate refresh token - UserId: {UserId}", user.Id);
//				try { await scope.RollbackAsync(); }
//				catch (Exception rbEx)
//				{
//					_logger.Error(rbEx, "Rollback failed during token rotation - UserId: {UserId}", user.Id);
//				}
//				throw;
//			}

//			_logger.Information(
//				"Token refreshed successfully - UserId: {UserId}, NewTokenId: {TokenId}",
//				user.Id, newRefreshToken.Id);

//			return new BaseResponse
//			{
//				ResponseCode = ResponseCode.successful,
//				ResponseMessage = "Token refreshed successfully",
//				Status = "successful",
//				Data = new TokenResponse
//				{
//					AccessToken = newAccessToken.Token,
//					RefreshToken = newRefreshValue,
//					AccessTokenExpiry = newAccessToken.Expiry,
//					RefreshTokenExpiry = newRefreshToken.ExpiresAt
//				}
//			};
//		}
//		catch (Exception ex)
//		{
//			_logger.Error(ex, "Unexpected error during token refresh");
//			return new BaseResponse
//			{
//				ResponseCode = ResponseCode.ErrorOccured,
//				ResponseMessage = "An unexpected error occurred during token refresh",
//				Status = "failed"
//			};
//		}
//	}

//	// ── Helpers ─────────────────────────────────────────────────────────────
//	private static string GenerateSecureToken()
//	{
//		var bytes = new byte[64];
//		RandomNumberGenerator.Fill(bytes);
//		return Convert.ToBase64String(bytes);
//	}
//}
