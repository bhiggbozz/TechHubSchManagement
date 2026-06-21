using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.classroom;

// ── Existing quiz creation view models ─────────────────────────────────────
public class CreateQuizViewModel
{
	public List<Guid> QuestionIds { get; set; } = new();
}

public class AttachQuizViewModel
{
	public string QuizCode { get; set; } = string.Empty;
}

public class QuizQuestionDto
{
	public Guid QuestionId { get; set; }
	public string Title { get; set; }
	public string TextContent { get; set; }
	public int QuestionType { get; set; }
	public int DifficultyLevel { get; set; }
	public int MarksAllocation { get; set; }
	public string SubjectName { get; set; }
	public string TopicName { get; set; }
	public int DisplayOrder { get; set; }
}

// ── Quiz Config ────────────────────────────────────────────────────────────
public class QuizConfigViewModel
{
	public bool AllowRetakes { get; set; } = false;
	public int MaxAttempts { get; set; } = 1;
	public int PassMarkPercent { get; set; } = 50;
	public int? TimeLimitMinutes { get; set; }
	public bool AutoSubmitOnTimeout { get; set; } = true;
	public bool ShuffleQuestions { get; set; } = false;
	public bool ShowResultImmediately { get; set; } = true;
	public bool ShowCorrectAnswers { get; set; } = false;
	public bool AllowBoardAnswer { get; set; } = true;
	public bool AllowAIAssistance { get; set; } = false;
	public int MaxAIAssistancePerQuestion { get; set; } = 1000;
	public int EasyMarks { get; set; } = 1;
	public int MediumMarks { get; set; } = 2;
	public int HardMarks { get; set; } = 3;
	public int ExamLevelMarks { get; set; } = 5;
}

public class QuizConfigDto
{
	public Guid Id { get; set; }
	public Guid TeacherId { get; set; }
	public bool AllowRetakes { get; set; }
	public int MaxAttempts { get; set; }
	public int PassMarkPercent { get; set; }
	public int? TimeLimitMinutes { get; set; }
	public bool AutoSubmitOnTimeout { get; set; }
	public bool ShuffleQuestions { get; set; }
	public bool ShowResultImmediately { get; set; }
	public bool ShowCorrectAnswers { get; set; }
	public bool AllowBoardAnswer { get; set; }
	public bool AllowAIAssistance { get; set; }
	public int MaxAIAssistancePerQuestion { get; set; }
	public int EasyMarks { get; set; }
	public int MediumMarks { get; set; }
	public int HardMarks { get; set; }
	public int ExamLevelMarks { get; set; }
}

// ── Quiz Attempt (student) ─────────────────────────────────────────────────
public class StartQuizViewModel
{
	public Guid LessonId { get; set; }
}

public class SubmitQuizViewModel
{
	public List<QuizAnswerSubmissionViewModel> Answers { get; set; } = new();
	public int? TimeTakenSeconds { get; set; }
}

public class QuizAnswerSubmissionViewModel
{
	public Guid QuestionId { get; set; }
	public Guid? SelectedOptionId { get; set; }
	public string? TypedAnswer { get; set; }
	public string? BoardSessionId { get; set; }
	public string? AudioUrl { get; set; }
	public long? TimeTakenMs { get; set; }
	public bool IsSkipped { get; set; } = false;
}

public class QuizOptionDto
{
	public Guid OptionId { get; set; }
	public string OptionLabel { get; set; }
	public string OptionText { get; set; }
}

public class QuizQuestionDetailDto
{
	public Guid QuestionId { get; set; }
	public string Title { get; set; }
	public string TextContent { get; set; }
	public int QuestionType { get; set; }
	public int DifficultyLevel { get; set; }
	public int MarksAllocation { get; set; }
	public string SubjectName { get; set; }
	public string TopicName { get; set; }
	public int DisplayOrder { get; set; }
	public List<QuizOptionDto> Options { get; set; } = new();
}

public class StartQuizResponseDto
{
	public Guid AttemptId { get; set; }
	public string QuizCode { get; set; }
	public int AttemptNumber { get; set; }
	public int? TimeLimitMinutes { get; set; }
	public int TotalQuestions { get; set; }
	public List<QuizQuestionDetailDto> Questions { get; set; } = new();
}

// ── Student Quiz Display (before / during attempt) ─────────────────────────
public class StudentQuizDisplayDto
{
	public Guid LessonId { get; set; }
	public string QuizCode { get; set; } = string.Empty;
	public QuizSettingsDisplayDto Config { get; set; } = new();
	public AttemptStatusDisplayDto AttemptStatus { get; set; } = new();
	public List<StudentQuizQuestionDto> Questions { get; set; } = new();
	public int TotalQuestions { get; set; }
	public decimal TotalMarks { get; set; }
}

