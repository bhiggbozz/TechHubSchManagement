using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TechHub.Background.Configuration
{
	public static class HangfireConfiguration
	{
		/// <summary>
		/// Configure Hangfire with resource-aware settings
		/// </summary>
		public static IServiceCollection AddHangfireServices(this IServiceCollection services, string connectionString, IConfiguration configuration)
		{
			
			var workerCount = CalculateSafeWorkerCount(configuration);

			services.AddHangfire(config =>
			{
				config
					.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
					.UseSimpleAssemblyNameTypeSerializer()
					.UseRecommendedSerializerSettings()
					.UseSqlServerStorage(connectionString, new SqlServerStorageOptions
					{
						CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
						QueuePollInterval = TimeSpan.FromSeconds(15),
						SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
						JobExpirationCheckInterval = TimeSpan.FromHours(1),
						CountersAggregateInterval = TimeSpan.FromMinutes(5),
						PrepareSchemaIfNecessary = true,
						DashboardJobListLimit = 10000,
						TransactionTimeout = TimeSpan.FromMinutes(1),
						UseRecommendedIsolationLevel = true,
						DisableGlobalLocks = true
					});
			});

			
			services.AddHangfireServer(options =>
			{
				options.WorkerCount = workerCount;
				options.Queues = new[] { "critical", "default", "low" };
				options.ShutdownTimeout = TimeSpan.FromMinutes(5);
				options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
				options.ServerName = $"{Environment.MachineName}:api-embedded";
				options.HeartbeatInterval = TimeSpan.FromSeconds(30);
				options.ServerCheckInterval = TimeSpan.FromMinutes(1);
				options.ServerTimeout = TimeSpan.FromMinutes(5);
			});

			return services;
		}

		/// <summary>
		/// Calculate safe worker count based on system resources
		/// </summary>
		private static int CalculateSafeWorkerCount(IConfiguration configuration)
		{
			var cpuCores = Environment.ProcessorCount;
			var maxWorkers = configuration.GetValue<int?>("Hangfire:MaxWorkers");
			var reserveCpuPercent = configuration.GetValue<int>("Hangfire:ReserveCpuForApi", 50);

			var availableCores = cpuCores * (100 - reserveCpuPercent) / 100.0;
			var calculatedWorkers = (int)Math.Floor(availableCores);

			// Apply limits
			calculatedWorkers = Math.Max(1, calculatedWorkers);  // Minimum 1
			calculatedWorkers = Math.Min(8, calculatedWorkers);  // Maximum 8

			// Apply configuration override
			if (maxWorkers.HasValue)
			{
				calculatedWorkers = Math.Min(calculatedWorkers, maxWorkers.Value);
			}

			return calculatedWorkers;
		}
	}
}