namespace TechHubComm.Models
{
    public class ServerMetrics
    {
        public string ServerInstance { get; set; } = Environment.MachineName;
        public int TotalConnections { get; set; }
        public int ActiveClassrooms { get; set; }
        public long MessagesPerSecond { get; set; }
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

}
