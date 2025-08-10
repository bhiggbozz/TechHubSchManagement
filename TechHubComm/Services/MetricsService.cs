using static TechHubComm.Services.Interfaces;
using TechHubComm.Models;
using StackExchange.Redis;
using System.Diagnostics;

namespace TechHubComm.Services
{
    public class MetricsService : IMetricsService
    {
        private readonly IDatabase _database;
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger<MetricsService> _logger;
        private readonly string _serverInstance;
        private long _messageCount = 0;
        private readonly Timer _metricsTimer;

        public MetricsService(
            IConnectionMultiplexer redis,
            IConnectionManager connectionManager,
            ILogger<MetricsService> logger)
        {
            _database = redis.GetDatabase();
            _connectionManager = connectionManager;
            _logger = logger;
            _serverInstance = Environment.MachineName;

            // Report metrics every 30 seconds
            _metricsTimer = new Timer(ReportMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        public async Task IncrementMessageCountAsync()
        {
            Interlocked.Increment(ref _messageCount);
            await _database.StringIncrementAsync($"metrics:messages:{_serverInstance}");
        }

        public async Task RecordConnectionCountAsync(int count)
        {
            await _database.StringSetAsync($"metrics:connections:{_serverInstance}", count);
        }

        public async Task RecordLatencyAsync(double latency)
        {
            await _database.ListLeftPushAsync($"metrics:latency:{_serverInstance}", latency);
            await _database.ListTrimAsync($"metrics:latency:{_serverInstance}", 0, 999); // Keep last 1000 samples
        }

        public async Task<ServerMetrics> GetMetricsAsync()
        {
            var totalConnections = await _connectionManager.GetTotalConnectionCountAsync();
            var process = Process.GetCurrentProcess();

            return new ServerMetrics
            {
                ServerInstance = _serverInstance,
                TotalConnections = totalConnections,
                MessagesPerSecond = Interlocked.Read(ref _messageCount),
                CpuUsage = GetCpuUsage(),
                MemoryUsage = process.WorkingSet64 / 1024.0 / 1024.0, // MB
                Timestamp = DateTime.UtcNow
            };
        }

        private async void ReportMetrics(object? state)
        {
            try
            {
                var metrics = await GetMetricsAsync();
                await _database.StringSetAsync($"server:metrics:{_serverInstance}",
                    System.Text.Json.JsonSerializer.Serialize(metrics), TimeSpan.FromMinutes(5));

                // Reset message count for next interval
                Interlocked.Exchange(ref _messageCount, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting metrics");
            }
        }

        private double GetCpuUsage()
        {
            // Simplified CPU usage calculation - in production use proper performance counters
            var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount;
        }
    }
}
