namespace TechHub.Core.Entities;

public class PerformanceAggregationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SchoolId { get; set; }
    public string RunStartedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public string? RunCompletedAt { get; set; }
    public string Status { get; set; } = "Running";
    public string? ErrorMessage { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public int? ItemsUpserted { get; set; }
    public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}
