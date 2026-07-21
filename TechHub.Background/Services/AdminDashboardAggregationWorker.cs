using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TechHub.Service.Interface;

namespace TechHub.Background.Services;

public class AdminDashboardAggregationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public AdminDashboardAggregationWorker(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("AdminDashboardAggregationWorker started - Interval: {Interval}h", Interval.TotalHours);

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
                _logger.Error(ex, "AdminDashboardAggregationWorker cycle error");
            }
        }

        _logger.Information("AdminDashboardAggregationWorker stopped");
    }

    private async Task RunAggregationCycle()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminDashboardService>();

        _logger.Information("Admin dashboard aggregation cycle starting");
        await service.AggregateAllSchoolsAsync();
        _logger.Information("Admin dashboard aggregation cycle complete");
    }
}
