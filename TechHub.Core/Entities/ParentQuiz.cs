using System;

namespace TechHub.Core.Entities
{
	/// <summary>
	/// A parent's saved defaults for the quizzes they set for their children, so creating one
	/// is a single button press. One row per (parent, school).
	/// </summary>
	public class ParentQuizConfig
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public Guid SchoolId { get; set; }
		public Guid ParentId { get; set; }
		public int QuestionCount { get; set; } = ParentQuizDefaults.QuestionCount;
		/// <summary>One of <see cref="ParentQuizModes"/>.</summary>
		public string Mode { get; set; } = ParentQuizModes.Mix;
		/// <summary>Null = no time limit.</summary>
		public int? TimeLimitMinutes { get; set; }
		public int PassMarkPercent { get; set; } = ParentQuizDefaults.PassMarkPercent;
		public bool ShuffleQuestions { get; set; } = true;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? UpdatedAt { get; set; }
	}

	/// <summary>
	/// A practice quiz a parent set for one child, built from the school's published objective
	/// questions. Deliberately separate from teacher <c>Assessments</c>/<c>Quiz</c>: it never
	/// appears in school rankings, performance snapshots or teacher lists.
	/// </summary>
	public class ParentQuiz
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public Guid SchoolId { get; set; }
		public Guid ParentId { get; set; }
		public Guid StudentId { get; set; }
		public Guid SubjectId { get; set; }
		/// <summary>Snapshots, so the quiz still reads correctly if a subject/topic is renamed later.</summary>
		public string SubjectName { get; set; } = string.Empty;
		public string? TopicNames { get; set; }
		public string Title { get; set; } = string.Empty;
		public string Mode { get; set; } = ParentQuizModes.Mix;
		public int QuestionCount { get; set; }
		public int? TimeLimitMinutes { get; set; }
		public int PassMarkPercent { get; set; }
		/// <summary>One of <see cref="ParentQuizStatus"/>.</summary>
		public string Status { get; set; } = ParentQuizStatus.Pending;
		/// <summary>Academic session/term at creation, for term reports later (null when the school has no calendar for that date).</summary>
		public Guid? SessionId { get; set; }
		public Guid? TermId { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? CompletedAt { get; set; }
		public bool IsActive { get; set; } = true;
	}

	public class ParentQuizQuestion
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public Guid QuizId { get; set; }
		public Guid QuestionId { get; set; }
		public int DisplayOrder { get; set; }
		public int DifficultyLevel { get; set; }
	}

	/// <summary>The child's single attempt at a parent quiz (a unique index on QuizId keeps it to one).</summary>
	public class ParentQuizAttempt
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public Guid QuizId { get; set; }
		public Guid StudentId { get; set; }
		public Guid SchoolId { get; set; }
		public string Status { get; set; } = ParentQuizAttemptStatus.InProgress;
		public DateTime StartedAt { get; set; } = DateTime.UtcNow;
		public DateTime? SubmittedAt { get; set; }
		public int? TimeTakenSeconds { get; set; }
		public int TotalQuestions { get; set; }
		public int TotalCorrect { get; set; }
		public int TotalWrong { get; set; }
		public int TotalSkipped { get; set; }
		public decimal? ScorePercent { get; set; }
		public bool? IsPassed { get; set; }
	}

	public class ParentQuizAnswer
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public Guid AttemptId { get; set; }
		public Guid QuestionId { get; set; }
		public Guid? SelectedOptionId { get; set; }
		public bool IsCorrect { get; set; }
		public bool IsSkipped { get; set; }
	}

	public static class ParentQuizModes
	{
		public const string Mix = "Mix";
		public const string Easy = "Easy";
		public const string Medium = "Medium";
		public const string Hard = "Hard";
		public const string ExamLevel = "ExamLevel";

		public static readonly string[] All = { Mix, Easy, Medium, Hard, ExamLevel };
	}

	public static class ParentQuizStatus
	{
		public const string Pending = "Pending";
		public const string InProgress = "InProgress";
		public const string Completed = "Completed";
		public const string Cancelled = "Cancelled";
	}

	public static class ParentQuizAttemptStatus
	{
		public const string InProgress = "InProgress";
		public const string Submitted = "Submitted";
	}

	public static class ParentQuizDefaults
	{
		public const int QuestionCount = 10;
		public const int MinQuestions = 5;
		public const int MaxQuestions = 30;
		public const int PassMarkPercent = 50;
		public const int MaxTimeLimitMinutes = 180;
		/// <summary>Unstarted quizzes one child can have waiting at once.</summary>
		public const int MaxPendingPerChild = 5;
		/// <summary>Quizzes one parent can create per day.</summary>
		public const int MaxCreatedPerParentPerDay = 10;
	}
}
