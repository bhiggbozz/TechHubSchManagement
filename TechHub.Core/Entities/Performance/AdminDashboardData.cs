using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Performance;

[BsonIgnoreExtraElements]
public class AdminDashboardData
{
    public ObjectId Id { get; set; }
    public Guid SchoolId { get; set; }
    public DateTime ComputedAt { get; set; }

    // School Overview
    public int TotalStudents { get; set; }
    public int TotalTeachers { get; set; }
    public int TotalClassrooms { get; set; }
    public int TotalSubjects { get; set; }
    public int TotalLessonsCreated { get; set; }
    public int TotalLessonsPublished { get; set; }
    public decimal OverallAverageScore { get; set; }
    public decimal OverallPassRate { get; set; }
    public int TotalLessonWatches { get; set; }
    public int StudentsWhoWatchedLessons { get; set; }

    // Teacher Activity Breakdown
    public List<AdminTeacherActivityItem> TeacherActivities { get; set; } = new();

    // Per-Classroom Performance
    public List<AdminClassroomPerformanceItem> ClassroomPerformances { get; set; } = new();

    // Per-Subject Performance
    public List<AdminSubjectPerformanceItem> SubjectPerformances { get; set; } = new();
}

[BsonIgnoreExtraElements]
public class AdminTeacherActivityItem
{
    public Guid TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public int TotalLessonsCreated { get; set; }
    public int PublishedLessons { get; set; }
    public int DraftLessons { get; set; }
    public int PendingApprovalLessons { get; set; }
    public int RejectedLessons { get; set; }
    public decimal TrustScore { get; set; }
    public string TrustLevel { get; set; } = "N/A";
    public DateTime? LastLessonCreated { get; set; }
}

[BsonIgnoreExtraElements]
public class AdminClassroomPerformanceItem
{
    public Guid ClassroomId { get; set; }
    public string ClassroomName { get; set; } = string.Empty;
    public int StudentCount { get; set; }
    public decimal AverageScorePercent { get; set; }
    public decimal PassRate { get; set; }
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public int LessonWatchCount { get; set; }
}

[BsonIgnoreExtraElements]
public class AdminSubjectPerformanceItem
{
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public decimal AverageScorePercent { get; set; }
    public decimal PassRate { get; set; }
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public int StudentCount { get; set; }
}
