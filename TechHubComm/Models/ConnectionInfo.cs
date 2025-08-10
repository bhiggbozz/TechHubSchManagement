namespace TechHubComm.Models
{
    public class ConnectionInfo
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public ClassroomContext Context { get; set; } = new();
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public string ServerInstance { get; set; } = Environment.MachineName;
    }
}
