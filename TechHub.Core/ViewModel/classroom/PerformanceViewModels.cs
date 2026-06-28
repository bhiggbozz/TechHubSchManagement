namespace TechHub.Core.ViewModel.classroom;

public class PerformanceDashboardDto
{
    public Guid? ClassroomId { get; set; }
    public string? ClassroomName { get; set; }
    public Guid? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public int StudentCount { get; set; }
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public decimal AverageScorePercent { get; set; }
    public decimal PassRate { get; set; }
    public double? AverageTimeTakenSeconds { get; set; }
    public DateTime LastActivityDate { get; set; }
    public DateTime ComputedAt { get; set; }
    public List<QuizPerformanceItemDto> QuizBreakdown { get; set; } = new();
}

public class QuizPerformanceItemDto
{
    public string QuizCode { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public Guid LessonId { get; set; }
    public int AttemptCount { get; set; }
    public decimal AverageScore { get; set; }
    public decimal PassRate { get; set; }
}

public class SchoolPerformanceOverviewDto
{
    public int TotalStudents { get; set; }
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public decimal OverallAverageScore { get; set; }
    public decimal OverallPassRate { get; set; }
    public int ClassroomCount { get; set; }
    public int SubjectCount { get; set; }
    public DateTime ComputedAt { get; set; }
    public List<PerformanceDashboardDto> ClassroomBreakdown { get; set; } = new();
    public List<PerformanceDashboardDto> SubjectBreakdown { get; set; } = new();
}

public class TeacherPerformanceDashboardDto
{
    public Guid TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public decimal OverallAverageScore { get; set; }
    public decimal OverallPassRate { get; set; }
    public List<PerformanceDashboardDto> Classrooms { get; set; } = new();
}

public class StudentPerformanceDetailDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public decimal AverageScorePercent { get; set; }
    public decimal PassRate { get; set; }
    public decimal BestScorePercent { get; set; }
    public List<StudentAttemptItemDto> RecentAttempts { get; set; } = new();
}

public class StudentAttemptItemDto
{
    public Guid AttemptId { get; set; }
    public string QuizCode { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public decimal FinalScorePercent { get; set; }
    public bool? IsPassed { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SubmittedAt { get; set; }
}

// ═════════════════════════════════════════════════════════
// NAVBAR DTOS — lightweight quick-summary per role
// ═════════════════════════════════════════════════════════

public class AdminNavbarDto
{
    public int TotalStudents { get; set; }
    public int TotalTeachers { get; set; }
    public int TotalClassrooms { get; set; }
    public int TotalAttempts { get; set; }
    public decimal OverallAverageScore { get; set; }
    public decimal OverallPassRate { get; set; }
    public int PendingGradingItems { get; set; }
}

public class SubjectTeacherNavbarDto
{
    public int ClassCount { get; set; }
    public int SubjectCount { get; set; }
    public int TotalStudents { get; set; }
    public decimal OverallAverageScore { get; set; }
    public decimal OverallPassRate { get; set; }
    public int PendingGradingItems { get; set; }
}

public class ClassTeacherNavbarDto
{
    public int ClassCount { get; set; }
    public int TotalStudents { get; set; }
    public decimal OverallAverageScore { get; set; }
    public decimal OverallPassRate { get; set; }
    public int PendingGradingItems { get; set; }
}

public class StudentNavbarDto
{
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public decimal AverageScorePercent { get; set; }
    public decimal PassRate { get; set; }
    public int PendingQuizzes { get; set; }
}
