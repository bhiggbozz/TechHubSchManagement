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

	/// <summary>
	/// Schedule a live class in one call: creates its own lesson (DeliveryMode = Live), attaches media,
	/// picks the students, attaches an optional practice quiz and books the slot. This is deliberately a
	/// copy of the recorded-class submit rather than a call into it, so either flow can change on its own.
	/// </summary>
	public class ScheduleLiveClassViewModel
	{
		[System.ComponentModel.DataAnnotations.Required] public Guid ClassroomId { get; set; }
		[System.ComponentModel.DataAnnotations.Required] public Guid SubjectId { get; set; }
		[System.ComponentModel.DataAnnotations.Required] public Guid TopicId { get; set; }
		[System.ComponentModel.DataAnnotations.Required] public Guid SubTopicId { get; set; }

		[System.ComponentModel.DataAnnotations.Required]
		public string Aim { get; set; } = string.Empty;

		[System.ComponentModel.DataAnnotations.Required]
		[System.ComponentModel.DataAnnotations.StringLength(2000)]
		public string Description { get; set; } = string.Empty;

		/// <summary>Optional; defaults to the aim.</summary>
		public string? Title { get; set; }

		/// <summary>1 to 3 active students of the classroom. Only these can join.</summary>
		public List<Guid> StudentIds { get; set; } = new();

		/// <summary>Start time, with an offset or UTC "Z"; no offset = school time (WAT). Required.</summary>
		[System.ComponentModel.DataAnnotations.Required]
		public DateTime? ScheduledStart { get; set; }

		/// <summary>Class length in minutes, 5 to 40; defaults to 40.</summary>
		public int? DurationMinutes { get; set; }

		/// <summary>Files already uploaded to Cloudinary by the frontend (same shape as a lesson's media).</summary>
		public List<LiveClassMediaViewModel> MediaFiles { get; set; } = new();

		/// <summary>Optional practice quiz: a code from POST /api/quiz/create in this school (own quiz unless head teacher).</summary>
		public string? QuizCode { get; set; }
	}

	public class LiveClassMediaViewModel
	{
		[System.ComponentModel.DataAnnotations.Required] public string FileName { get; set; } = string.Empty;
		[System.ComponentModel.DataAnnotations.Required] public string OriginalFileName { get; set; } = string.Empty;
		[System.ComponentModel.DataAnnotations.Required] public string FileExtension { get; set; } = string.Empty;
		[System.ComponentModel.DataAnnotations.Required] public string CloudinaryUrl { get; set; } = string.Empty;
		[System.ComponentModel.DataAnnotations.Required] public string PublicId { get; set; } = string.Empty;
		public long FileSizeBytes { get; set; }
		public int? Duration { get; set; }
		public int DisplayOrder { get; set; }
		public string MetaData { get; set; } = string.Empty;
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
