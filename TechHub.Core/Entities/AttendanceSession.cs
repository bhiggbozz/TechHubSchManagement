using System;

namespace TechHub.Core.Entities
{
	/// <summary>
	/// A window during which a teacher can scan student QR codes to record
	/// attendance for a class, a subject or a sub-topic.
	/// </summary>
	public class AttendanceSession
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public string CreationDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public string ModifiedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public Guid SchoolId { get; set; }
		public Guid TeacherId { get; set; }
		public int AttendanceType { get; set; }
		public Guid? ClassroomId { get; set; }
		public Guid? SubjectId { get; set; }
		public Guid? SubTopicId { get; set; }
		public Guid? ClassPreparationId { get; set; }
		public int Status { get; set; }
		public string StartedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public string? EndedAt { get; set; }
		public Guid CreatedBy { get; set; }
		public bool IsActive { get; set; } = true;
	}
}
