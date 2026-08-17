using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using TechSchPlatform.Core;
using TechSchPlatform.Core.Model;
using TechSchPlatform.Service.Interfaces;

namespace TechSchPlatform.Service.Services;

public class PlatformAuditService : IPlatformAuditService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlatformAuditService> _logger;
    private readonly string _connString;

    public PlatformAuditService(IConfiguration configuration, ILogger<PlatformAuditService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connString = configuration.GetConnectionString("DbConnectionString") ?? string.Empty;
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
                    Id = Guid.NewGuid(),
                    ActorId = Guid.TryParse(claims?.UserId, out var actorId) ? actorId : Guid.Empty,
                    ActorName = claims?.Email ?? claims?.UserId,
                    ActorRole = claims?.Role,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Description = description,
                    DetailsJson = details is null ? (object?)null : JsonSerializer.Serialize(details),
                    CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                });

            _logger.LogInformation(
                "Platform audit logged - Actor: {ActorId}, Action: {Action}, Entity: {EntityType}:{EntityId}",
                claims?.UserId, action, entityType, entityId);

            return new BaseResponse
            {
                ResponseCode = ResponseCode.successful,
                ResponseMessage = "Audit logged",
                Status = "successful"
            };
        }
        catch (Exception ex)
        {
            // Audit failures must never break the underlying operation.
            _logger.LogError(ex, "Failed to write platform audit entry");
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
            var sql = new StringBuilder(@"
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

            pageNumber = pageNumber < 1 ? 1 : pageNumber;
            pageSize = pageSize < 1 ? 50 : pageSize;

            sql.Append(@"
                ORDER BY CreatedAt DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve platform audit logs");
            return new BaseResponse
            {
                ResponseCode = ResponseCode.ErrorOccured,
                ResponseMessage = "Failed to retrieve audit logs",
                Status = "failed"
            };
        }
    }
}