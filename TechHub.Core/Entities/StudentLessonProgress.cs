using System;

namespace TechHub.Core.Entities;

public class StudentLessonProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid LessonId { get; set; }
    public Guid SchoolId { get; set; }
    public string WatchedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}
