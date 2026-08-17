using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace TechHub.Service.Infrastructure.Logging;

/// <summary>
/// Serilog sink that bridges every Error/Fatal log event into the shared
/// <see cref="LogChannel"/> that feeds the <c>ApplicationLogs</c> table.
///
/// Services catch their exceptions, log them with
/// <c>_logger.Error(ex, ...)</c>, and return a generic BaseResponse — the
/// real exception never reaches the global handler. This sink sits on the
/// global <c>Log.Logger</c>, so it captures the ACTUAL exception message +
/// full stack trace for every such swallowed exception across all services,
/// workers and middleware, with request context when a request is in flight.
/// No per-service wiring is required.
///
/// The sink is created before the host (DI) is built; its shared channel and
/// context accessor are bound once via <see cref="Initialize"/> after
/// <c>app.Build()</c>. Everything here is synchronous, non-blocking and
/// exception-safe — logging must never break the pipeline.
/// </summary>
public sealed class ApplicationLogsSink : ILogEventSink
{
    private LogChannel? _channel;
    private IHttpContextAccessor? _httpContextAccessor;

    /// <summary>Bind the shared channel + context accessor once the host (DI) is built.</summary>
    public void Initialize(LogChannel channel, IHttpContextAccessor httpContextAccessor)
    {
        _channel = channel;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Emit(LogEvent logEvent)
    {
        if (_channel is null)
            return;

        // Only server-error level events belong in ApplicationLogs.
        if (logEvent.Level < LogEventLevel.Error)
            return;

        try
        {
            var context = _httpContextAccessor?.HttpContext;
            var exception = logEvent.Exception;

            _channel.Enqueue(new LogEntry
            {
                LogLevel = logEvent.Level == LogEventLevel.Fatal ? "Critical" : "Error",
                Message = exception?.Message ?? logEvent.RenderMessage(),
                Exception = exception?.ToString(),
                Source = ResolveSource(logEvent),
                Endpoint = context is not null
                    && context.Items.TryGetValue("Logging_Endpoint", out var endpoint)
                        ? endpoint?.ToString() : null,
                RequestPath = context?.Request.Path.ToString(),
                RequestMethod = context?.Request.Method,
                UserId = ResolveUserId(context),
                TenantId = context is not null
                    && context.Items.TryGetValue("TenantId", out var tenantId)
                        ? tenantId?.ToString() : null,
                CorrelationId = context?.TraceIdentifier
            });
        }
        catch
        {
            // Logging must never throw.
        }
    }

    /// <summary>The DI type that emitted the event, e.g. "TechHub.Service.Service.SchoolService".</summary>
    private static string? ResolveSource(LogEvent logEvent)
    {
        if (logEvent.Properties.TryGetValue("SourceContext", out var value)
            && value is ScalarValue scalar
            && scalar.Value is string source)
        {
            return source;
        }

        return null;
    }

    /// <summary>JWT "sub" claim — surfaced as ClaimTypes.NameIdentifier by the JwtBearer handler.</summary>
    private static string? ResolveUserId(HttpContext? context)
    {
        var user = context?.User;
        if (user is null)
            return null;

        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
    }
}