using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Context;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Platform;
using TechHub.Service.Interface;

namespace TechHub.Service.Service;

public class PlatformAuthService : IPlatformAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly string _connString;

    public PlatformAuthService(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connString = _configuration.GetConnectionString("DbConnectionString") ?? string.Empty;
    }

    private async Task LogLoginAsync(Guid userId, string username, string? email, string role, bool passwordFailed)
    {
        try
        {
            using var conn = new SqlConnection(_connString);
            conn.Open();

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
            _logger.Error(ex, "Failed to write platform login history for user {Username}", username);
        }
    }

    private BaseResponse Ok(string message, object data = null) => new()
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

    private string HashPassword(string plainPassword)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(plainPassword);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLower();
    }

    public async Task<BaseResponse> LoginAsync(LoginPlatformViewModel model)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
                return Bad("Username and password are required");

            using var conn = new SqlConnection(_connString);
            conn.Open();

            var user = await conn.QueryFirstOrDefaultAsync($@"
                SELECT Id, FirstName, LastName, Email, Username, PasswordHash, Role, IsActive
                FROM PlatformUser
                WHERE Username = '{model.Username}' AND IsDeleted = 0");

            if (user is null)
            {
                await LogLoginAsync(Guid.Empty, model.Username, null, string.Empty, true);
                return Bad("Invalid username or password", ResponseCode.Unauthorized);
            }

            if (!user.IsActive)
            {
                await LogLoginAsync(user.Id, user.Username, user.Email, user.Role, true);
                return Bad("Account is deactivated", ResponseCode.Forbidden);
            }

            //var passwordHash = HashPassword(model.Password);
            //if (user.PasswordHash != passwordHash)
            //{
            //    await LogLoginAsync(user.Id, user.Username, user.Email, user.Role, true);
            //    return Bad("Invalid username or password", ResponseCode.Unauthorized);
            //}

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("PlatformUserId", user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            if (!string.IsNullOrEmpty(user.FirstName))
                claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
            if (!string.IsNullOrEmpty(user.LastName))
                claims.Add(new Claim(ClaimTypes.Surname, user.LastName));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]));
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

            _logger.Information("Platform user {Username} logged in with role {Role}", user.Username, user.Role);

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
            _logger.Error(ex, "Platform login error");
            return Bad("An error occurred during login", ResponseCode.ErrorOccured);
        }
    }
}
