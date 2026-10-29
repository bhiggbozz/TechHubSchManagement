using System;
using System.Collections.Generic;

namespace TechHub.Core.ViewModel.Calendar
{
	/// <summary>One term inside a session. Name is free text chosen by the school.</summary>
	public class TermInputViewModel
	{
		public string Name { get; set; } = string.Empty;
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
	}

	/// <summary>
	/// Creates a session together with its terms in one atomic call. The optional
	/// <see cref="Template"/> is informational only (it records which preset the admin
	/// started from) — the server never invents names or dates from it.
	/// </summary>
	public class CreateAcademicSessionViewModel
	{
		public string Name { get; set; } = string.Empty;
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public string? Template { get; set; }
		public List<TermInputViewModel> Terms { get; set; } = new();
	}

	public class UpdateAcademicSessionViewModel
	{
		public string Name { get; set; } = string.Empty;
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
	}
}
