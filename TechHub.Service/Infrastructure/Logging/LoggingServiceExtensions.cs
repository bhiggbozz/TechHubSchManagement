using Microsoft.Extensions.DependencyInjection;

namespace TechHub.Service.Infrastructure.Logging;

/// <summary>
/// Registers the database error-logging subsystem:
///   • <see cref="LogChannel"/>              singleton (shared queue)
///   • <see cref="IDbLogger"/>/<see cref="DbLogger"/> singleton
///   • <see cref="LogWriterService"/>        hosted background service
///
/// The <see cref="GlobalExceptionMiddleware"/> is a pipeline concern and is
/// added in Program.cs (app.UseMiddleware) — it must run early, before auth
/// and endpoints.
/// </summary>
public static class LoggingServiceExtensions
{
    public static IServiceCollection AddDatabaseLogging(this IServiceCollection services)
    {
        services.AddSingleton<LogChannel>();
        services.AddSingleton<IDbLogger, DbLogger>();
        services.AddHostedService<LogWriterService>();

        return services;
    }
}