public class QuizSettingsDisplayDto
{
	public bool AllowRetakes { get; set; }
	public int MaxAttempts { get; set; }
	public int PassMarkPercent { get; set; }
	public int? TimeLimitMinutes { get; set; }
	public bool AutoSubmitOnTimeout { get; set; }
	public bool ShuffleQuestions { get; set; }
	public string ShowResultMode { get; set; } = "Immediate";
	public bool ShowCorrectAnswers { get; set; }
	public bool AllowBoardAnswer { get; set; }
	public int EasyMarks { get; set; }
	public int MediumMarks { get; set; }
	public int HardMarks { get; set; }
	public int ExamLevelMarks { get; set; }
}

public class AttemptStatusDisplayDto
{
	public bool HasInProgressAttempt { get; set; }
	public Guid? InProgressAttemptId { get; set; }
	public int CompletedAttempts { get; set; }
	public bool MaxAttemptsReached { get; set; }
	public bool CanStart { get; set; }
}

public class StudentQuizQuestionDto
{
	public Guid QuestionId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string TextContent { get; set; } = string.Empty;
	public int QuestionType { get; set; }
	public string QuestionTypeName { get; set; } = string.Empty;
	public int DifficultyLevel { get; set; }
	public string DifficultyName { get; set; } = string.Empty;
	public int MarksAllocation { get; set; }
	public decimal ResolvedMaxMarks { get; set; }
	public int DisplayOrder { get; set; }
	public string SubjectName { get; set; } = string.Empty;
	public string TopicName { get; set; } = string.Empty;
	public List<QuizOptionDto> Options { get; set; } = new();
}

// ── Grading (teacher) ──────────────────────────────────────────────────────
public class GradeAnswerViewModel
{
	public decimal ManualMarksObtained { get; set; }
	public string? TeacherFeedback { get; set; }
}

public class PendingGradeDto
{
	public Guid AnswerId { get; set; }
	public Guid AttemptId { get; set; }
	public Guid QuestionId { get; set; }
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
	public string QuizCode { get; set; }
	public string LessonTitle { get; set; }
	public int QuestionType { get; set; }
	public string? TypedAnswer { get; set; }
	public string? BoardSessionId { get; set; }
	public string? AudioUrl { get; set; }
	public decimal MaxMarks { get; set; }
	public string? SubmittedAt { get; set; }
}

// ── Results & Analytics ────────────────────────────────────────────────────
public class QuizAnswerResultDto
{
	public Guid QuestionId { get; set; }
	public int QuestionType { get; set; }
	public decimal MaxMarks { get; set; }
	public decimal? MarksObtained { get; set; }
	public bool? IsCorrect { get; set; }
	public string? TypedAnswer { get; set; }
	public string? TeacherFeedback { get; set; }
	public bool IsSkipped { get; set; }
}

public class QuizResultDto
{
	public Guid AttemptId { get; set; }
	public string QuizCode { get; set; }
	public int AttemptNumber { get; set; }
	public decimal TotalMarks { get; set; }
	public decimal AutoMarksObtained { get; set; }
	public decimal ManualMarksObtained { get; set; }
	public decimal FinalScorePercent { get; set; }
	public bool? IsPassed { get; set; }
	public string Status { get; set; }
	public string? SubmittedAt { get; set; }
	public List<QuizAnswerResultDto> Answers { get; set; } = new();
}

public class LessonQuizResultDto
{
	public Guid AttemptId { get; set; }
	public Guid StudentId { get; set; }
	public string StudentName { get; set; }
	public int AttemptNumber { get; set; }
	public decimal FinalScorePercent { get; set; }
	public bool? IsPassed { get; set; }
	public string Status { get; set; }
	public string? SubmittedAt { get; set; }
}

public class QuestionAnalyticsDto
{
	public Guid QuestionId { get; set; }
	public string QuestionTitle { get; set; }
	public int QuestionType { get; set; }
	public decimal MaxMarks { get; set; }
	public decimal AverageMarksObtained { get; set; }
	public decimal SuccessRate { get; set; }
	public int TotalAttempts { get; set; }
}

public class QuizAnalyticsDto
{
	public Guid LessonId { get; set; }
	public string QuizCode { get; set; }
	public int TotalStudents { get; set; }
	public int TotalAttempts { get; set; }
	public decimal AverageScore { get; set; }
	public decimal PassRate { get; set; }
	public List<QuestionAnalyticsDto> PerQuestionStats { get; set; } = new();
}

public class StudentQuizHistoryDto
{
	public Guid AttemptId { get; set; }
	public string QuizCode { get; set; }
	public Guid LessonId { get; set; }
	public string LessonTitle { get; set; }
	public int AttemptNumber { get; set; }
	public decimal FinalScorePercent { get; set; }
	public bool? IsPassed { get; set; }
	public string Status { get; set; }
	public string? SubmittedAt { get; set; }
}

