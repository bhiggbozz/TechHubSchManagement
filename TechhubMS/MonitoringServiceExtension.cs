using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;


 namespace TechhubMs;
public static class MonitoringServiceExtensions
{
	/// <summary>
	/// Registers monitoring and observability services (Health Checks, Metrics)
	/// </summary>
	/// <summary>
	/// Registers basic monitoring services (Health Checks without external dependencies)
	/// </summary>
	public static IServiceCollection AddMonitoringServices(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var connectionString = configuration.GetConnectionString("DefaultConnection");

		// Basic Health Checks (no external packages required)
		services.AddHealthChecks()
			.AddCheck("self", () => HealthCheckResult.Healthy("API is healthy"))
			.AddCheck("database", () =>
			{
				if (string.IsNullOrEmpty(connectionString))
				{
					return HealthCheckResult.Degraded("No database connection string configured");
				}

				try
				{
					using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
					connection.Open();
					using var command = connection.CreateCommand();
					command.CommandText = "SELECT 1";
					command.CommandTimeout = 5;
					command.ExecuteScalar();
					return HealthCheckResult.Healthy("Database is accessible");
				}
				catch (Exception ex)
				{
					return HealthCheckResult.Unhealthy("Database is not accessible", ex);
				}
			}, tags: new[] { "db", "sql" })
			.AddCheck("memory", () =>
			{
				var allocated = GC.GetTotalMemory(forceFullCollection: false);
				var threshold = 1024L * 1024L * 1024L; // 1 GB threshold

				return allocated < threshold
					? HealthCheckResult.Healthy($"Memory usage: {allocated / 1024 / 1024} MB")
					: HealthCheckResult.Degraded($"High memory usage: {allocated / 1024 / 1024} MB");
			}, tags: new[] { "memory" });

		return services;
	}
}
