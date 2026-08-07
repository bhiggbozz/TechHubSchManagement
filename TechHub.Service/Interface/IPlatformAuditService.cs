using TechHub.Core;
using TechHub.Core.Model;

namespace TechHub.Service.Interface;

public interface IPlatformAuditService
{
    /// <summary>
    /// Write an audit entry for a platform action. Action/EntityType follow the
    /// <see cref="TechHub.Core.Model.PlatformAuditAction"/> conventions.
    /// </summary>
    Task<BaseResponse> LogAsync(AuthenticatedUserClaims claims, string action, string entityType, Guid? entityId, string description, object? details = null);

    /// <summary>
    /// Retrieve audit entries, optionally filtered by action/entity, newest first.
    /// </summary>
    Task<BaseResponse> GetLogsAsync(string? action = null, string? entityType = null, int pageNumber = 1, int pageSize = 50);
}