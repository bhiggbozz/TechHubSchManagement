using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Enum;
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

    private bool CanCreateRole(string actorRole, string targetRole) => actorRole switch
    {
        "PlatformSuperAdmin" => targetRole is "PlatformSuperAdmin" or "PlatformAdmin" or "PlatformUser",
        "PlatformAdmin" => targetRole == "PlatformUser",
        _ => false
    };

    public async Task<BaseResponse> CreatePlatformUserAsync(CreatePlatformAdminViewModel model, AuthenticatedUserClaims claims)
    {
        using (LogContext.PushProperty("RequestedBy", claims.UserId))
        {
            try
            {
                var targetRole = string.IsNullOrWhiteSpace(model.Role) ? "PlatformAdmin" : model.Role.Trim();

                if (!CanCreateRole(claims.Role, targetRole))
                    return Bad($"Your role ({claims.Role}) cannot create a '{targetRole}' account", ResponseCode.Forbidden);

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

                await conn.ExecuteAsync(@"
                    INSERT INTO PlatformUser (Id, FirstName, LastName, Email, Username, PasswordHash, Role, IsActive, IsDeleted, CreatedBy, CreatedAt, ModifiedAt)
                    VALUES (@Id, @FirstName, @LastName, @Email, @Username, @PasswordHash, @Role, 1, 0, @CreatedBy, @CreatedAt, @ModifiedAt)",
                    new
                    {
                        Id = userId,
                        model.FirstName,
                        model.LastName,
                        model.Email,
                        model.Username,
                        PasswordHash = passwordHash,
                        Role = targetRole,
                        CreatedBy = claims.UserId,
                        CreatedAt = now,
                        ModifiedAt = now
                    });

                _logger.Information(
                    "Platform user created - Id: {Id}, Username: {Username}, Role: {Role}, By: {CreatedBy}",
                    userId, model.Username, targetRole, claims.UserId);

                return Ok("Platform user created successfully", new
                {
                    Id = userId,
                    Username = model.Username,
                    Email = model.Email,
                    Role = targetRole
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating platform user");
                return Bad("An error occurred while creating platform user", ResponseCode.ErrorOccured);
            }
        }
    }

    public async Task<BaseResponse> GetPlatformUsersAsync(AuthenticatedUserClaims claims)
    {
        try
        {
            using var conn = new SqlConnection(_connString);
            conn.Open();

            var users = await conn.QueryAsync(@"
                SELECT Id, FirstName, LastName, Email, Username, Role, IsActive, CreatedAt
                FROM PlatformUser
                WHERE IsDeleted = 0
                ORDER BY CreatedAt DESC");

            return Ok("Platform users retrieved", users);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving platform users");
            return Bad("An error occurred while retrieving platform users", ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> GetPlatformLoginHistoryAsync(Guid? userId, int pageNumber, int pageSize)
    {
        try
        {
            var sql = new StringBuilder(@"
                SELECT Id, PlatformUserId, Username, Email, Role, PasswordFailed, DeviceType, DeviceIp, CreatedAt
                FROM PlatformLoginHistory
                WHERE 1 = 1");
            var parameters = new DynamicParameters();

            if (userId is not null)
            {
                sql.Append(" AND PlatformUserId = @PlatformUserId");
                parameters.Add("PlatformUserId", userId);
            }

            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 50 : pageSize;

            sql.Append(@"
                ORDER BY CreatedAt DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
            parameters.Add("Page", (pageNumber - 1) * pageSize);
            parameters.Add("PageSize", pageSize);

            using var conn = new SqlConnection(_connString);
            conn.Open();

            var logs = await conn.QueryAsync(sql.ToString(), parameters);

            return Ok("Platform login history retrieved", logs);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error retrieving platform login history");
            return Bad("An error occurred while retrieving platform login history", ResponseCode.ErrorOccured);
        }
    }
}