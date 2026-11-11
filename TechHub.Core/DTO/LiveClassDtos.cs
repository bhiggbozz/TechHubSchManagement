using System;
using System.Collections.Generic;

namespace TechHub.Core.DTO
{
	public class LiveClassStudentDto
	{
		public Guid StudentId { get; set; }
		public string Name { get; set; } = string.Empty;
		public DateTime? FirstJoinedAt { get; set; }
		public bool CanSpeak { get; set; }
	}

	public class LiveClassDto
	{
		public Guid Id { get; set; }
		public Guid LessonId { get; set; }
		public string Title { get; set; } = string.Empty;
		public Guid ClassroomId { get; set; }
		public string? ClassroomName { get; set; }
		public Guid? SubjectId { get; set; }
		public string? SubjectName { get; set; }
		public Guid TeacherId { get; set; }
		public string? TeacherName { get; set; }
		/// <summary>Scheduled | Live | Ended | Cancelled</summary>
		public string Status { get; set; } = string.Empty;
		public int DurationMinutes { get; set; }
		public int MaxStudents { get; set; }
		/// <summary>The teacher's client records the board under this session id (existing board batch + manifest endpoints).</summary>
		public Guid BoardSessionId { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime? StartedAt { get; set; }
		public DateTime? EndsAt { get; set; }
		public DateTime? EndedAt { get; set; }
		public string? EndReason { get; set; }
		/// <summary>Scheduled classes: the slot (UTC). Null for a class that is started on the spot.</summary>
		public DateTime? ScheduledStart { get; set; }
		public DateTime? ScheduledEnd { get; set; }
		/// <summary>Status of the lesson behind the class (PendingApproval | Approved | Rejected | Published).</summary>
		public string? LessonStatus { get; set; }
		/// <summary>Teacher: the Start button may be pressed now (approved, and inside the slot). Ignore for students.</summary>
		public bool CanStartNow { get; set; }
		/// <summary>Server clock, so the "starts in..." countdown does not depend on the device clock.</summary>
		public DateTime ServerTime { get; set; }
		public List<LiveClassStudentDto> Students { get; set; } = new();
		/// <summary>The lesson's media, for the app to download ahead of time. Only filled by GET /{id} and join, not in the list.</summary>
		public List<LiveClassMediaDto>? Media { get; set; }
	}

	/// <summary>Everything a browser needs to enter a live class.</summary>
	public class LiveClassJoinDto
	{
		public Guid LiveClassId { get; set; }
		/// <summary>Teacher | Student</summary>
		public string Role { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public Guid LessonId { get; set; }
		public Guid BoardSessionId { get; set; }
		public LiveClassAudioDto Audio { get; set; } = new();
		public LiveClassBoardDto Board { get; set; } = new();
		public DateTime EndsAt { get; set; }
		public int SecondsRemaining { get; set; }
		/// <summary>Server clock, so the countdown does not depend on the device clock.</summary>
		public DateTime ServerTime { get; set; }
		/// <summary>Students only: whether the teacher has let them speak.</summary>
		public bool CanSpeak { get; set; }
		/// <summary>The lesson's media. The teacher opens and closes them by id during the class; the app downloads them first.</summary>
		public List<LiveClassMediaDto> Media { get; set; } = new();
	}

	/// <summary>One file attached to the lesson. Only its details travel here; the app downloads the file from <see cref="Url"/>.</summary>
	public class LiveClassMediaDto
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public string MediaType { get; set; } = string.Empty;
		public string? FileExtension { get; set; }
		/// <summary>Cloudinary address the app downloads from.</summary>
		public string Url { get; set; } = string.Empty;
		public long SizeBytes { get; set; }
		public int? Duration { get; set; }
		public int DisplayOrder { get; set; }
		public string? MetaData { get; set; }
	}

	public class LiveClassAudioDto
	{
		/// <summary>wss:// address of the LiveKit server.</summary>
		public string Url { get; set; } = string.Empty;
		public string Token { get; set; } = string.Empty;
		public string RoomName { get; set; } = string.Empty;
	}

	public class LiveClassBoardDto
	{
		/// <summary>SignalR hub path on the API host; pass the JWT as ?access_token=.</summary>
		public string HubPath { get; set; } = "/hubs/live-board";
		public string Group { get; set; } = string.Empty;
	}
}
