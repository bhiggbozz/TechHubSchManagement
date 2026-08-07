using System;
using System.Collections.Generic;

namespace TechHub.Core.DTO
{
	public class AttendanceStudentDto
	{
		public Guid StudentId { get; set; }
		public string StudentName { get; set; }
	}

	public class AttendanceRecordDto
	{
		public Guid Id { get; set; }
		public Guid SessionId { get; set; }
		public Guid StudentId { get; set; }
		public string StudentName { get; set; }
		public bool IsPresent { get; set; }
		public bool IsManual { get; set; }
		public string AttendedAt { get; set; }
	}

	public class AttendanceSessionDto
	{
		public Guid Id { get; set; }
		public Guid TeacherId { get; set; }
		public string TeacherName { get; set; }
		public int AttendanceType { get; set; }
		public string AttendanceTypeName { get; set; }
		public Guid? ClassroomId { get; set; }
		public string ClassroomName { get; set; }
		public Guid? SubjectId { get; set; }
		public string SubjectName { get; set; }
		public Guid? SubTopicId { get; set; }
		public string SubTopicName { get; set; }
		public int Status { get; set; }
		public string StatusName { get; set; }
		public string StartedAt { get; set; }
		public string EndedAt { get; set; }
		public int PresentCount { get; set; }
	}

	public class AttendanceSessionDetailDto : AttendanceSessionDto
	{
		public List<AttendanceRecordDto> Records { get; set; } = new();
	}

	public class AttendanceSummaryDto
	{
		public Guid SessionId { get; set; }
		public int RosterCount { get; set; }
		public int PresentCount { get; set; }
		public int AbsentCount { get; set; }
		public List<AttendanceStudentDto> Present { get; set; } = new();
		public List<AttendanceStudentDto> Absent { get; set; } = new();
	}

	public class StudentAttendanceHistoryDto
	{
		public Guid SessionId { get; set; }
		public int AttendanceType { get; set; }
		public string AttendanceTypeName { get; set; }
		public string SubjectName { get; set; }
		public string ClassroomName { get; set; }
		public string SubTopicName { get; set; }
		public string StartedAt { get; set; }
		public int SessionStatus { get; set; }
		public bool IsPresent { get; set; }
		public string AttendedAt { get; set; }
	}

	/// <summary>
	/// Result of generating a student QR image. Carries either the raw PNG bytes
	/// (Success = true) or an API error to render as a standard BaseResponse.
	/// </summary>
	public class StudentQrCodeResult
	{
		public bool Success { get; set; }
		public string ResponseCode { get; set; }
		public string ResponseMessage { get; set; }
		public byte[] Bytes { get; set; }
		public string FileName { get; set; }
	}
}
