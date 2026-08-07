using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Attendance;
using TechHub.Service.Interface;

namespace TechHub.Service.Service
{
	public class AttendanceService : IAttendanceService
	{
		private readonly IQueryRepository<Users> _userQueryRepo;
		private readonly ICommandRespository<Users> _userCommandRepo;
		private readonly IQueryRepository<AttendanceSession> _sessionQueryRepo;
		private readonly ICommandRespository<AttendanceSession> _sessionCommandRepo;
		private readonly IQueryRepository<AttendanceRecord> _recordQueryRepo;
		private readonly ICommandRespository<AttendanceRecord> _recordCommandRepo;
		private readonly IQueryRepository<StudentClassroom> _studentClassroomQueryRepo;
		private readonly IQueryRepository<ClassroomTeacher> _classroomTeacherQueryRepo;
		private readonly IQueryRepository<TeacherSubject> _teacherSubjectQueryRepo;
		private readonly IQueryRepository<Classroom> _classroomQueryRepo;
		private readonly IQueryRepository<Subjects> _subjectQueryRepo;
		private readonly IQueryRepository<Topic> _topicQueryRepo;
		private readonly IQueryRepository<SubTopic> _subTopicQueryRepo;
		private readonly ILogger _logger;

		public AttendanceService(
			IQueryRepository<Users> userQueryRepo,
			ICommandRespository<Users> userCommandRepo,
			IQueryRepository<AttendanceSession> sessionQueryRepo,
			ICommandRespository<AttendanceSession> sessionCommandRepo,
			IQueryRepository<AttendanceRecord> recordQueryRepo,
			ICommandRespository<AttendanceRecord> recordCommandRepo,
			IQueryRepository<StudentClassroom> studentClassroomQueryRepo,
			IQueryRepository<ClassroomTeacher> classroomTeacherQueryRepo,
			IQueryRepository<TeacherSubject> teacherSubjectQueryRepo,
			IQueryRepository<Classroom> classroomQueryRepo,
			IQueryRepository<Subjects> subjectQueryRepo,
			IQueryRepository<Topic> topicQueryRepo,
			IQueryRepository<SubTopic> subTopicQueryRepo,
			ILogger logger)
		{
			_userQueryRepo = userQueryRepo;
			_userCommandRepo = userCommandRepo;
			_sessionQueryRepo = sessionQueryRepo;
			_sessionCommandRepo = sessionCommandRepo;
			_recordQueryRepo = recordQueryRepo;
			_recordCommandRepo = recordCommandRepo;
			_studentClassroomQueryRepo = studentClassroomQueryRepo;
			_classroomTeacherQueryRepo = classroomTeacherQueryRepo;
			_teacherSubjectQueryRepo = teacherSubjectQueryRepo;
			_classroomQueryRepo = classroomQueryRepo;
			_subjectQueryRepo = subjectQueryRepo;
			_topicQueryRepo = topicQueryRepo;
			_subTopicQueryRepo = subTopicQueryRepo;
			_logger = logger;
		}

		#region QR Codes

