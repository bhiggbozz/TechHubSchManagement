using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.classroom;

// ── Admin Analytics — Lesson Summary ───────────────────────────────────────
public class LessonQuizAnalyticsSummaryDto
{
	public Guid LessonId { get; set; }
	public string QuizCode { get; set; } = string.Empty;
	public int TotalStudents { get; set; }
	public int AttemptedCount { get; set; }
	public int CompletedCount { get; set; }
	public decimal ParticipationRate { get; set; }
	public decimal? AverageScore { get; set; }
	public decimal? HighestScore { get; set; }
	public decimal? LowestScore { get; set; }
	public decimal? PassRate { get; set; }
	public int? AverageTimeSeconds { get; set; }
	public int AutoGradedCount { get; set; }
	public int PartiallyGradedCount { get; set; }
	public int FullyGradedCount { get; set; }
	public int PendingGradingCount { get; set; }
	public string ComputedAt { get; set; } = string.Empty;
}

// ── Per-Question Stats ────────────────────────────────────────────────────
public class QuestionAnalyticsDetailDto
{
	public Guid QuestionId { get; set; }
	public string QuestionTitle { get; set; } = string.Empty;
	public int QuestionType { get; set; }
	public int DifficultyLevel { get; set; }
	public decimal MaxMarks { get; set; }
	public int TotalResponses { get; set; }
	public int CorrectCount { get; set; }
	public int WrongCount { get; set; }
	public int SkippedCount { get; set; }
	public decimal AverageMarksObtained { get; set; }
	public decimal SuccessRate { get; set; }
	public long? AvgTimeTakenMs { get; set; }
}

// ── Per-Student Stats ─────────────────────────────────────────────────────
public class StudentQuizPerformanceDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public int AttemptCount { get; set; }
	public decimal? BestScorePercent { get; set; }
	public Guid? BestAttemptId { get; set; }
	public bool? IsPassed { get; set; }
	public string LatestStatus { get; set; } = string.Empty;
	public string? LatestSubmittedAt { get; set; }
	public int? TotalTimeSeconds { get; set; }
	public int? CorrectAnswers { get; set; }
	public int? WrongAnswers { get; set; }
	public int? SkippedAnswers { get; set; }
}

// ── Per-Subject Performance (per-lesson quiz stats for a subject) ──────────
public class SubjectQuizPerformanceDto
{
    public Guid LessonId { get; set; }
    public string QuizCode { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public int InProgressAttempts { get; set; }
    public decimal AverageScorePercent { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public decimal PassRate { get; set; }
}

// ── Per-Student Performance (aggregate + per-lesson breakdown) ─────────────
public class StudentQuizPerformanceDetailDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int TotalQuizzes { get; set; }
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public int InProgressAttempts { get; set; }
    public decimal AverageScorePercent { get; set; }
    public decimal PassRate { get; set; }
    public decimal BestScorePercent { get; set; }
    public List<StudentQuizPerformanceItemDto> Quizzes { get; set; } = new();
}

public class StudentQuizPerformanceItemDto
{
    public Guid AttemptId { get; set; }
    public Guid LessonId { get; set; }
    public string QuizCode { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public decimal? BestScorePercent { get; set; }
    public Guid? BestAttemptId { get; set; }
    public bool? IsPassed { get; set; }
    public string LatestStatus { get; set; } = string.Empty;
    public string? LatestSubmittedAt { get; set; }
}

// ── Per-Classroom Performance (per-lesson quiz stats) ─────────────────────
public class ClassroomQuizPerformanceDto
{
    public Guid LessonId { get; set; }
    public string QuizCode { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int TotalAttempts { get; set; }
    public int CompletedAttempts { get; set; }
    public int InProgressAttempts { get; set; }
    public decimal AverageScorePercent { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public decimal PassRate { get; set; }
}

// ── Participation (who hasn't taken the quiz) ─────────────────────────────
public class LessonParticipationDto
{
	public int TotalStudents { get; set; }
	public int AttemptedCount { get; set; }
	public int NotAttemptedCount { get; set; }
	public decimal ParticipationRate { get; set; }
	public List<StudentParticipationDto> Attempted { get; set; } = new();
	public List<StudentParticipationDto> NotAttempted { get; set; } = new();
}

public class StudentParticipationDto
{
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public Guid? AttemptId { get; set; }
	public decimal? ScorePercent { get; set; }
	public string? Status { get; set; }
	public string? SubmittedAt { get; set; }
}
