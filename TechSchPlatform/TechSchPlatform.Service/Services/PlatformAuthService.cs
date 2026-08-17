using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TechSchPlatform.Core;
using TechSchPlatform.Core.Model;
using TechSchPlatform.Core.ViewModel.Platform;
using TechSchPlatform.Service.Interfaces;

namespace TechSchPlatform.Service.Services;

public class PlatformAuthService : IPlatformAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlatformAuthService> _logger;
    private readonly string _connString;

    public PlatformAuthService(IConfiguration configuration, ILogger<PlatformAuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connString = configuration.GetConnectionString("DbConnectionString") ?? string.Empty;
    }

    private async Task LogLoginAsync(Guid userId, string username, string? email, string role, bool passwordFailed)
    {
        try
        {
            using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();

            await conn.ExecuteAsync(@"
                INSERT INTO PlatformLoginHistory (Id, PlatformUserId, Username, Email, Role, PasswordFailed, DeviceType, DeviceIp, CreatedAt)
                VALUES (@Id, @PlatformUserId, @Username, @Email, @Role, @PasswordFailed, @DeviceType, @DeviceIp, @CreatedAt)",
                new
                {
                    Id = Guid.NewGuid(),
                    PlatformUserId = userId,
                    Username = username,
                    Email = email,
                    Role = role,
                    PasswordFailed = passwordFailed,
                    DeviceType = (string?)null,
                    DeviceIp = (string?)null,
                    CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });
        }
        catch (Exception ex)
        {
            // Login history must never break the login flow.
            _logger.LogError(ex, "Failed to write platform login history for user {Username}", username);
        }
    }

    public static string HashPassword(string plainPassword)
    {
        var bytes = Encoding.UTF8.GetBytes(plainPassword);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLower();
    }

    private BaseResponse Ok(string message, object? data = null) => new()
    {
        ResponseCode = ResponseCode.successful,
        ResponseMessage = message,
        Status = "successful",
        Data = data
    };

    private BaseResponse Bad(string message, string code = ResponseCode.BadRequest) => new()
    {
        ResponseCode = code,
        ResponseMessage = message,
        Status = "failed",
        Data = null
    };

    public async Task<BaseResponse> LoginAsync(LoginPlatformViewModel model)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
                return Bad("Username and password are required");

            using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();

            var user = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT Id, FirstName, LastName, Email, Username, PasswordHash, Role, IsActive
                FROM PlatformUser
                WHERE Username = @Username AND IsDeleted = 0",
                new { Username = model.Username });

            if (user is null)
            {
                await LogLoginAsync(Guid.Empty, model.Username, null, string.Empty, true);
                return Bad("Invalid username or password", ResponseCode.Unauthorized);
            }

            if (!(bool)user.IsActive)
            {
                await LogLoginAsync(user.Id, user.Username, user.Email, user.Role, true);
                return Bad("Account is deactivated", ResponseCode.Forbidden);
            }

            var passwordHash = HashPassword(model.Password);
            if ((string)user.PasswordHash != passwordHash)
            {
                await LogLoginAsync(user.Id, user.Username, user.Email, user.Role, true);
                return Bad("Invalid username or password", ResponseCode.Unauthorized);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, (string)user.Email),
                new Claim(ClaimTypes.Role, (string)user.Role),
                new Claim("PlatformUserId", user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            if (!string.IsNullOrEmpty((string)user.FirstName))
                claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
            if (!string.IsNullOrEmpty((string)user.LastName))
                claims.Add(new Claim(ClaimTypes.Surname, user.LastName));

            var secretKey = _configuration["Jwt:SecretKey"];
            if (string.IsNullOrEmpty(secretKey))
                return Bad("JWT SecretKey is not configured", ResponseCode.ErrorOccured);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            await LogLoginAsync(user.Id, user.Username, user.Email, user.Role, false);

            _logger.LogInformation("Platform user {Username} logged in with role {Role}",
                (string)user.Username, (string)user.Role);

            return Ok("Login successful", new
            {
                Token = tokenString,
                TokenExpiresIn = 3600,
                User = new
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Username = user.Username,
                    Role = user.Role
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Platform login error");
            return Bad("An error occurred during login", ResponseCode.ErrorOccured);
        }
    }
}