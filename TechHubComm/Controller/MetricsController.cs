using Microsoft.AspNetCore.Mvc;
using static TechHubComm.Services.Interfaces;

namespace TechHubComm.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController : ControllerBase
    {
        private readonly IMetricsService _metricsService;
        private readonly IConnectionManager _connectionManager;

        public MetricsController(IMetricsService metricsService, IConnectionManager connectionManager)
        {
            _metricsService = metricsService;
            _connectionManager = connectionManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetMetrics()
        {
            var metrics = await _metricsService.GetMetricsAsync();
            var totalConnections = await _connectionManager.GetTotalConnectionCountAsync();

            return Ok(new
            {
                ServerMetrics = metrics,
                TotalConnections = totalConnections,
                Timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("dashboard")]
        public IActionResult GetDashboard()
        {
            return Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>Virtual Board Metrics</title>
    <script src='https://cdn.jsdelivr.net/npm/chart.js'></script>
</head>
<body>
    <h1>Virtual Board Real-time Metrics</h1>
    <div style='width: 800px; height: 400px;'>
        <canvas id='metricsChart'></canvas>
    </div>
    <script>
        const ctx = document.getElementById('metricsChart').getContext('2d');
        const chart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [{
                    label: 'Connections',
                    data: [],
                    borderColor: 'rgb(75, 192, 192)',
                    tension: 0.1
                }, {
                    label: 'Messages/sec',
                    data: [],
                    borderColor: 'rgb(255, 99, 132)',
                    tension: 0.1
                }]
            },
            options: {
                responsive: true,
                scales: {
                    y: {
                        beginAtZero: true
                    }
                }
            }
        });

        setInterval(async () => {
            const response = await fetch('/api/metrics');
            const data = await response.json();
            
            chart.data.labels.push(new Date().toLocaleTimeString());
            chart.data.datasets[0].data.push(data.totalConnections);
            chart.data.datasets[1].data.push(data.serverMetrics.messagesPerSecond);
            
            if (chart.data.labels.length > 20) {
                chart.data.labels.shift();
                chart.data.datasets[0].data.shift();
                chart.data.datasets[1].data.shift();
            }
            
            chart.update();
        }, 5000);
    </script>
</body>
</html>", "text/html");
        }
    }
}
