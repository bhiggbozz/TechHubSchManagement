using System;

namespace TechHub.Core.ViewModel.Calendar
{
	public class CreateCalendarEntryViewModel
	{
		public string Title { get; set; } = string.Empty;
		/// <summary>Assessment | Quiz | MidTerm | Exam | Holiday | Other</summary>
		public string EntryType { get; set; } = string.Empty;
		public string? Description { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		/// <summary>School | Classroom | Subject</summary>
		public string ScopeType { get; set; } = string.Empty;
		public Guid? ClassroomId { get; set; }
		public Guid? SubjectId { get; set; }
	}

	/// <summary>Scope is fixed at creation; everything else can change.</summary>
	public class UpdateCalendarEntryViewModel
	{
		public string Title { get; set; } = string.Empty;
		public string EntryType { get; set; } = string.Empty;
		public string? Description { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
	}

	public class CalendarEntryQueryViewModel
	{
		public DateTime? From { get; set; }
		public DateTime? To { get; set; }
		public Guid? ClassroomId { get; set; }
		public Guid? SubjectId { get; set; }
		/// <summary>For a parent: show just this child's calendar (must be their own child). Omitted = all their children.</summary>
		public Guid? StudentId { get; set; }
	}
}
