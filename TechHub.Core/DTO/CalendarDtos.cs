using System.Collections.Generic;

namespace TechHub.Core.DTO
{
	/// <summary>Dates are returned as plain "yyyy-MM-dd" strings (no time, no timezone).</summary>
	public class AcademicTermDto
	{
		public System.Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Sequence { get; set; }
		public string StartDate { get; set; } = string.Empty;
		public string EndDate { get; set; } = string.Empty;
		/// <summary>Upcoming | Current | Past — relative to today in the school's local date.</summary>
		public string Status { get; set; } = string.Empty;
	}

	public class AcademicSessionDto
	{
		public System.Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string StartDate { get; set; } = string.Empty;
		public string EndDate { get; set; } = string.Empty;
		public string Status { get; set; } = string.Empty;
		public List<AcademicTermDto> Terms { get; set; } = new();
	}

	public class CalendarTemplateDto
	{
		public string Key { get; set; } = string.Empty;
		public string Label { get; set; } = string.Empty;
		/// <summary>Suggested term names to pre-fill the form; empty for "Custom".</summary>
		public List<string> TermNames { get; set; } = new();
	}
}
