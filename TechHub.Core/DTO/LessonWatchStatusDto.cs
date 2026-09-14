using System;
using System.Collections.Generic;

namespace TechHub.Core.DTO
{
	public class LessonWatchStatusDto
	{
		public Guid LessonId { get; set; }
		public string Aim { get; set; } = string.Empty;
		public Guid ClassroomId { get; set; }
		public string ClassroomName { get; set; } = string.Empty;
		public int TotalStudents { get; set; }
		public int WatchedCount { get; set; }
		public decimal WatchedRate { get; set; }
		public List<StudentWatchStatusDto> Students { get; set; } = new();
	}

	public class StudentWatchStatusDto
	{
		public Guid StudentId { get; set; }
		public string StudentName { get; set; } = string.Empty;
		public bool HasWatched { get; set; }
		public string? WatchedAt { get; set; }
	}
}
