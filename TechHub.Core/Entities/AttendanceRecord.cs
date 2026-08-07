using System;

namespace TechHub.Core.Entities
{
	/// <summary>
	/// A single student's presence within an attendance session.
	/// Inserted when a teacher scans the student's QR code (IsManual = false).
	/// </summary>
	public class AttendanceRecord
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public string CreationDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public string ModifiedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public Guid SessionId { get; set; }
		public Guid StudentId { get; set; }
		public Guid SchoolId { get; set; }
		public bool IsPresent { get; set; } = true;
		public bool IsManual { get; set; }
		public string AttendedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public Guid CreatedBy { get; set; }
		public bool IsActive { get; set; } = true;
	}
}