		public async Task<BaseResponse> GetStudentQrTokenAsync(Guid studentId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");

					var student = await _userQueryRepo.Get(studentId);
					if (student is null || student.SchoolId != schoolId || !student.IsActive)
						return Fail(ResponseCode.NotFound, "Student not found in this school");

					if (!CanAccessStudentQr(claims, studentId))
						return Fail(ResponseCode.Forbidden, "You can only view your own QR code");

					var token = await EnsureQrTokenAsync(student);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "QR token retrieved",
						Status = "successful",
						Data = new { studentId = student.Id, qrToken = token }
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Attendance: error getting QR token for student {StudentId}", studentId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while retrieving the QR code");
				}
			}
		}

		public async Task<StudentQrCodeResult> GetStudentQrCodeAsync(Guid studentId, AuthenticatedUserClaims claims)
		{
			try
			{
				var schoolId = ParseSchoolId(claims);
				if (schoolId == Guid.Empty)
					return new StudentQrCodeResult { Success = false, ResponseCode = ResponseCode.Unauthorized, ResponseMessage = "Invalid school context" };

				var student = await _userQueryRepo.Get(studentId);
				if (student is null || student.SchoolId != schoolId || !student.IsActive)
					return new StudentQrCodeResult { Success = false, ResponseCode = ResponseCode.NotFound, ResponseMessage = "Student not found in this school" };

				if (!CanAccessStudentQr(claims, studentId))
					return new StudentQrCodeResult { Success = false, ResponseCode = ResponseCode.Forbidden, ResponseMessage = "You can only view your own QR code" };

				var token = await EnsureQrTokenAsync(student);
				var bytes = GenerateQrPng(token);

				return new StudentQrCodeResult
				{
					Success = true,
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "QR code generated",
					Bytes = bytes,
					FileName = $"student-{student.Id:N}-qr.png"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Attendance: error generating QR image for student {StudentId}", studentId);
				return new StudentQrCodeResult { Success = false, ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = "An error occurred while generating the QR code" };
			}
		}

		#endregion

		#region Sessions

		public async Task<BaseResponse> StartSessionAsync(StartAttendanceSessionViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					var teacherId = ParseUserId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");
					if (teacherId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid user context");

					if (IsStudentRole(claims?.Role))
						return Fail(ResponseCode.Forbidden, "Students cannot take attendance");

					if (model is null)
						return Fail(ResponseCode.BadRequest, "Request body is required");

					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
					Guid? classroomId = null;
					Guid? subjectId = null;
					Guid? subTopicId = null;

					switch ((AttendanceType)model.AttendanceType)
					{
						case AttendanceType.Class:
							if (!model.ClassroomId.HasValue || model.ClassroomId == Guid.Empty)
								return Fail(ResponseCode.BadRequest, "ClassroomId is required for class attendance");
							if (!await ClassroomExistsAsync(model.ClassroomId.Value, schoolId))
								return Fail(ResponseCode.NotFound, "Classroom not found");
							if (!await CanManageClassroomAsync(teacherId, model.ClassroomId.Value, schoolId, claims?.Role))
								return Fail(ResponseCode.Forbidden, "You are not assigned to this classroom");
							classroomId = model.ClassroomId;
							break;

						case AttendanceType.Subject:
							if (!model.SubjectId.HasValue || model.SubjectId == Guid.Empty)
								return Fail(ResponseCode.BadRequest, "SubjectId is required for subject attendance");
							if (!await SubjectExistsAsync(model.SubjectId.Value, schoolId))
								return Fail(ResponseCode.NotFound, "Subject not found");
							if (!await CanTeachSubjectAsync(teacherId, model.SubjectId.Value, schoolId, claims?.Role))
								return Fail(ResponseCode.Forbidden, "You are not assigned to this subject");
							subjectId = model.SubjectId;
							if (model.ClassroomId.HasValue && model.ClassroomId != Guid.Empty)
							{
								if (!await ClassroomExistsAsync(model.ClassroomId.Value, schoolId))
									return Fail(ResponseCode.NotFound, "Classroom not found");
								classroomId = model.ClassroomId;
							}
							break;

						case AttendanceType.SubTopic:
							if (!model.SubTopicId.HasValue || model.SubTopicId == Guid.Empty)
								return Fail(ResponseCode.BadRequest, "SubTopicId is required for sub-topic attendance");
							var subTopic = await _subTopicQueryRepo.Get(model.SubTopicId.Value);
							if (subTopic is null || subTopic.SchoolId != schoolId)
								return Fail(ResponseCode.NotFound, "Sub-topic not found");
							var topic = await _topicQueryRepo.Get(subTopic.TopicId);
							if (topic is null || topic.SchoolId != schoolId)
								return Fail(ResponseCode.NotFound, "Topic not found");
							if (!await CanTeachSubjectAsync(teacherId, topic.SubjectId, schoolId, claims?.Role))
								return Fail(ResponseCode.Forbidden, "You are not assigned to this subject");
							subTopicId = subTopic.Id;
							subjectId = topic.SubjectId;
							classroomId = (model.ClassroomId.HasValue && model.ClassroomId != Guid.Empty)
								? model.ClassroomId
								: (subTopic.ClassroomId != Guid.Empty ? subTopic.ClassroomId : topic.ClassroomId);
							break;

						default:
							return Fail(ResponseCode.BadRequest, "Invalid attendance type");
					}

					var sessionId = Guid.NewGuid();
					var sessionDict = new Dictionary<string, object>
					{
						{ "Id", sessionId },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "SchoolId", schoolId },
						{ "TeacherId", teacherId },
						{ "AttendanceType", model.AttendanceType },
						{ "ClassroomId", classroomId ?? (object)DBNull.Value },
						{ "SubjectId", subjectId ?? (object)DBNull.Value },
						{ "SubTopicId", subTopicId ?? (object)DBNull.Value },
						{ "ClassPreparationId", (model.ClassPreparationId.HasValue && model.ClassPreparationId != Guid.Empty) ? (object)model.ClassPreparationId.Value : DBNull.Value },
						{ "Status", (int)AttendanceSessionStatus.Open },
						{ "StartedAt", now },
						{ "EndedAt", DBNull.Value },
						{ "CreatedBy", teacherId },
						{ "IsActive", true }
					};

					await _sessionCommandRepo.Create(sessionDict);

					_logger.Information("Attendance: session {SessionId} started by teacher {TeacherId} (type {Type})", sessionId, teacherId, model.AttendanceType);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Attendance session started. Scan student QR codes to register attendance.",
						Status = "successful",
						Data = await BuildSessionDtoAsync(sessionId, schoolId)
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Attendance: error starting session");
					return Fail(ResponseCode.ErrorOccured, "An error occurred while starting the attendance session");
				}
			}
		}

		public async Task<BaseResponse> EndSessionAsync(Guid sessionId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					var teacherId = ParseUserId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");
					if (teacherId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid user context");

					var session = await _sessionQueryRepo.Get(sessionId);
					if (session is null || session.SchoolId != schoolId || !session.IsActive)
						return Fail(ResponseCode.NotFound, "Session not found");

					if (session.TeacherId != teacherId && !IsAdminRole(claims?.Role))
						return Fail(ResponseCode.Forbidden, "You can only end your own sessions");

					if (session.Status != (int)AttendanceSessionStatus.Open)
						return Fail(ResponseCode.BadRequest, "This session is already closed");

					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
					await _sessionCommandRepo.UpdateTableColumnById(
						new Dictionary<string, object>
						{
							{ "Status", (int)AttendanceSessionStatus.Closed },
							{ "EndedAt", now },
							{ "ModifiedDate", now }
						},
						new KeyValuePair<string, object>("Id", session.Id));

					_logger.Information("Attendance: session {SessionId} closed by teacher {TeacherId}", sessionId, teacherId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Attendance session ended",
						Status = "successful",
						Data = await BuildSessionDtoAsync(session.Id, schoolId)
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Attendance: error ending session {SessionId}", sessionId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while ending the attendance session");
				}
			}
		}

		public async Task<BaseResponse> ScanStudentAsync(Guid sessionId, string qrToken, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					if (string.IsNullOrWhiteSpace(qrToken))
						return Fail(ResponseCode.BadRequest, "QR token is required");

					var schoolId = ParseSchoolId(claims);
					var callerId = ParseUserId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");
					if (callerId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid user context");

					var session = await _sessionQueryRepo.Get(sessionId);
					if (session is null || session.SchoolId != schoolId || !session.IsActive)
						return Fail(ResponseCode.NotFound, "Session not found");

					if (session.Status != (int)AttendanceSessionStatus.Open)
						return Fail(ResponseCode.BadRequest, "This session is not open for scanning");

					if (session.TeacherId != callerId && !IsAdminRole(claims?.Role))
						return Fail(ResponseCode.Forbidden, "You are not authorized to scan for this session");

					var student = await _userQueryRepo.GetByPropertyName("QrCodeToken", qrToken);
					if (student is null || student.SchoolId != schoolId || !student.IsActive || student.RoleId != (int)UserRole.Student)
						return Fail(ResponseCode.BadRequest, "Invalid QR code");

					if (!await IsStudentEligibleForSessionAsync(student.Id, session, schoolId))
						return Fail(ResponseCode.BadRequest, "Student is not part of this class or subject");

					var existing = await _recordQueryRepo.SelectByColumns(
						"SELECT * FROM AttendanceRecord WHERE SessionId = @SessionId AND StudentId = @StudentId",
						new Dictionary<string, object> { { "SessionId", sessionId }, { "StudentId", student.Id } });

					if (existing is not null)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.successful,
							ResponseMessage = "Student already marked present",
							Status = "successful",
							Data = await BuildRecordDtoAsync(existing.Id, sessionId, schoolId)
						};
					}

					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
					var recordId = Guid.NewGuid();
					var recordDict = new Dictionary<string, object>
					{
						{ "Id", recordId },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "SessionId", sessionId },
						{ "StudentId", student.Id },
						{ "SchoolId", schoolId },
						{ "IsPresent", true },
						{ "IsManual", false },
						{ "AttendedAt", now },
						{ "CreatedBy", callerId },
						{ "IsActive", true }
					};

					await _recordCommandRepo.Create(recordDict);

					_logger.Information("Attendance: student {StudentId} scanned into session {SessionId}", student.Id, sessionId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Attendance registered",
						Status = "successful",
						Data = await BuildRecordDtoAsync(recordId, sessionId, schoolId)
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Attendance: error scanning student for session {SessionId}", sessionId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while registering attendance");
				}
			}
		}

		public async Task<BaseResponse> GetSessionAsync(Guid sessionId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");

					var session = await _sessionQueryRepo.Get(sessionId);
					if (session is null || session.SchoolId != schoolId || !session.IsActive)
						return Fail(ResponseCode.NotFound, "Session not found");

					if (IsStudentRole(claims?.Role))
						return Fail(ResponseCode.Forbidden, "Students cannot view attendance sessions");

					var dto = await BuildSessionDtoAsync(session.Id, schoolId) as AttendanceSessionDto;
					var records = (await _recordQueryRepo.QueryAsync<AttendanceRecordDto>(
						@"SELECT r.Id, r.SessionId, r.StudentId, r.IsPresent, r.IsManual, r.AttendedAt,
						         (u.FirstName + ' ' + u.LastName) AS StudentName
						  FROM   AttendanceRecord r
						  JOIN   Users u ON u.Id = r.StudentId
						  WHERE  r.SessionId = @SessionId AND r.SchoolId = @SchoolId AND r.IsActive = 1
						  ORDER  BY r.AttendedAt DESC",
						new Dictionary<string, object> { { "SessionId", sessionId }, { "SchoolId", schoolId } })).ToList();

					var detail = new AttendanceSessionDetailDto
					{
						Id = dto?.Id ?? session.Id,
						TeacherId = dto?.TeacherId ?? session.TeacherId,
						TeacherName = dto?.TeacherName ?? string.Empty,
						AttendanceType = dto?.AttendanceType ?? session.AttendanceType,
						AttendanceTypeName = dto?.AttendanceTypeName ?? string.Empty,
						ClassroomId = dto?.ClassroomId,
						ClassroomName = dto?.ClassroomName,
						SubjectId = dto?.SubjectId,
						SubjectName = dto?.SubjectName,
						SubTopicId = dto?.SubTopicId,
						SubTopicName = dto?.SubTopicName,
						Status = dto?.Status ?? session.Status,
						StatusName = dto?.StatusName ?? string.Empty,
						StartedAt = dto?.StartedAt ?? session.StartedAt,
						EndedAt = dto?.EndedAt,
						PresentCount = dto?.PresentCount ?? 0,
						Records = records
					};

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = records.Count > 0 ? $"{records.Count} attendance record(s) found" : "No attendance recorded yet",
						Status = "successful",
						Data = detail
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Attendance: error getting session {SessionId}", sessionId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while retrieving the session");
				}
			}
		}

		public async Task<BaseResponse> GetSessionsAsync(AuthenticatedUserClaims claims, int? attendanceType, int pageNumber, int pageSize)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					var teacherId = ParseUserId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");
					if (teacherId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid user context");

					if (IsStudentRole(claims?.Role))
						return Fail(ResponseCode.Forbidden, "Students cannot view attendance sessions");

					pageNumber = pageNumber < 1 ? 1 : pageNumber;
					pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

					var parameters = new Dictionary<string, object>
					{
						{ "SchoolId", schoolId },
						{ "TeacherId", teacherId }
					};
					var where = "WHERE SchoolId = @SchoolId AND TeacherId = @TeacherId AND IsActive = 1";
					if (attendanceType.HasValue)
					{
						where += " AND AttendanceType = @AttendanceType";
						parameters["AttendanceType"] = attendanceType.Value;
					}

					var totalCount = await _sessionQueryRepo.CountAsync($"SELECT COUNT(*) FROM AttendanceSession {where}", parameters);

					var pageParameters = new Dictionary<string, object>(parameters)
					{
						{ "Offset", (pageNumber - 1) * pageSize },
						{ "PageSize", pageSize }
					};
					var rows = (await _sessionQueryRepo.QueryAsync<AttendanceSession>(
						$"SELECT * FROM AttendanceSession {where} ORDER BY StartedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
						pageParameters)).ToList();

					var sessions = new List<AttendanceSessionDto>();
					foreach (var row in rows)
					{
						var dto = await BuildSessionDtoAsync(row.Id, schoolId);
						if (dto is AttendanceSessionDto s) sessions.Add(s);
					}

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = sessions.Count > 0 ? $"{sessions.Count} session(s) found" : "No attendance sessions found",
						Status = "successful",
						Data = new
						{
							sessions,
							totalCount,
							pageNumber,
							pageSize
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Attendance: error listing sessions");
					return Fail(ResponseCode.ErrorOccured, "An error occurred while listing attendance sessions");
				}
			}
		}

		public async Task<BaseResponse> GetSessionSummaryAsync(Guid sessionId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");

					var session = await _sessionQueryRepo.Get(sessionId);
					if (session is null || session.SchoolId != schoolId || !session.IsActive)
						return Fail(ResponseCode.NotFound, "Session not found");

					if (IsStudentRole(claims?.Role))
						return Fail(ResponseCode.Forbidden, "Students cannot view attendance summaries");

					var present = (await _recordQueryRepo.QueryAsync<AttendanceStudentDto>(
						@"SELECT u.Id AS StudentId, (u.FirstName + ' ' + u.LastName) AS StudentName
						  FROM   AttendanceRecord r
						  JOIN   Users u ON u.Id = r.StudentId
						  WHERE  r.SessionId = @SessionId AND r.SchoolId = @SchoolId AND r.IsPresent = 1 AND r.IsActive = 1
						  ORDER  BY u.FirstName ASC",
						new Dictionary<string, object> { { "SessionId", sessionId }, { "SchoolId", schoolId } })).ToList();

					var roster = await GetRosterAsync(session, schoolId);
					var presentIds = present.Select(p => p.StudentId).ToHashSet();
					var absent = roster.Where(r => !presentIds.Contains(r.StudentId)).ToList();

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Attendance summary retrieved",
						Status = "successful",
						Data = new AttendanceSummaryDto
						{
							SessionId = session.Id,
							RosterCount = roster.Count,
							PresentCount = present.Count,
							AbsentCount = absent.Count,
							Present = present,
							Absent = absent
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Attendance: error getting summary for session {SessionId}", sessionId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while retrieving the attendance summary");
				}
			}
		}

		public async Task<BaseResponse> GetStudentAttendanceAsync(Guid studentId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");

					var student = await _userQueryRepo.Get(studentId);
					if (student is null || student.SchoolId != schoolId || !student.IsActive)
						return Fail(ResponseCode.NotFound, "Student not found in this school");

					bool isSelf = Guid.TryParse(claims?.UserId, out var callerId) && callerId == studentId;
					if (!isSelf && IsStudentRole(claims?.Role))
						return Fail(ResponseCode.Forbidden, "You can only view your own attendance");

					var rows = (await _recordQueryRepo.QueryAsync<StudentAttendanceHistoryDto>(
						@"SELECT r.SessionId, r.IsPresent, r.AttendedAt, s.AttendanceType, s.StartedAt, s.[Status] AS SessionStatus,
						         sub.Name AS SubTopicName, sj.Subject AS SubjectName, c.Name AS ClassroomName
						  FROM   AttendanceRecord r
						  JOIN   AttendanceSession s ON s.Id = r.SessionId
						  LEFT JOIN SubTopic sub ON sub.Id = s.SubTopicId
						  LEFT JOIN Subjects  sj  ON sj.Id  = s.SubjectId
						  LEFT JOIN Classroom c   ON c.Id   = s.ClassroomId
						  WHERE  r.StudentId = @StudentId AND r.SchoolId = @SchoolId AND r.IsActive = 1 AND s.IsActive = 1
						  ORDER  BY r.AttendedAt DESC",
						new Dictionary<string, object> { { "StudentId", studentId }, { "SchoolId", schoolId } })).ToList();

					foreach (var row in rows)
						row.AttendanceTypeName = ((AttendanceType)row.AttendanceType).ToString();

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = rows.Count > 0 ? $"{rows.Count} attendance record(s) found" : "No attendance records found",
						Status = "successful",
						Data = rows
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Attendance: error getting history for student {StudentId}", studentId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while retrieving the attendance history");
				}
			}
		}

		#endregion

		#region Private helpers

		private static Guid ParseSchoolId(AuthenticatedUserClaims claims)
			=> Guid.TryParse(claims?.SchoolId, out var id) ? id : Guid.Empty;

		private static Guid ParseUserId(AuthenticatedUserClaims claims)
			=> Guid.TryParse(claims?.UserId, out var id) ? id : Guid.Empty;

		private static bool IsStudentRole(string role)
			=> string.Equals(role, nameof(UserRole.Student), StringComparison.OrdinalIgnoreCase);

		private static bool IsAdminRole(string role)
			=> role is "Administrator" or "SuperAdministrator" or "HeadTeacher";

		private static bool IsStaffRole(string role)
			=> !IsStudentRole(role);

		private static BaseResponse Fail(string code, string message)
			=> new BaseResponse { ResponseCode = code, ResponseMessage = message, Status = "failed" };

		/// <summary>
		/// Students can see their own QR; staff can see any student's QR in their school.
		/// </summary>
		private static bool CanAccessStudentQr(AuthenticatedUserClaims claims, Guid studentId)
		{
			if (IsStaffRole(claims?.Role))
				return true;
			return Guid.TryParse(claims?.UserId, out var callerId) && callerId == studentId;
		}

		private async Task<string> EnsureQrTokenAsync(Users student)
		{
			if (!string.IsNullOrWhiteSpace(student.QrCodeToken))
				return student.QrCodeToken;

			var token = GenerateQrToken();
			await _userCommandRepo.UpdateTableColumnById("QrCodeToken", "Id", token, student.Id);
			return token;
		}

		private static string GenerateQrToken()
		{
			var bytes = RandomNumberGenerator.GetBytes(16);
			return Convert.ToHexString(bytes);
		}

		private static byte[] GenerateQrPng(string content)
		{
			using var generator = new QRCoder.QRCodeGenerator();
			using var qrData = generator.CreateQrCode(content, QRCoder.QRCodeGenerator.ECCLevel.Q);
			using var qrCode = new QRCoder.PngByteQRCode(qrData);
			return qrCode.GetGraphic(20);
		}

		private async Task<bool> ClassroomExistsAsync(Guid classroomId, Guid schoolId)
		{
			var classroom = await _classroomQueryRepo.Get(classroomId);
			return classroom is not null && classroom.SchoolId == schoolId && classroom.IsActive;
		}

		private async Task<bool> SubjectExistsAsync(Guid subjectId, Guid schoolId)
		{
			var subject = await _subjectQueryRepo.Get(subjectId);
			return subject is not null && subject.SchoolId == schoolId && subject.IsActive;
		}

		private async Task<bool> CanManageClassroomAsync(Guid teacherId, Guid classroomId, Guid schoolId, string role)
		{
			if (IsAdminRole(role))
				return true;

			return await _classroomTeacherQueryRepo.CountAsync(
				"SELECT TOP 1 1 FROM ClassroomTeacher WHERE TeacherId = @TeacherId AND ClassroomId = @ClassroomId AND SchoolId = @SchoolId AND IsActive = 1",
				new Dictionary<string, object>
				{
					{ "TeacherId", teacherId },
					{ "ClassroomId", classroomId },
					{ "SchoolId", schoolId }
				}) > 0;
		}

		private async Task<bool> CanTeachSubjectAsync(Guid teacherId, Guid subjectId, Guid schoolId, string role)
		{
			if (IsAdminRole(role))
				return true;

			return await _teacherSubjectQueryRepo.CountAsync(
				"SELECT TOP 1 1 FROM TeacherSubject WHERE TeacherId = @TeacherId AND SubjectId = @SubjectId AND SchoolId = @SchoolId AND IsActive = 1",
				new Dictionary<string, object>
				{
					{ "TeacherId", teacherId },
					{ "SubjectId", subjectId },
					{ "SchoolId", schoolId }
				}) > 0;
		}

		private async Task<bool> IsStudentEligibleForSessionAsync(Guid studentId, AttendanceSession session, Guid schoolId)
		{
			switch ((AttendanceType)session.AttendanceType)
			{
				case AttendanceType.Class:
					return session.ClassroomId.HasValue
						&& await IsStudentInClassroomAsync(studentId, session.ClassroomId.Value, schoolId);

				case AttendanceType.Subject:
					if (session.ClassroomId.HasValue)
						return await IsStudentInClassroomAsync(studentId, session.ClassroomId.Value, schoolId);
					return session.SubjectId.HasValue
						&& await IsStudentInSubjectAsync(studentId, session.SubjectId.Value, schoolId);

				case AttendanceType.SubTopic:
					if (session.ClassroomId.HasValue)
						return await IsStudentInClassroomAsync(studentId, session.ClassroomId.Value, schoolId);
					return session.SubjectId.HasValue
						&& await IsStudentInSubjectAsync(studentId, session.SubjectId.Value, schoolId);

				default:
					return false;
			}
		}

		private async Task<bool> IsStudentInClassroomAsync(Guid studentId, Guid classroomId, Guid schoolId)
		{
			return await _studentClassroomQueryRepo.CountAsync(
				"SELECT TOP 1 1 FROM StudentClassroom WHERE StudentId = @StudentId AND ClassroomId = @ClassroomId AND SchoolId = @SchoolId AND IsActive = 1",
				new Dictionary<string, object>
				{
					{ "StudentId", studentId },
					{ "ClassroomId", classroomId },
					{ "SchoolId", schoolId }
				}) > 0;
		}

		private async Task<bool> IsStudentInSubjectAsync(Guid studentId, Guid subjectId, Guid schoolId)
		{
			return await _studentClassroomQueryRepo.CountAsync(
				@"SELECT COUNT(*) FROM (
					SELECT sc.StudentId FROM StudentClassroom sc
					JOIN ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId
					WHERE cs.SubjectId = @SubjectId AND sc.StudentId = @StudentId AND sc.SchoolId = @SchoolId AND sc.IsActive = 1 AND cs.IsActive = 1
					UNION
					SELECT sms.StudentId FROM StudentMinorSubject sms
					WHERE sms.SubjectId = @SubjectId AND sms.StudentId = @StudentId AND sms.SchoolId = @SchoolId
				) t",
				new Dictionary<string, object>
				{
					{ "StudentId", studentId },
					{ "SubjectId", subjectId },
					{ "SchoolId", schoolId }
				}) > 0;
		}

		private async Task<List<AttendanceStudentDto>> GetRosterAsync(AttendanceSession session, Guid schoolId)
		{
			var parameters = new Dictionary<string, object>
			{
				{ "ClassroomId", session.ClassroomId ?? Guid.Empty },
				{ "SubjectId", session.SubjectId ?? Guid.Empty },
				{ "SchoolId", schoolId }
			};

			string sql;
			switch ((AttendanceType)session.AttendanceType)
			{
				case AttendanceType.Class:
					sql = @"SELECT u.Id AS StudentId, (u.FirstName + ' ' + u.LastName) AS StudentName
							FROM   StudentClassroom sc
							JOIN   Users u ON u.Id = sc.StudentId
							WHERE  sc.ClassroomId = @ClassroomId AND sc.SchoolId = @SchoolId AND sc.IsActive = 1 AND u.IsActive = 1
							ORDER  BY u.FirstName ASC";
					break;

				case AttendanceType.Subject:
				case AttendanceType.SubTopic:
					if (session.ClassroomId.HasValue)
					{
						sql = @"SELECT u.Id AS StudentId, (u.FirstName + ' ' + u.LastName) AS StudentName
								FROM   StudentClassroom sc
								JOIN   Users u ON u.Id = sc.StudentId
								WHERE  sc.ClassroomId = @ClassroomId AND sc.SchoolId = @SchoolId AND sc.IsActive = 1 AND u.IsActive = 1
								ORDER  BY u.FirstName ASC";
					}
					else
					{
						sql = @"SELECT DISTINCT u.Id AS StudentId, (u.FirstName + ' ' + u.LastName) AS StudentName
								FROM (
									SELECT sc.StudentId FROM StudentClassroom sc
									JOIN ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId
									WHERE cs.SubjectId = @SubjectId AND sc.SchoolId = @SchoolId AND sc.IsActive = 1 AND cs.IsActive = 1
									UNION
									SELECT sms.StudentId FROM StudentMinorSubject sms
									WHERE sms.SubjectId = @SubjectId AND sms.SchoolId = @SchoolId
								) src
								JOIN Users u ON u.Id = src.StudentId
								WHERE u.IsActive = 1
								ORDER BY u.FirstName ASC";
					}
					break;

				default:
					return new List<AttendanceStudentDto>();
			}

			return (await _recordQueryRepo.QueryAsync<AttendanceStudentDto>(sql, parameters)).ToList();
		}

		private async Task<object> BuildSessionDtoAsync(Guid sessionId, Guid schoolId)
		{
			var session = await _sessionQueryRepo.Get(sessionId);
			if (session is null || session.SchoolId != schoolId)
				return null;

			var teacher = await _userQueryRepo.Get(session.TeacherId);
			var classroom = session.ClassroomId.HasValue ? await _classroomQueryRepo.Get(session.ClassroomId.Value) : null;
			var subject = session.SubjectId.HasValue ? await _subjectQueryRepo.Get(session.SubjectId.Value) : null;
			var subTopic = session.SubTopicId.HasValue ? await _subTopicQueryRepo.Get(session.SubTopicId.Value) : null;

			var presentCount = await _recordQueryRepo.CountAsync(
				"SELECT COUNT(*) FROM AttendanceRecord WHERE SessionId = @SessionId AND IsPresent = 1 AND IsActive = 1",
				new Dictionary<string, object> { { "SessionId", sessionId } });

			return new AttendanceSessionDto
			{
				Id = session.Id,
				TeacherId = session.TeacherId,
				TeacherName = teacher is null ? string.Empty : $"{teacher.FirstName} {teacher.LastName}".Trim(),
				AttendanceType = session.AttendanceType,
				AttendanceTypeName = ((AttendanceType)session.AttendanceType).ToString(),
				ClassroomId = session.ClassroomId,
				ClassroomName = classroom?.Name,
				SubjectId = session.SubjectId,
				SubjectName = subject?.Subject,
				SubTopicId = session.SubTopicId,
				SubTopicName = subTopic?.Name,
				Status = session.Status,
				StatusName = ((AttendanceSessionStatus)session.Status).ToString(),
				StartedAt = session.StartedAt,
				EndedAt = session.EndedAt,
				PresentCount = presentCount
			};
		}

		private async Task<object> BuildRecordDtoAsync(Guid recordId, Guid sessionId, Guid schoolId)
		{
			var record = await _recordQueryRepo.Get(recordId);
			if (record is null || record.SessionId != sessionId || record.SchoolId != schoolId)
				return null;

			var student = await _userQueryRepo.Get(record.StudentId);

			return new AttendanceRecordDto
			{
				Id = record.Id,
				SessionId = record.SessionId,
				StudentId = record.StudentId,
				StudentName = student is null ? string.Empty : $"{student.FirstName} {student.LastName}".Trim(),
				IsPresent = record.IsPresent,
				IsManual = record.IsManual,
				AttendedAt = record.AttendedAt
			};
		}

		#endregion
	}
}
