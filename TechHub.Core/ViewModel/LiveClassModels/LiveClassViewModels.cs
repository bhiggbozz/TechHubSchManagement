using System;
using System.Collections.Generic;

namespace TechHub.Core.ViewModel.LiveClassModels
{
	public class CreateLiveClassViewModel
	{
		/// <summary>An Approved lesson the teacher owns (a head teacher may use any lesson in the school).</summary>
		public Guid LessonId { get; set; }

		/// <summary>1 to 3 students from the lesson's classroom. Only these can join.</summary>
		public List<Guid> StudentIds { get; set; } = new();

		/// <summary>Optional; defaults to the lesson's aim.</summary>
		public string? Title { get; set; }

		/// <summary>
		/// When the class starts. Send it with an offset (e.g. 2026-11-10T17:15:00+01:00) or as UTC ("Z");
		/// with no offset it is read as school time (WAT). Leave it out to set up a class that can be started on the spot
		/// from an Approved lesson (the old behaviour). When it is set, the lesson may still be waiting for approval:
		/// the students only see the class once it is approved.
		/// </summary>
		public DateTime? ScheduledStart { get; set; }

		/// <summary>Class length in minutes, 5 to 40. Only used with <see cref="ScheduledStart"/>; defaults to 40.</summary>
		public int? DurationMinutes { get; set; }
	}

	public class RescheduleLiveClassViewModel
	{
		/// <summary>The new start time (same rules as when creating).</summary>
		public DateTime ScheduledStart { get; set; }

		/// <summary>New length in minutes, 5 to 40. Leave out to keep the current length.</summary>
		public int? DurationMinutes { get; set; }
	}

	public class SetSpeakViewModel
	{
		/// <summary>true = let this student speak, false = back to listen-only.</summary>
		public bool Allow { get; set; }
	}
}
