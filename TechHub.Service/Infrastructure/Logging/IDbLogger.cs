using Microsoft.AspNetCore.Http;

namespace TechHub.Service.Infrastructure.Logging;

/// <summary>
/// Fire-and-forget database error logger used by application code.
/// Implementations must be completely non-blocking and must never throw —
/// a failure to log must never affect the request pipeline.
/// </summary>
public interface IDbLogger
{
    void LogError(Exception ex, string source, HttpContext? context = null);

    void LogCritical(Exception ex, string source, HttpContext? context = null);
}