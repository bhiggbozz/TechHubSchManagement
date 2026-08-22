using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using TechSchPlatform.Core;
using TechSchPlatform.Core.Model;
using TechSchPlatform.Core.ViewModel.Platform;
using TechSchPlatform.Service.Interfaces;

namespace TechSchPlatform.Service.Services;

public class PlatformAdminService : IPlatformAdminService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlatformAdminService> _logger;
    private readonly string _connString;
    private readonly IEmailService _emailService;

    public PlatformAdminService(IConfiguration configuration, ILogger<PlatformAdminService> logger, IEmailService emailService)
    {
        _configuration = configuration;
        _logger = logger;
        _connString = configuration.GetConnectionString("DbConnectionString") ?? string.Empty;
        _emailService = emailService;
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

    private bool CanCreateRole(string? actorRole, string targetRole) => actorRole switch
    {
        "PlatformSuperAdmin" => targetRole is "PlatformSuperAdmin" or "PlatformAdmin" or "PlatformUser",
        "PlatformAdmin" => targetRole == "PlatformUser",
        _ => false
    };

    public async Task<BaseResponse> CreatePlatformUserAsync(CreatePlatformAdminViewModel model, AuthenticatedUserClaims claims)
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
            await conn.OpenAsync();

            var existingUsername = await conn.QueryFirstOrDefaultAsync<Guid?>(@"
                SELECT TOP 1 Id FROM PlatformUser
                WHERE Username = @Username AND IsDeleted = 0",
                new { model.Username });

            if (existingUsername is not null)
                return Bad("Username already exists", ResponseCode.Conflict);

            var existingEmail = await conn.QueryFirstOrDefaultAsync<Guid?>(@"
                SELECT TOP 1 Id FROM PlatformUser
                WHERE Email = @Email AND IsDeleted = 0",
                new { model.Email });

            if (existingEmail is not null)
                return Bad("Email already exists", ResponseCode.Conflict);

            var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var passwordHash = PlatformAuthService.HashPassword(model.Password);
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

            _logger.LogInformation(
                "Platform user created - Id: {Id}, Username: {Username}, Role: {Role}, By: {CreatedBy}",
                userId, model.Username, targetRole, claims.UserId);

            // Send credentials email (fire-and-forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    var subject = "Your TechHub Platform Account";
                    var body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <h2>Platform Account Created</h2>
                            <p>Dear {model.FirstName},</p>
                            <p>A {targetRole} account has been created for you on the TechHub platform.</p>
                            <h3>Login Credentials</h3>
                            <ul>
                                <li><strong>Username:</strong> {model.Username}</li>
                                <li><strong>Password:</strong> {model.Password}</li>
                            </ul>
                            <p>Please log in and change your password on first login.</p>
                            <p>Best regards,<br/>TechHub Platform Team</p>
                        </body>
                        </html>";

                    await _emailService.SendAsync(model.Email, $"{model.FirstName} {model.LastName}", subject, body);
                    _logger.LogInformation("Credentials email sent to {Email} for platform user {Username}", model.Email, model.Username);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send credentials email for platform user {Username}", model.Username);
                }
            });

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
            _logger.LogError(ex, "Error creating platform user");
            return Bad("An error occurred while creating platform user", ResponseCode.ErrorOccured);
        }
    }

    public async Task<BaseResponse> GetPlatformUsersAsync(AuthenticatedUserClaims claims)
    {
        try
        {
            using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();

            var users = await conn.QueryAsync(@"
                SELECT Id, FirstName, LastName, Email, Username, Role, IsActive, CreatedAt
                FROM PlatformUser
                WHERE IsDeleted = 0
                ORDER BY CreatedAt DESC");

            return Ok("Platform users retrieved", users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving platform users");
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
            parameters.Add("Offset", (pageNumber - 1) * pageSize);
            parameters.Add("PageSize", pageSize);

            using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();

            var logs = await conn.QueryAsync(sql.ToString(), parameters);

            return Ok("Platform login history retrieved", logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving platform login history");
            return Bad("An error occurred while retrieving platform login history", ResponseCode.ErrorOccured);
        }
    }
}