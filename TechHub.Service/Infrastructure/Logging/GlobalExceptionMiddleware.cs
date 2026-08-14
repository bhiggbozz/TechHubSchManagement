using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace TechHub.Service.Infrastructure.Logging;

/// <summary>
/// Outermost request middleware. Wraps the rest of the pipeline in try/catch:
/// unhandled exceptions are reported through <see cref="IDbLogger"/> (fire and
/// forget — never awaited / blocking), then a deterministic 500 JSON body is
/// returned carrying the correlation id so it can be matched to the logged row.
///
/// The exception is deliberately NOT rethrown and no logging call is awaited.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public GlobalExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IDbLogger logger)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Fire-and-forget: never blocks, never throws.
            logger.LogError(ex, "GlobalExceptionMiddleware", context);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                var payload = JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "An unexpected error occurred",
                    correlationId = context.TraceIdentifier
                });

                await context.Response.WriteAsync(payload, context.RequestAborted);
            }
        }
    }
}