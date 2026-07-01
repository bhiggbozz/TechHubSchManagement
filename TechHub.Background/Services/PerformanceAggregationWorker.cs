using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TechHub.Service.Interface;

namespace TechHub.Background.Services;

/// <summary>
/// Background service that periodically aggregates quiz performance data
/// from SQL Server into MongoDB performance snapshots.
///
/// BEHAVIOUR:
/// - Runs every 15 minutes
/// - On each cycle, aggregates ALL schools' completed quiz attempts
/// - Dashboard endpoints read from MongoDB (fast, pre-computed)
///
/// WHY A BACKGROUND SERVICE INSTEAD OF ON-DEMAND:
/// - The aggregation query joins QuizAttempt + LessonContent + Classroom
///   + Subjects + Users across potentially thousands of rows
/// - Pre-computing on a schedule avoids dashboard latency spikes
/// - MongoDB is optimised for the read patterns the dashboard needs
///
/// SCALING NOTE:
/// - If the dataset grows large, switch to incremental processing:
///   Track LastAggregatedAt per school and only process new attempts
/// </summary>
public class PerformanceAggregationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    // Safety net: full re-aggregation once daily.
    // Real-time updates happen incrementally on quiz submit / manual grade.
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public PerformanceAggregationWorker(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information(
            "PerformanceAggregationWorker started - Interval: {Interval}m",
            Interval.TotalMinutes);

        // ── Run once immediately on startup ──────────────────────────────
        await RunAggregationCycle();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                await RunAggregationCycle();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "PerformanceAggregationWorker cycle error");
            }
        }

        _logger.Information("PerformanceAggregationWorker stopped");
    }

    private async Task RunAggregationCycle()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var aggregationService = scope.ServiceProvider
            .GetRequiredService<IPerformanceAggregationService>();

        _logger.Information("Performance aggregation cycle starting");
        await aggregationService.AggregateAllSchoolsAsync();
        _logger.Information("Performance aggregation cycle complete");
    }
}
