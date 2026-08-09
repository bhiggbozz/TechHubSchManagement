using System;
using System.Collections.Generic;

namespace TechHub.Core.DTO
{
	public class AttendanceAnalyticsDto
	{
		public string Period { get; set; }
		public string PeriodLabel { get; set; }
		public string FromDate { get; set; }
		public string ToDate { get; set; }
		public List<AttendanceAnalyticsBucketDto> Buckets { get; set; } = new();
		public AttendanceAnalyticsTotalsDto Totals { get; set; } = new();
	}

	public class AttendanceAnalyticsBucketDto
	{
		public string Label { get; set; }
		public string FromDate { get; set; }
		public string ToDate { get; set; }
		public List<AttendanceAnalyticsRowDto> ByClass { get; set; } = new();
		public List<AttendanceAnalyticsRowDto> BySubject { get; set; } = new();
		public AttendanceAnalyticsTotalsDto Totals { get; set; } = new();
	}

	public class AttendanceAnalyticsRowDto
	{
		public Guid Id { get; set; }
		public string Name { get; set; }
		public int Sessions { get; set; }
		public int Expected { get; set; }
		public int Present { get; set; }
		public int Absent { get; set; }
		public decimal AttendanceRate { get; set; }
	}

	public class AttendanceAnalyticsTotalsDto
	{
		public int Sessions { get; set; }
		public int Expected { get; set; }
		public int Present { get; set; }
		public int Absent { get; set; }
		public decimal AttendanceRate { get; set; }
	}

	public class AttendanceAbsentStudentsDto
	{
		public int AttendanceType { get; set; }
		public string AttendanceTypeName { get; set; }
		public Guid? ClassroomId { get; set; }
		public string ClassroomName { get; set; }
		public Guid? SubjectId { get; set; }
		public string SubjectName { get; set; }
		public string Period { get; set; }
		public string PeriodLabel { get; set; }
		public string FromDate { get; set; }
		public string ToDate { get; set; }
		public int TotalStudents { get; set; }
		public int Sessions { get; set; }
		public List<AttendanceStudentStatsDto> Students { get; set; } = new();
		public AttendanceStudentStatsTotalsDto Totals { get; set; } = new();
	}

	public class AttendanceStudentStatsDto
	{
		public Guid StudentId { get; set; }
		public string StudentName { get; set; }
		public int Sessions { get; set; }
		public int PresentCount { get; set; }
		public int AbsentCount { get; set; }
		public decimal AttendanceRate { get; set; }
	}

	public class AttendanceStudentStatsTotalsDto
	{
		public int Students { get; set; }
		public int Sessions { get; set; }
		public int PresentCount { get; set; }
		public int AbsentCount { get; set; }
		public decimal AttendanceRate { get; set; }
	}

	public class AttendanceStudentStatsResponseDto
	{
		public Guid StudentId { get; set; }
		public string StudentName { get; set; }
		public int AttendanceType { get; set; }
		public string AttendanceTypeName { get; set; }
		public Guid? ClassroomId { get; set; }
		public string ClassroomName { get; set; }
		public Guid? SubjectId { get; set; }
		public string SubjectName { get; set; }
		public string Period { get; set; }
		public string PeriodLabel { get; set; }
		public string FromDate { get; set; }
		public string ToDate { get; set; }
		public int Sessions { get; set; }
		public int PresentCount { get; set; }
		public int AbsentCount { get; set; }
		public decimal AttendanceRate { get; set; }
	}
}