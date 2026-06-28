using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Performance;

[BsonIgnoreExtraElements]
public class PerformanceSnapshot
{
    public ObjectId Id { get; set; }

    public string DocType { get; set; } = string.Empty;

    public Guid SchoolId { get; set; }
    public Guid? ClassroomId { get; set; }
    public string? ClassroomName { get; set; }
    public Guid? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public Guid? StudentId { get; set; }
    public string? StudentName { get; set; }

    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public int InProgressAttempts { get; set; }
    public int PartiallyGradedAttempts { get; set; }
    public decimal AverageScorePercent { get; set; }
    public decimal PassRate { get; set; }
    public int StudentCount { get; set; }
    public decimal TotalMarksSum { get; set; }
    public decimal ObtainedMarksSum { get; set; }
    public double? AverageTimeTakenSeconds { get; set; }
    public DateTime LastActivityDate { get; set; }
    public DateTime ComputedAt { get; set; }

    public List<QuizPerformanceItem> QuizBreakdown { get; set; } = new();
}

[BsonIgnoreExtraElements]
public class QuizPerformanceItem
{
    public string QuizCode { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public Guid LessonId { get; set; }
    public int AttemptCount { get; set; }
    public int CompletedCount { get; set; }
    public decimal AverageScore { get; set; }
    public decimal PassRate { get; set; }
    public int StudentCount { get; set; }
}
