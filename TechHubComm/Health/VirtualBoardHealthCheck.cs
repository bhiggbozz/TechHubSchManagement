using static TechHubComm.Services.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TechHubComm.Services;

namespace TechHubComm.Health
{
    public class VirtualBoardHealthCheck : IHealthCheck
    {
        private readonly IConnectionManager _connectionManager;
        private readonly IMetricsService _metricsService;

        public VirtualBoardHealthCheck(IConnectionManager connectionManager, IMetricsService metricsService)
        {
            _connectionManager = connectionManager;
            _metricsService = metricsService;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var metrics = await _metricsService.GetMetricsAsync();
                var connectionCount = await _connectionManager.GetTotalConnectionCountAsync();

                var data = new Dictionary<string, object>
                {
                    ["TotalConnections"] = connectionCount,
                    ["MessagesPerSecond"] = metrics.MessagesPerSecond,
                    ["CpuUsage"] = metrics.CpuUsage,
                    ["MemoryUsage"] = metrics.MemoryUsage
                };

                // Check if server is under stress
                if (metrics.CpuUsage > 80 || metrics.MemoryUsage > 1000) // 1GB
                {
                    return HealthCheckResult.Degraded("Server under high load", data: data);
                }

                return HealthCheckResult.Healthy("Virtual board service is healthy", data: data);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Virtual board service is unhealthy", ex);
            }
        }
    }
}
