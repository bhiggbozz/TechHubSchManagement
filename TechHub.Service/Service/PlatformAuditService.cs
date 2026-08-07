using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text.Json;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Service.Interface;

namespace TechHub.Service.Service;

/// <summary>
/// Platform audit trail service. Writes "who did what" entries for school
/// operations and platform user management. Uses Dapper directly so it can be
/// lifted out into the future platform microservice.
/// </summary>
public class PlatformAuditService : IPlatformAuditService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly string _connString;

    public PlatformAuditService(IConfiguration configuration, ILogger logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connString = _configuration.GetConnectionString("DbConnectionString") ?? string.Empty;
    }

    public async Task<BaseResponse> LogAsync(
        AuthenticatedUserClaims claims,
        string action,
        string entityType,
        Guid? entityId,
        string description,
        object? details = null)
    {
        try
        {
            using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();

            await conn.ExecuteAsync(@"
                INSERT INTO PlatformAuditLog (Id, ActorId, ActorName, ActorRole, [Action], EntityType, EntityId, Description, DetailsJson, CreatedAt)
                VALUES (@Id, @ActorId, @ActorName, @ActorRole, @Action, @EntityType, @EntityId, @Description, @DetailsJson, @CreatedAt)",
                new
                {
                    Id = System.Guid.NewGuid(),
                    ActorId = System.Guid.TryParse(claims?.UserId, out var actorId) ? actorId : System.Guid.Empty,
                    ActorName = claims?.Email ?? claims?.UserId,
                    ActorRole = claims?.Role,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Description = description,
                    DetailsJson = details is null ? (object?)null : JsonSerializer.Serialize(details),
                    CreatedAt = System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });

            _logger.Information(
                "Platform audit logged - Actor: {ActorId}, Action: {Action}, Entity: {EntityType}:{EntityId}",
                claims?.UserId, action, entityType, entityId);

            return new BaseResponse
            {
                ResponseCode = ResponseCode.successful,
                ResponseMessage = "Audit logged",
                Status = "successful"
            };
        }
        catch (System.Exception ex)
        {
            // Audit failures must never break the underlying operation.
            _logger.Error(ex, "Failed to write platform audit entry");
            return new BaseResponse
            {
                ResponseCode = ResponseCode.ErrorOccured,
                ResponseMessage = "Failed to log audit entry",
                Status = "failed"
            };
        }
    }

    public async Task<BaseResponse> GetLogsAsync(string? action = null, string? entityType = null, int pageNumber = 1, int pageSize = 50)
    {
        try
        {
            var sql = new System.Text.StringBuilder(@"
                SELECT Id, ActorId, ActorName, ActorRole, [Action], EntityType, EntityId, Description, DetailsJson, CreatedAt
                FROM PlatformAuditLog
                WHERE 1 = 1");
            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(action))
            {
                sql.Append(" AND [Action] = @Action");
                parameters.Add("Action", action);
            }
            if (!string.IsNullOrWhiteSpace(entityType))
            {
                sql.Append(" AND EntityType = @EntityType");
                parameters.Add("EntityType", entityType);
            }

            sql.Append(@"
                ORDER BY CreatedAt DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");

            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 50 : pageSize;
            parameters.Add("Offset", (pageNumber - 1) * pageSize);
            parameters.Add("PageSize", pageSize);

            using var conn = new SqlConnection(_connString);
            var logs = await conn.QueryAsync<object>(sql.ToString(), parameters);

            return new BaseResponse
            {
                ResponseCode = ResponseCode.successful,
                ResponseMessage = "Audit logs retrieved",
                Status = "successful",
                Data = logs
            };
        }
        catch (System.Exception ex)
        {
            _logger.Error(ex, "Failed to retrieve platform audit logs");
            return new BaseResponse
            {
                ResponseCode = ResponseCode.ErrorOccured,
                ResponseMessage = "Failed to retrieve audit logs",
                Status = "failed"
            };
        }
    }
}