using System;

namespace TechHub.Core.Entities
{
	/// <summary>
	/// A dated period on the school calendar (assessment week, quiz period, mid-term, exam,
	/// holiday …). View-only: it never blocks or schedules real quizzes/assessments.
	/// Scope is the whole school, one classroom, or one subject (optionally within a classroom).
	/// </summary>
	public class CalendarEntry
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public Guid SchoolId { get; set; }
		public Guid SessionId { get; set; }
		/// <summary>The term the entry falls inside; null only for admin entries that sit in a break between terms.</summary>
		public Guid? TermId { get; set; }
		public string Title { get; set; } = string.Empty;
		public string EntryType { get; set; } = string.Empty;
		public string? Description { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public string ScopeType { get; set; } = string.Empty;
		public Guid? ClassroomId { get; set; }
		public Guid? SubjectId { get; set; }
		public Guid CreatedBy { get; set; }
		/// <summary>The creator's role at the time, so the entry keeps its authority level even if the role later changes.</summary>
		public string CreatedByRole { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime? ModifiedAt { get; set; }
		/// <summary>Highest authority level (1 teacher, 2 head teacher, 3 admin) that has changed someone else's entry. Anyone below that level can no longer edit it.</summary>
		public int SupersededLevel { get; set; }
		public bool IsActive { get; set; } = true;
	}

	/// <summary>One edit or delete of an entry, with the values before and after.</summary>
	public class CalendarEntryChange
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public Guid EntryId { get; set; }
		public Guid SchoolId { get; set; }
		public Guid ChangedBy { get; set; }
		public string ChangedByRole { get; set; } = string.Empty;
		public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
		/// <summary>Updated | Deleted</summary>
		public string Action { get; set; } = string.Empty;
		public string? OldTitle { get; set; }
		public string? NewTitle { get; set; }
		public string? OldEntryType { get; set; }
		public string? NewEntryType { get; set; }
		public DateTime? OldStartDate { get; set; }
		public DateTime? OldEndDate { get; set; }
		public DateTime? NewStartDate { get; set; }
		public DateTime? NewEndDate { get; set; }
		/// <summary>True when the person changing it is not the entry's creator — these are the changes viewers are told about.</summary>
		public bool OnOthersEntry { get; set; }
	}

	public static class CalendarEntryTypes
	{
		public const string Assessment = "Assessment";
		public const string Quiz = "Quiz";
		public const string MidTerm = "MidTerm";
		public const string Exam = "Exam";
		public const string Holiday = "Holiday";
		public const string Other = "Other";

		public static readonly string[] All = { Assessment, Quiz, MidTerm, Exam, Holiday, Other };
	}

	public static class CalendarEntryScopes
	{
		public const string School = "School";
		public const string Classroom = "Classroom";
		public const string Subject = "Subject";
	}
}
