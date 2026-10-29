using System.Collections.Generic;

namespace TechHub.Core.DTO
{
	/// <summary>Dates are plain "yyyy-MM-dd" strings; <see cref="ChangedAt"/> is UTC ISO-8601.</summary>
	public class CalendarEntryChangeDto
	{
		public System.Guid ChangedById { get; set; }
		public string ChangedByName { get; set; } = string.Empty;
		/// <summary>The role of the person who made the change (e.g. HeadTeacher, Administrator).</summary>
		public string ChangedByRole { get; set; } = string.Empty;
		public string ChangedAt { get; set; } = string.Empty;
		public string Action { get; set; } = string.Empty;
		public bool OnOthersEntry { get; set; }
		public string? PreviousTitle { get; set; }
		public string? NewTitle { get; set; }
		public string? PreviousEntryType { get; set; }
		public string? NewEntryType { get; set; }
		public string? PreviousStartDate { get; set; }
		public string? PreviousEndDate { get; set; }
		public string? NewStartDate { get; set; }
		public string? NewEndDate { get; set; }
		/// <summary>Ready-to-show sentence, e.g. "Updated by head teacher Ada Obi on 12 Feb 2027: dates changed from 3 Mar – 7 Mar 2027 to 10 Mar – 14 Mar 2027."</summary>
		public string Message { get; set; } = string.Empty;
	}

	public class CalendarEntryDto
	{
		public System.Guid Id { get; set; }
		public System.Guid SessionId { get; set; }
		public System.Guid? TermId { get; set; }
		public string Title { get; set; } = string.Empty;
		public string EntryType { get; set; } = string.Empty;
		public string? Description { get; set; }
		public string StartDate { get; set; } = string.Empty;
		public string EndDate { get; set; } = string.Empty;
		/// <summary>Upcoming | Current | Past</summary>
		public string Status { get; set; } = string.Empty;
		public string ScopeType { get; set; } = string.Empty;
		public System.Guid? ClassroomId { get; set; }
		public string? ClassroomName { get; set; }
		public System.Guid? SubjectId { get; set; }
		public string? SubjectName { get; set; }
		public System.Guid CreatedById { get; set; }
		public string CreatedByName { get; set; } = string.Empty;
		public string CreatedByRole { get; set; } = string.Empty;
		/// <summary>True when an administrator created it — the entries the school treats as the official timeline.</summary>
		public bool IsAdminEntry { get; set; }
		/// <summary>Whether the caller may edit or delete it.</summary>
		public bool CanEdit { get; set; }
		/// <summary>The most recent change made by someone other than the creator, or null if there is none.</summary>
		public CalendarEntryChangeDto? LastChange { get; set; }
		/// <summary>Full change history; only filled on the single-entry endpoint.</summary>
		public List<CalendarEntryChangeDto>? Changes { get; set; }
	}

	public class CalendarEntryTypeDto
	{
		public string Key { get; set; } = string.Empty;
		public string Label { get; set; } = string.Empty;
	}
}
