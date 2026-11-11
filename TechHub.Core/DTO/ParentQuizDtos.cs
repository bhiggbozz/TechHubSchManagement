using System;
using System.Collections.Generic;

namespace TechHub.Core.DTO
{
	/// <summary>How many usable questions exist, by difficulty — so the parent sees what is available before creating a quiz.</summary>
	public class ParentQuizAvailabilityDto
	{
		public int Easy { get; set; }
		public int Medium { get; set; }
		public int Hard { get; set; }
		public int ExamLevel { get; set; }
		public int Total => Easy + Medium + Hard + ExamLevel;
	}

	public class ParentQuizTopicDto
	{
		public Guid TopicId { get; set; }
		public string Name { get; set; } = string.Empty;
		public ParentQuizAvailabilityDto Available { get; set; } = new();
		/// <summary>Only sub-topics that have at least one usable question.</summary>
		public List<ParentQuizSubTopicDto> SubTopics { get; set; } = new();
	}

	public class ParentQuizSubTopicDto
	{
		public Guid SubTopicId { get; set; }
		public string Name { get; set; } = string.Empty;
		public ParentQuizAvailabilityDto Available { get; set; } = new();
	}

	public class ParentQuizSubjectDto
	{
		public Guid SubjectId { get; set; }
		public string Name { get; set; } = string.Empty;
		/// <summary>All usable questions in the subject, including ones with no topic.</summary>
		public ParentQuizAvailabilityDto Available { get; set; } = new();
		public List<ParentQuizTopicDto> Topics { get; set; } = new();
	}

	public class ParentQuizSettingsDto
	{
		public int QuestionCount { get; set; }
		public string Mode { get; set; } = string.Empty;
		public int? TimeLimitMinutes { get; set; }
		public int PassMarkPercent { get; set; }
		public bool ShuffleQuestions { get; set; }
		/// <summary>False until the parent has saved their own settings (the values above are then the built-in defaults).</summary>
		public bool IsCustomised { get; set; }
	}

	/// <summary>One quiz in a list — for the parent (Student* fields filled) and the child.</summary>
	public class ParentQuizSummaryDto
	{
		public Guid Id { get; set; }
		public Guid StudentId { get; set; }
		public string StudentName { get; set; } = string.Empty;
		public string ParentName { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string SubjectName { get; set; } = string.Empty;
		public string? TopicNames { get; set; }
		public string Mode { get; set; } = string.Empty;
		public int QuestionCount { get; set; }
		public int? TimeLimitMinutes { get; set; }
		public int PassMarkPercent { get; set; }
		/// <summary>Pending | InProgress | Completed | Cancelled</summary>
		public string Status { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; }
		public DateTime? CompletedAt { get; set; }
		public decimal? ScorePercent { get; set; }
		public bool? IsPassed { get; set; }
		public int? TotalCorrect { get; set; }
	}

	public class ParentQuizOptionDto
	{
		public Guid OptionId { get; set; }
		public string? OptionLabel { get; set; }
		public string? OptionText { get; set; }
		public string? OptionHtml { get; set; }
		public string? ContentParts { get; set; }
		public bool? HasLatex { get; set; }
		public int? OrderIndex { get; set; }
	}

	/// <summary>A question as the child sees it while sitting the quiz — no correct answer.</summary>
	public class ParentQuizQuestionDto
	{
		public Guid QuestionId { get; set; }
		public int DisplayOrder { get; set; }
		public string? Title { get; set; }
		public string? TextContent { get; set; }
		public string? QuestionHtml { get; set; }
		public string? ContentParts { get; set; }
		public bool? HasLatex { get; set; }
		public string? ImageUrl { get; set; }
		public int QuestionType { get; set; }
		public int DifficultyLevel { get; set; }
		public List<ParentQuizOptionDto> Options { get; set; } = new();
	}

	public class ParentQuizStartDto
	{
		public Guid QuizId { get; set; }
		public Guid AttemptId { get; set; }
		public string Title { get; set; } = string.Empty;
		public int? TimeLimitMinutes { get; set; }
		/// <summary>UTC. Lets the client show the correct time left when the child resumes.</summary>
		public DateTime StartedAt { get; set; }
		public List<ParentQuizQuestionDto> Questions { get; set; } = new();
	}

	/// <summary>One question in a finished quiz, with the child's pick and the right answer — the parent's review.</summary>
	public class ParentQuizReviewItemDto
	{
		public Guid QuestionId { get; set; }
		public int DisplayOrder { get; set; }
		public string? Title { get; set; }
		public string? TextContent { get; set; }
		public string? QuestionHtml { get; set; }
		public string? ContentParts { get; set; }
		public bool? HasLatex { get; set; }
		public string? ImageUrl { get; set; }
		public int DifficultyLevel { get; set; }
		public bool IsCorrect { get; set; }
		public bool IsSkipped { get; set; }
		public Guid? SelectedOptionId { get; set; }
		public Guid? CorrectOptionId { get; set; }
		public List<ParentQuizReviewOptionDto> Options { get; set; } = new();
	}

	public class ParentQuizReviewOptionDto
	{
		public Guid OptionId { get; set; }
		public string? OptionLabel { get; set; }
		public string? OptionText { get; set; }
		public string? OptionHtml { get; set; }
		public string? ContentParts { get; set; }
		public bool? HasLatex { get; set; }
		public bool IsCorrect { get; set; }
		public bool WasSelected { get; set; }
	}

	public class ParentQuizResultDto
	{
		public ParentQuizSummaryDto Quiz { get; set; } = new();
		public int TotalQuestions { get; set; }
		public int TotalCorrect { get; set; }
		public int TotalWrong { get; set; }
		public int TotalSkipped { get; set; }
		public int? TimeTakenSeconds { get; set; }
		public List<ParentQuizReviewItemDto> Questions { get; set; } = new();
	}

	public class ParentQuizCreatedDto
	{
		public ParentQuizSummaryDto Quiz { get; set; } = new();
		/// <summary>Set when fewer questions were available than requested, e.g. "Only 8 questions were available, so your quiz has 8."</summary>
		public string? Notice { get; set; }
	}
}
