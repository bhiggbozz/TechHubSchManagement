using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Attendance;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	[Authorize]
	public class AttendanceController : ControllerBase
	{
		private readonly IAttendanceService _attendanceService;

		public AttendanceController(IAttendanceService attendanceService)
		{
			_attendanceService = attendanceService;
		}

		private const string TeacherRoles = "SubjectTeacher,ClassTeacher,HeadTeacher,Administrator,SuperAdministrator";

		private IActionResult Map(BaseResponse response) => response?.ResponseCode switch
		{
			ResponseCode.successful => Ok(response),
			ResponseCode.BadRequest => BadRequest(response),
			ResponseCode.NotFound => NotFound(response),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
			ResponseCode.Unauthorized => Unauthorized(response),
			ResponseCode.Conflict => Conflict(response),
			_ => StatusCode(StatusCodes.Status500InternalServerError, response)
		};

		/// <summary>Open an attendance session (Class / Subject / SubTopic).</summary>
		[HttpPost("session/start")]
		[Authorize(Roles = TeacherRoles)]
		public async Task<IActionResult> StartSession([FromBody] StartAttendanceSessionViewModel model)
		{
			return Map(await _attendanceService.StartSessionAsync(model, User.GetAuthenticatedUserClaims()));
		}

		/// <summary>Close an open attendance session.</summary>
		[HttpPost("session/{sessionId:guid}/end")]
		[Authorize(Roles = TeacherRoles)]
		public async Task<IActionResult> EndSession(Guid sessionId)
		{
			return Map(await _attendanceService.EndSessionAsync(sessionId, User.GetAuthenticatedUserClaims()));
		}

		/// <summary>Register a student as present by scanning their QR code.</summary>
		[HttpPost("session/{sessionId:guid}/scan")]
		[Authorize(Roles = TeacherRoles)]
		public async Task<IActionResult> ScanStudent(Guid sessionId, [FromBody] ScanQrCodeViewModel model)
		{
			return Map(await _attendanceService.ScanStudentAsync(sessionId, model?.QrToken, User.GetAuthenticatedUserClaims()));
		}

		/// <summary>Get a session with its recorded attendance.</summary>
		[HttpGet("session/{sessionId:guid}")]
		public async Task<IActionResult> GetSession(Guid sessionId)
		{
			return Map(await _attendanceService.GetSessionAsync(sessionId, User.GetAuthenticatedUserClaims()));
		}

		/// <summary>Get present/absent breakdown against the expected roster.</summary>
		[HttpGet("session/{sessionId:guid}/summary")]
		[Authorize(Roles = TeacherRoles)]
		public async Task<IActionResult> GetSessionSummary(Guid sessionId)
		{
			return Map(await _attendanceService.GetSessionSummaryAsync(sessionId, User.GetAuthenticatedUserClaims()));
		}

		/// <summary>List the caller's own attendance sessions (optional type filter + pagination).</summary>
		[HttpGet("sessions")]
		[Authorize(Roles = TeacherRoles)]
		public async Task<IActionResult> GetSessions([FromQuery] int? attendanceType, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
		{
			return Map(await _attendanceService.GetSessionsAsync(User.GetAuthenticatedUserClaims(), attendanceType, pageNumber, pageSize));
		}

		/// <summary>Return a student's printable QR PNG. Staff in the school or the student themself.</summary>
		[HttpGet("student/{studentId:guid}/qrcode")]
		public async Task<IActionResult> GetStudentQrCode(Guid studentId)
		{
			var result = await _attendanceService.GetStudentQrCodeAsync(studentId, User.GetAuthenticatedUserClaims());
			if (result.Success)
				return File(result.Bytes, "image/png", result.FileName);

			return Map(new BaseResponse
			{
				ResponseCode = result.ResponseCode,
				ResponseMessage = result.ResponseMessage,
				Status = "failed"
			});
		}

		/// <summary>Return a student's QR token as JSON (part of their profile).</summary>
		[HttpGet("student/{studentId:guid}/qr-token")]
		public async Task<IActionResult> GetStudentQrToken(Guid studentId)
		{
			return Map(await _attendanceService.GetStudentQrTokenAsync(studentId, User.GetAuthenticatedUserClaims()));
		}

		/// <summary>Current student's own QR token (profile convenience endpoint).</summary>
		[HttpGet("student/me/qr-token")]
		public async Task<IActionResult> GetMyQrToken()
		{
			var claims = User.GetAuthenticatedUserClaims();
			if (!System.Guid.TryParse(claims?.UserId, out var studentId))
				return Map(new BaseResponse { ResponseCode = ResponseCode.Unauthorized, ResponseMessage = "Invalid user context", Status = "failed" });

			return Map(await _attendanceService.GetStudentQrTokenAsync(studentId, claims));
		}

		/// <summary>Current student's own QR PNG.</summary>
		[HttpGet("student/me/qrcode")]
		public async Task<IActionResult> GetMyQrCode()
		{
			var claims = User.GetAuthenticatedUserClaims();
			if (!System.Guid.TryParse(claims?.UserId, out var studentId))
				return Map(new BaseResponse { ResponseCode = ResponseCode.Unauthorized, ResponseMessage = "Invalid user context", Status = "failed" });

			var result = await _attendanceService.GetStudentQrCodeAsync(studentId, claims);
			if (result.Success)
				return File(result.Bytes, "image/png", result.FileName);

			return Map(new BaseResponse
			{
				ResponseCode = result.ResponseCode,
				ResponseMessage = result.ResponseMessage,
				Status = "failed"
			});
		}

		/// <summary>Get an admin attendance analytics report per class / per subject for the selected period.</summary>
		[HttpGet("analytics")]
		[Authorize(Roles = "HeadTeacher,Administrator,SuperAdministrator")]
		public async Task<IActionResult> GetAnalytics(
			[FromQuery] int period,
			[FromQuery] string? date,
			[FromQuery] string? month,
			[FromQuery] string? fromMonth,
			[FromQuery] string? toMonth,
			[FromQuery] int? attendanceType,
			[FromQuery] Guid? classroomId,
			[FromQuery] Guid? subjectId)
		{
			return Map(await _attendanceService.GetAttendanceAnalyticsAsync(
				User.GetAuthenticatedUserClaims(), period, date, month, fromMonth, toMonth, attendanceType, classroomId, subjectId));
		}

		/// <summary>List students who were absent in a class or subject over the selected period.</summary>
		[HttpGet("absent-students")]
		[Authorize(Roles = "HeadTeacher,Administrator,SuperAdministrator")]
		public async Task<IActionResult> GetAbsentStudents(
			[FromQuery] int attendanceType,
			[FromQuery] Guid? classroomId,
			[FromQuery] Guid? subjectId,
			[FromQuery] int period,
			[FromQuery] string? date,
			[FromQuery] string? month,
			[FromQuery] string? fromMonth,
			[FromQuery] string? toMonth)
		{
			return Map(await _attendanceService.GetAbsentStudentsAsync(
				User.GetAuthenticatedUserClaims(), attendanceType, classroomId, subjectId, period, date, month, fromMonth, toMonth));
		}

		/// <summary>Get a student's attendance stats for a class or subject over the selected period.</summary>
		[HttpGet("student/{studentId:guid}/stats")]
		[Authorize(Roles = "SubjectTeacher,ClassTeacher,HeadTeacher,Administrator,SuperAdministrator,Parent")]
		public async Task<IActionResult> GetStudentStats(
			Guid studentId,
			[FromQuery] int attendanceType,
			[FromQuery] Guid? classroomId,
			[FromQuery] Guid? subjectId,
			[FromQuery] int period,
			[FromQuery] string? date,
			[FromQuery] string? month,
			[FromQuery] string? fromMonth,
			[FromQuery] string? toMonth)
		{
			return Map(await _attendanceService.GetStudentAttendanceStatsAsync(
				User.GetAuthenticatedUserClaims(), studentId, attendanceType, classroomId, subjectId, period, date, month, fromMonth, toMonth));
		}

		/// <summary>A student's attendance history (self or staff).</summary>
		[HttpGet("student/{studentId:guid}/attendance")]
		public async Task<IActionResult> GetStudentAttendance(Guid studentId)
		{
			return Map(await _attendanceService.GetStudentAttendanceAsync(studentId, User.GetAuthenticatedUserClaims()));
		}
	}
}
