using System;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Attendance;

namespace TechHub.Service.Interface
{
	public interface IAttendanceService
	{
		/// <summary>Gets (or lazily generates) a student's unique QR token.</summary>
		Task<BaseResponse> GetStudentQrTokenAsync(Guid studentId, AuthenticatedUserClaims claims);

		/// <summary>Returns a printable QR PNG for a student's token.</summary>
		Task<StudentQrCodeResult> GetStudentQrCodeAsync(Guid studentId, AuthenticatedUserClaims claims);

		/// <summary>Opens a new attendance session (Class / Subject / SubTopic).</summary>
		Task<BaseResponse> StartSessionAsync(StartAttendanceSessionViewModel model, AuthenticatedUserClaims claims);

		/// <summary>Closes an open attendance session.</summary>
		Task<BaseResponse> EndSessionAsync(Guid sessionId, AuthenticatedUserClaims claims);

		/// <summary>Registers a student as present by scanning their QR token.</summary>
		Task<BaseResponse> ScanStudentAsync(Guid sessionId, string qrToken, AuthenticatedUserClaims claims);

		/// <summary>Returns a session with its attendance records.</summary>
		Task<BaseResponse> GetSessionAsync(Guid sessionId, AuthenticatedUserClaims claims);

		/// <summary>Returns a teacher's own sessions, optionally filtered by type.</summary>
		Task<BaseResponse> GetSessionsAsync(AuthenticatedUserClaims claims, int? attendanceType, int pageNumber, int pageSize);

		/// <summary>Returns present/absent breakdown for a session against the expected roster.</summary>
		Task<BaseResponse> GetSessionSummaryAsync(Guid sessionId, AuthenticatedUserClaims claims);

		/// <summary>Returns a student's attendance history (self or staff).</summary>
		Task<BaseResponse> GetStudentAttendanceAsync(Guid studentId, AuthenticatedUserClaims claims);

		/// <summary>Gets an admin attendance analytics report (per class / per subject) for a period.</summary>
		Task<BaseResponse> GetAttendanceAnalyticsAsync(AuthenticatedUserClaims claims, int period, string? date, string? month, string? fromMonth, string? toMonth, int? attendanceType, Guid? classroomId, Guid? subjectId);

		/// <summary>Lists students who were absent in a class or subject over a period.</summary>
		Task<BaseResponse> GetAbsentStudentsAsync(AuthenticatedUserClaims claims, int attendanceType, Guid? classroomId, Guid? subjectId, int period, string? date, string? month, string? fromMonth, string? toMonth);

		/// <summary>Gets a student's attendance stats for a class or subject over a period.</summary>
		Task<BaseResponse> GetStudentAttendanceStatsAsync(AuthenticatedUserClaims claims, Guid studentId, int attendanceType, Guid? classroomId, Guid? subjectId, int period, string? date, string? month, string? fromMonth, string? toMonth);
	}
}
