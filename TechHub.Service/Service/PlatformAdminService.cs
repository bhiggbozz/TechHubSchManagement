using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using System;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Platform;
using TechHub.Service.Interface;

namespace TechHub.Service.Service;

public class PlatformAdminService : IPlatformAdminService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly string _connString;

    public PlatformAdminService(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connString = _configuration.GetConnectionString("DbConnectionString") ?? string.Empty;
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

    public async Task<BaseResponse> CreatePlatformAdminAsync(CreatePlatformAdminViewModel model, AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                if (claims.Role != "PlatformSuperAdmin")
                    return Bad("Only PlatformSuperAdmin can create platform admins", ResponseCode.Forbidden);

                if (string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.LastName) ||
                    string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Username) ||
                    string.IsNullOrWhiteSpace(model.Password))
                    return Bad("All fields are required");

                using var conn = new SqlConnection(_connString);
                conn.Open();

                var existingUsername = await conn.QueryFirstOrDefaultAsync<Guid?>($@"
                    SELECT TOP 1 Id FROM PlatformUser
                    WHERE Username = '{model.Username}' AND IsDeleted = 0");

                if (existingUsername is not null)
                    return Bad("Username already exists", ResponseCode.Conflict);

                var existingEmail = await conn.QueryFirstOrDefaultAsync<Guid?>($@"
                    SELECT TOP 1 Id FROM PlatformUser
                    WHERE Email = '{model.Email}' AND IsDeleted = 0");

                if (existingEmail is not null)
                    return Bad("Email already exists", ResponseCode.Conflict);

                var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                var passwordHash = HashPassword(model.Password);
                var userId = Guid.NewGuid();

                await conn.ExecuteAsync($@"
                    INSERT INTO PlatformUser (Id, FirstName, LastName, Email, Username, PasswordHash, Role, IsActive, IsDeleted, CreatedBy, CreatedAt, ModifiedAt)
                    VALUES ('{userId}', '{model.FirstName}', '{model.LastName}', '{model.Email}',
                            '{model.Username}', '{passwordHash}', 'PlatformAdmin', 1, 0,
                            '{claims.UserId}', '{now}', '{now}')");

                _logger.Information(
                    "PlatformAdmin created - Id: {Id}, Username: {Username}, By: {CreatedBy}",
                    userId, model.Username, claims.UserId);

                return Ok("Platform admin created successfully", new
                {
                    Id = userId,
                    Username = model.Username,
                    Email = model.Email,
                    Role = "PlatformAdmin"
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating platform admin");
                return Bad("An error occurred while creating platform admin", ResponseCode.ErrorOccured);
            }
        }
    }
}
