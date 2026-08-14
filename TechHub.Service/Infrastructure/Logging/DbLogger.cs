using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TechHub.Service.Infrastructure.Logging;
/// <summary>
/// Builds a <see cref="LogEntry"/> from an exception + optional
/// <see cref="HttpContext"/> and hands it to the <see cref="LogChannel"/>.
///
/// Everything here is synchronous, non-blocking and exception-safe: the whole
/// method body is guarded so it can never propagate a failure into the request
/// pipeline.
/// </summary>
public sealed class DbLogger : IDbLogger
{
    private readonly LogChannel _channel;

    public DbLogger(LogChannel channel)
    {
        _channel = channel;
    }

    public void LogError(Exception ex, string source, HttpContext? context = null)
        => LogCore(ex, "Error", source, context);

    public void LogCritical(Exception ex, string source, HttpContext? context = null)
        => LogCore(ex, "Critical", source, context);

    private void LogCore(Exception ex, string level, string source, HttpContext? context)
    {
        if (ex is null)
            return;

        try
        {
            var entry = new LogEntry
            {
                LogLevel = level,
                Message = ex.Message,
                Exception = ex.ToString(),
                Source = string.IsNullOrWhiteSpace(source) ? null : source,
                Endpoint = ResolveEndpoint(context),
                RequestPath = context?.Request.Path.ToString(),
                RequestMethod = context?.Request.Method,
                UserId = ResolveUserId(context),
                TenantId = ResolveTenantId(context),
                CorrelationId = context?.TraceIdentifier
            };

            _channel.Enqueue(entry);
        }
        catch
        {
            // Logging must never throw.
        }
    }

    /// <summary>JWT "sub" claim — surfaced as ClaimTypes.NameIdentifier by the
    /// JwtBearer handler.</summary>
    private static string? ResolveUserId(HttpContext? context)
    {
        var user = context?.User;
        if (user is null)
            return null;

        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
    }

    /// <summary>Tenant GUID stored in HttpContext.Items by MultiTenantMiddleware.</summary>
    private static string? ResolveTenantId(HttpContext? context)
    {
        if (context is null || !context.Items.TryGetValue("TenantId", out var tenantId))
            return null;

        return tenantId?.ToString();
    }

    /// <summary>
    /// Controller.action of the failing request (e.g. "AssessmentController.StartAttempt").
    /// The value is stashed into <c>HttpContext.Items["Logging_Endpoint"]</c> by the
    /// Web project's ResponseCodeMiddleware (which has access to endpoint metadata) —
    /// the Service layer's Http.Abstractions reference is too old to resolve it directly.
    /// May be null when the request never reached the endpoint (auth/routing failure).
    /// </summary>
    private static string? ResolveEndpoint(HttpContext? context)
    {
        if (context is null || !context.Items.TryGetValue("Logging_Endpoint", out var endpoint))
            return null;

        return endpoint?.ToString();
    }
}