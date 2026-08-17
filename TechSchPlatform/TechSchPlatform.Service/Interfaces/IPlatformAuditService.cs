using TechSchPlatform.Core;
using TechSchPlatform.Core.Model;

namespace TechSchPlatform.Service.Interfaces;

public interface IPlatformAuditService
{
    Task<BaseResponse> LogAsync(
        AuthenticatedUserClaims claims,
        string action,
        string entityType,
        Guid? entityId,
        string description,
        object? details = null);

    Task<BaseResponse> GetLogsAsync(string? action = null, string? entityType = null, int pageNumber = 1, int pageSize = 50);
}