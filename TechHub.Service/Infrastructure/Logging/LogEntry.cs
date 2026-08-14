namespace TechHub.Service.Infrastructure.Logging;

/// <summary>
/// A single application error record queued for durable write to the
/// <c>ApplicationLogs</c> table. Mirrors the table columns minus Id/CreatedAt
/// (the database assigns those). Being a plain POCO it maps 1:1 onto Dapper.
/// </summary>
public sealed class LogEntry
{
    /// <summary>Error or Critical.</summary>
    public string LogLevel { get; init; } = "Error";

    public string Message { get; init; } = string.Empty;

    /// <summary>Full stack trace via <see cref="System.Exception.ToString()"/>.</summary>
    public string? Exception { get; init; }

    /// <summary>Class / middleware that logged the error.</summary>
    public string? Source { get; init; }

    /// <summary>Matched controller.action of the failing request, e.g. "AssessmentController.StartAttempt".</summary>
    public string? Endpoint { get; init; }

    public string? RequestPath { get; init; }

    public string? RequestMethod { get; init; }

    /// <summary>From the JWT "sub" (NameIdentifier) claim if present.</summary>
    public string? UserId { get; init; }

    /// <summary>From <c>HttpContext.Items["TenantId"]</c> if present.</summary>
    public string? TenantId { get; init; }

    /// <summary><c>HttpContext.TraceIdentifier</c>.</summary>
    public string? CorrelationId { get; init; }
}