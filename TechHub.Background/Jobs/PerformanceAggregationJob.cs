using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TechHub.Service.Interface;

namespace TechHub.Background.Jobs;

/// <summary>
/// Hangfire recurring job that aggregates quiz performance data
/// from SQL Server into MongoDB performance snapshots.
///
/// Schedule: Every 15 minutes
/// Queue: low
/// Retry: 1 attempt
///
/// WHAT IT DOES:
/// 1. Queries all completed quiz attempts from SQL Server
/// 2. Aggregates by classroom+subject, student, teacher, and school
/// 3. Upserts aggregated snapshots into MongoDB
/// 4. Dashboard endpoints read from MongoDB (fast, pre-computed)
/// </summary>
public class PerformanceAggregationJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    public PerformanceAggregationJob(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.Information("PerformanceAggregationJob started");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var aggregationService = scope.ServiceProvider
                .GetRequiredService<IPerformanceAggregationService>();

            await aggregationService.AggregateAllSchoolsAsync();

            _logger.Information("PerformanceAggregationJob completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "PerformanceAggregationJob failed");
            throw;
        }
    }
}
