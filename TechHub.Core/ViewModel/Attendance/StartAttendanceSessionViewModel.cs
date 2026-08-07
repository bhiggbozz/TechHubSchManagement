using System;

namespace TechHub.Core.ViewModel.Attendance
{
	/// <summary>
	/// Request to open an attendance session.
	/// Which fields are required depends on AttendanceType:
	///  - Class:    ClassroomId required
	///  - Subject:  SubjectId required, ClassroomId optional
	///  - SubTopic: SubTopicId required, ClassroomId optional
	/// </summary>
	public class StartAttendanceSessionViewModel
	{
		public int AttendanceType { get; set; }
		public Guid? ClassroomId { get; set; }
		public Guid? SubjectId { get; set; }
		public Guid? SubTopicId { get; set; }
		public Guid? ClassPreparationId { get; set; }
	}
}
