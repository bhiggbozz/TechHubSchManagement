using System;

namespace TechHub.Core.Entities
{
	/// <summary>
	/// A live class: one teacher plus up to three picked students, audio over LiveKit and the board
	/// over SignalR. Always started from an Approved lesson; the teacher's normal 1-minute board
	/// batches keep recording it, so finishing it publishes the lesson as a replay.
	/// </summary>
	public class LiveClass
	{
		public Guid Id { get; set; }
		public Guid SchoolId { get; set; }
		public Guid LessonId { get; set; }
		public Guid ClassroomId { get; set; }
		public Guid? SubjectId { get; set; }
		public Guid TeacherId { get; set; }
		public string Title { get; set; } = string.Empty;
		public string Status { get; set; } = LiveClassStatus.Scheduled;
		public int DurationMinutes { get; set; }
		public int MaxStudents { get; set; }
		/// <summary>The board session id the teacher's client records under (existing /api/board/session/{id}/batch + manifest).</summary>
		public Guid BoardSessionId { get; set; }
		public string RoomName { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; }
		public DateTime? StartedAt { get; set; }
		public DateTime? EndsAt { get; set; }
		public DateTime? EndedAt { get; set; }
		public string? EndReason { get; set; }
		/// <summary>The 5-minute and 2-minute warnings have been sent.</summary>
		public bool Warned5 { get; set; }
		public bool Warned2 { get; set; }
		public bool IsActive { get; set; } = true;
	}

	/// <summary>One of the (at most three) students the teacher picked. Only these students can ever join.</summary>
	public class LiveClassStudent
	{
		public Guid Id { get; set; }
		public Guid LiveClassId { get; set; }
		public Guid StudentId { get; set; }
		public Guid SchoolId { get; set; }
		public DateTime? FirstJoinedAt { get; set; }
		public DateTime? LastLeftAt { get; set; }
		public int JoinCount { get; set; }
		/// <summary>The teacher has let this student speak. Students listen only until then.</summary>
		public bool CanSpeak { get; set; }
	}

	public static class LiveClassStatus
	{
		public const string Scheduled = "Scheduled";
		public const string Live = "Live";
		public const string Ended = "Ended";
		public const string Cancelled = "Cancelled";
	}

	public static class LiveClassEndReason
	{
		public const string TeacherEnded = "TeacherEnded";
		public const string TimeUp = "TimeUp";
		public const string Cancelled = "Cancelled";
		public const string Expired = "Expired";
	}
}
