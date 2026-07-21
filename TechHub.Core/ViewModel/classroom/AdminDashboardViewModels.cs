namespace TechHub.Core.ViewModel.classroom;

public class AdminDashboardResponseDto
{
    public DateTime ComputedAt { get; set; }

    public AdminOverviewDto Overview { get; set; } = new();
    public List<AdminTeacherActivityDto> TeacherActivities { get; set; } = new();
    public List<AdminClassroomPerformanceDto> ClassroomPerformances { get; set; } = new();
    public List<AdminSubjectPerformanceDto> SubjectPerformances { get; set; } = new();
}

public class AdminOverviewDto
{
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
}

public class AdminTeacherActivityDto
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

public class AdminClassroomPerformanceDto
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

public class AdminSubjectPerformanceDto
{
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public decimal AverageScorePercent { get; set; }
    public decimal PassRate { get; set; }
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public int StudentCount { get; set; }
}
