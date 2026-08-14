using Dapper;
using Hangfire;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using TechHub.Service.Infrastructure.Logging;

namespace TechHub.BackgroundJobs.Jobs
{
    /// <summary>
    /// Recurring job ("cleanup-application-logs") that deletes error-log rows
    /// older than 90 days. Deletes in batches of 5,000 (DELETE TOP) to avoid
    /// holding long locks on the ApplicationLogs table.
    ///
    /// Uses a fresh, self-contained SqlConnection resolved from the same
    /// "LogsDb" → "DefaultConnection" → "DbConnectionString" chain as the
    /// LogWriterService — it does not touch the scoped repository pattern.
    /// </summary>
    public class ApplicationLogsCleanupJob
    {
        private const int BatchSize = 5000;

        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public ApplicationLogsCleanupJob(IConfiguration configuration, ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [Queue("low")]
        [AutomaticRetry(Attempts = 2)]
        public async Task ExecuteAsync()
        {
            var connectionString = LogsConnectionStringResolver.Resolve(_configuration);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.Debug("ApplicationLogsCleanup: no log connection string configured; skipping");
                return;
            }

            var totalDeleted = 0;
            int deleted;

            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                do
                {
                    deleted = await connection.ExecuteAsync(@"
                        DELETE TOP (5000) FROM ApplicationLogs
                        WHERE CreatedAt < DATEADD(DAY, -90, GETUTCDATE());");

                    totalDeleted += deleted;
                }
                while (deleted >= BatchSize);

                _logger.Information(
                    "ApplicationLogsCleanup completed - removed {Count} rows older than 90 days",
                    totalDeleted);
            }
            catch (Exception ex)
            {
                // Cleanup failure must never affect anything else.
                _logger.Warning(ex, "ApplicationLogsCleanup failed (non-fatal)");
            }
        }
    }
}