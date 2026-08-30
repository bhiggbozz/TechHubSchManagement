using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Globalization;
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
						Data = new { studentId = student.Id, studentName = $"{student.FirstName} {student.LastName}".Trim(), qrToken = token }
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
				var bytes = GenerateQrPng(BuildQrPayload(student, token));

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

					var student = await _userQueryRepo.GetByPropertyName("QrCodeToken", ResolveQrToken(qrToken));
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
							Data = await BuildRecordDtoAsync(existing.Id, sessionId, schoolId, alreadyMarked: true)
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

					if (string.Equals(claims?.Role, nameof(UserRole.Parent), StringComparison.OrdinalIgnoreCase))
					{
						if (!Guid.TryParse(claims?.UserId, out var parentId) ||
							!await IsParentOfStudentAsync(parentId, studentId, schoolId))
							return Fail(ResponseCode.Forbidden, "You can only view attendance for your own children");
					}

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

		public async Task<BaseResponse> GetAttendanceAnalyticsAsync(AuthenticatedUserClaims claims, int period, string? date, string? month, string? fromMonth, string? toMonth, int? attendanceType, Guid? classroomId, Guid? subjectId)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");

					if (!IsAdminRole(claims?.Role))
						return Fail(ResponseCode.Forbidden, "Only administrators can view attendance analytics");

					if (!Enum.IsDefined(typeof(AttendanceAnalyticsPeriod), period))
						return Fail(ResponseCode.BadRequest, "Invalid period. Use Daily(0), Weekly(1), Monthly(2) or MonthlyRange(3)");

					if (attendanceType.HasValue && !Enum.IsDefined(typeof(AttendanceType), attendanceType.Value))
						return Fail(ResponseCode.BadRequest, "Invalid attendance type");

					var buckets = BuildAnalyticsBuckets((AttendanceAnalyticsPeriod)period, date, month, fromMonth, toMonth);
					if (buckets.Count == 0)
						return Fail(ResponseCode.BadRequest, "Invalid date range. Provide date (yyyy-MM-dd) for daily/weekly, month (yyyy-MM) for monthly, or fromMonth/toMonth for a monthly range");

					var from = buckets[0].From;
					var to = buckets[^1].To;
					var bucketExpr = buckets.Count == 1
						? $"CASE WHEN s.StartedAt IS NOT NULL THEN N'{buckets[0].Label.Replace("'", "''")}' END"
						: "SUBSTRING(s.StartedAt, 1, 7)";

					var (classSql, classParams) = BuildClassAnalyticsSql(schoolId, bucketExpr, from, to, attendanceType, classroomId);
					var classRows = (await _sessionQueryRepo.QueryAsync<AnalyticsRow>(classSql, classParams)).ToList();
					var (subjectSql, subjectParams) = BuildSubjectAnalyticsSql(schoolId, bucketExpr, from, to, attendanceType, subjectId);
					var subjectRows = (await _sessionQueryRepo.QueryAsync<AnalyticsRow>(subjectSql, subjectParams)).ToList();
					var (totalsSql, totalsParams) = BuildTotalsSql(schoolId, bucketExpr, from, to, attendanceType, classroomId, subjectId);
					var totalsRows = (await _sessionQueryRepo.QueryAsync<AnalyticsTotalsRow>(totalsSql, totalsParams)).ToList();

					var analyticsBuckets = new List<AttendanceAnalyticsBucketDto>();
					var overall = new AttendanceAnalyticsTotalsDto();
					foreach (var bucket in buckets)
					{
						var bucketTotals = ToTotalsDto(totalsRows.FirstOrDefault(t => t.Bucket == bucket.Label));
						analyticsBuckets.Add(new AttendanceAnalyticsBucketDto
						{
							Label = bucket.Label,
							FromDate = bucket.From.Substring(0, 10),
							ToDate = bucket.To.Substring(0, 10),
							ByClass = classRows.Where(r => r.Bucket == bucket.Label).Select(MapAnalyticsRow).ToList(),
							BySubject = subjectRows.Where(r => r.Bucket == bucket.Label).Select(MapAnalyticsRow).ToList(),
							Totals = bucketTotals
						});
						overall.Sessions += bucketTotals.Sessions;
						overall.Expected += bucketTotals.Expected;
						overall.Present += bucketTotals.Present;
					}
					overall.Absent = Math.Max(0, overall.Expected - overall.Present);
					overall.AttendanceRate = overall.Expected > 0 ? Math.Round((decimal)overall.Present / overall.Expected * 100, 1) : 0;

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = overall.Sessions > 0 ? "Attendance analytics retrieved" : "No attendance sessions found in the selected period",
						Status = "successful",
						Data = new AttendanceAnalyticsDto
						{
							Period = ((AttendanceAnalyticsPeriod)period).ToString(),
							PeriodLabel = BuildPeriodLabel((AttendanceAnalyticsPeriod)period, buckets),
							FromDate = buckets[0].From.Substring(0, 10),
							ToDate = buckets[^1].To.Substring(0, 10),
							Buckets = analyticsBuckets,
							Totals = overall
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Attendance: error generating analytics for period {Period}", period);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while generating the attendance analytics");
				}
			}
		}

		#endregion

		#region Analytics reports

		public async Task<BaseResponse> GetAbsentStudentsAsync(AuthenticatedUserClaims claims, int attendanceType, Guid? classroomId, Guid? subjectId, int period, string? date, string? month, string? fromMonth, string? toMonth)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");

					if (!IsAdminRole(claims?.Role))
						return Fail(ResponseCode.Forbidden, "Only administrators can view attendance reports");

					if (attendanceType != (int)AttendanceType.Class && attendanceType != (int)AttendanceType.Subject)
						return Fail(ResponseCode.BadRequest, "Invalid attendance type. Use Class(0) or Subject(1)");

					if (!Enum.IsDefined(typeof(AttendanceAnalyticsPeriod), period))
						return Fail(ResponseCode.BadRequest, "Invalid period. Use Daily(0), Weekly(1), Monthly(2) or MonthlyRange(3)");

					var buckets = BuildAnalyticsBuckets((AttendanceAnalyticsPeriod)period, date, month, fromMonth, toMonth);
					if (buckets.Count == 0)
						return Fail(ResponseCode.BadRequest, "Invalid date range. Provide date (yyyy-MM-dd) for daily/weekly, month (yyyy-MM) for monthly, or fromMonth/toMonth for a monthly range");

					var from = buckets[0].From;
					var to = buckets[^1].To;

					Guid? entityId = null;
					string entityName = null;
					if (attendanceType == (int)AttendanceType.Class)
					{
						if (!classroomId.HasValue)
							return Fail(ResponseCode.BadRequest, "ClassroomId is required for class absence report");
						var classroom = await _classroomQueryRepo.Get(classroomId.Value);
						if (classroom is null || classroom.SchoolId != schoolId || !classroom.IsActive)
							return Fail(ResponseCode.NotFound, "Classroom not found");
						entityId = classroom.Id;
						entityName = classroom.Name;
					}
					else
					{
						if (!subjectId.HasValue)
							return Fail(ResponseCode.BadRequest, "SubjectId is required for subject absence report");
						var subject = await _subjectQueryRepo.Get(subjectId.Value);
						if (subject is null || subject.SchoolId != schoolId || !subject.IsActive)
							return Fail(ResponseCode.NotFound, "Subject not found");
						entityId = subject.Id;
						entityName = subject.Subject;
					}

					var (sql, parameters) = BuildAbsentStudentsSql(schoolId, attendanceType, entityId.Value, from, to);
					var students = (await _recordQueryRepo.QueryAsync<AttendanceStudentStatsDto>(sql, parameters)).ToList();
					foreach (var student in students)
						student.AttendanceRate = student.Sessions > 0 ? Math.Round((decimal)student.PresentCount / student.Sessions * 100, 1) : 0;

					var totals = new AttendanceStudentStatsTotalsDto
					{
						Students = students.Count,
						Sessions = students.Sum(s => s.Sessions),
						PresentCount = students.Sum(s => s.PresentCount),
						AbsentCount = students.Sum(s => s.AbsentCount)
					};
					totals.AttendanceRate = totals.Sessions > 0 ? Math.Round((decimal)totals.PresentCount / totals.Sessions * 100, 1) : 0;

					var totalStudents = await GetRosterCountAsync(schoolId, attendanceType, entityId.Value);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = students.Count > 0 ? $"{students.Count} student(s) absent" : "No absent students found",
						Status = "successful",
						Data = new AttendanceAbsentStudentsDto
						{
							AttendanceType = attendanceType,
							AttendanceTypeName = ((AttendanceType)attendanceType).ToString(),
							ClassroomId = attendanceType == (int)AttendanceType.Class ? entityId : null,
							ClassroomName = attendanceType == (int)AttendanceType.Class ? entityName : null,
							SubjectId = attendanceType == (int)AttendanceType.Subject ? entityId : null,
							SubjectName = attendanceType == (int)AttendanceType.Subject ? entityName : null,
							Period = ((AttendanceAnalyticsPeriod)period).ToString(),
							PeriodLabel = BuildPeriodLabel((AttendanceAnalyticsPeriod)period, buckets),
							FromDate = from.Substring(0, 10),
							ToDate = to.Substring(0, 10),
							TotalStudents = totalStudents,
							Sessions = totals.Sessions,
							Students = students,
							Totals = totals
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Attendance: error generating absent-students report for type {Type}", attendanceType);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while generating the absent students report");
				}
			}
		}

		public async Task<BaseResponse> GetStudentAttendanceStatsAsync(AuthenticatedUserClaims claims, Guid studentId, int attendanceType, Guid? classroomId, Guid? subjectId, int period, string? date, string? month, string? fromMonth, string? toMonth)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					var schoolId = ParseSchoolId(claims);
					if (schoolId == Guid.Empty)
						return Fail(ResponseCode.Unauthorized, "Invalid school context");

					if (attendanceType != (int)AttendanceType.Class && attendanceType != (int)AttendanceType.Subject)
						return Fail(ResponseCode.BadRequest, "Invalid attendance type. Use Class(0) or Subject(1)");

					if (!Enum.IsDefined(typeof(AttendanceAnalyticsPeriod), period))
						return Fail(ResponseCode.BadRequest, "Invalid period. Use Daily(0), Weekly(1), Monthly(2) or MonthlyRange(3)");

					var buckets = BuildAnalyticsBuckets((AttendanceAnalyticsPeriod)period, date, month, fromMonth, toMonth);
					if (buckets.Count == 0)
						return Fail(ResponseCode.BadRequest, "Invalid date range. Provide date (yyyy-MM-dd) for daily/weekly, month (yyyy-MM) for monthly, or fromMonth/toMonth for a monthly range");

					var student = await _userQueryRepo.Get(studentId);
					if (student is null || student.SchoolId != schoolId || !student.IsActive)
						return Fail(ResponseCode.NotFound, "Student not found in this school");

					var from = buckets[0].From;
					var to = buckets[^1].To;

					Guid? entityId = null;
					string entityName = null;
					if (attendanceType == (int)AttendanceType.Class)
					{
						if (!classroomId.HasValue)
							return Fail(ResponseCode.BadRequest, "ClassroomId is required for class attendance stats");
						var classroom = await _classroomQueryRepo.Get(classroomId.Value);
						if (classroom is null || classroom.SchoolId != schoolId || !classroom.IsActive)
							return Fail(ResponseCode.NotFound, "Classroom not found");
						entityId = classroom.Id;
						entityName = classroom.Name;
					}
					else
					{
						if (!subjectId.HasValue)
							return Fail(ResponseCode.BadRequest, "SubjectId is required for subject attendance stats");
						var subject = await _subjectQueryRepo.Get(subjectId.Value);
						if (subject is null || subject.SchoolId != schoolId || !subject.IsActive)
							return Fail(ResponseCode.NotFound, "Subject not found");
						entityId = subject.Id;
						entityName = subject.Subject;
					}

					// Parents are scoped to their own children; ClassTeacher/SubjectTeacher
					// are scoped to the class/subject they teach; HeadTeacher/
					// Administrator/SuperAdministrator bypass the check.
					if (string.Equals(claims?.Role, nameof(UserRole.Parent), StringComparison.OrdinalIgnoreCase))
					{
						if (!Guid.TryParse(claims?.UserId, out var parentId))
							return Fail(ResponseCode.Unauthorized, "Invalid user context");

						if (!await IsParentOfStudentAsync(parentId, studentId, schoolId))
							return Fail(ResponseCode.Forbidden, "You can only view attendance for your own children");
					}
					else if (!IsAdminRole(claims?.Role))
					{
						if (!Guid.TryParse(claims?.UserId, out var callerId))
							return Fail(ResponseCode.Unauthorized, "Invalid user context");

						if (attendanceType == (int)AttendanceType.Class)
						{
							if (!await CanManageClassroomAsync(callerId, classroomId.Value, schoolId, claims?.Role))
								return Fail(ResponseCode.Forbidden, "You can only view attendance for a class you teach");
						}
						else
						{
							if (!await CanTeachSubjectAsync(callerId, subjectId.Value, schoolId, claims?.Role))
								return Fail(ResponseCode.Forbidden, "You can only view attendance for a subject you teach");
						}
					}

					var (sql, parameters) = BuildStudentStatsSql(schoolId, studentId, attendanceType, entityId.Value, from, to);
					var row = (await _recordQueryRepo.QueryAsync<StudentStatsRow>(sql, parameters)).FirstOrDefault();
					var sessions = row?.Sessions ?? 0;
					var presentCount = row?.PresentCount ?? 0;
					var absentCount = row?.AbsentCount ?? 0;
					var rate = sessions > 0 ? Math.Round((decimal)presentCount / sessions * 100, 1) : 0;

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = sessions > 0 ? "Student attendance stats retrieved" : "No attendance sessions found for this student",
						Status = "successful",
						Data = new AttendanceStudentStatsResponseDto
						{
							StudentId = studentId,
							StudentName = $"{student.FirstName} {student.LastName}".Trim(),
							AttendanceType = attendanceType,
							AttendanceTypeName = ((AttendanceType)attendanceType).ToString(),
							ClassroomId = attendanceType == (int)AttendanceType.Class ? entityId : null,
							ClassroomName = attendanceType == (int)AttendanceType.Class ? entityName : null,
							SubjectId = attendanceType == (int)AttendanceType.Subject ? entityId : null,
							SubjectName = attendanceType == (int)AttendanceType.Subject ? entityName : null,
							Period = ((AttendanceAnalyticsPeriod)period).ToString(),
							PeriodLabel = BuildPeriodLabel((AttendanceAnalyticsPeriod)period, buckets),
							FromDate = from.Substring(0, 10),
							ToDate = to.Substring(0, 10),
							Sessions = sessions,
							PresentCount = presentCount,
							AbsentCount = absentCount,
							AttendanceRate = rate
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Attendance: error getting stats for student {StudentId}", studentId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while retrieving the student attendance stats");
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

		private async Task<bool> IsParentOfStudentAsync(Guid parentId, Guid studentId, Guid schoolId)
		{
			var count = await _userQueryRepo.CountAsync(
				"SELECT COUNT(*) FROM StudentParent WHERE ParentId = @ParentId AND StudentId = @StudentId AND SchoolId = @SchoolId AND IsActive = 1",
				new Dictionary<string, object> { { "ParentId", parentId }, { "StudentId", studentId }, { "SchoolId", schoolId } });
			return count > 0;
		}

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

		/// <summary>
		/// QR content sent to the frontend. Carries the student name so a scanner
		/// can display it, while the qrToken stays the lookup key for scanning.
		/// </summary>
		private static string BuildQrPayload(Users student, string token)
		{
			var name = $"{student.FirstName} {student.LastName}".Trim();
			return System.Text.Json.JsonSerializer.Serialize(new
			{
				qrToken = token,
				studentId = student.Id,
				studentName = name
			});
		}

		/// <summary>
		/// Extracts the raw qrToken from a scanned value. Accepts both the new JSON
		/// payload and the legacy plain token so already-printed QR codes keep working.
		/// </summary>
		private static string ResolveQrToken(string scannedValue)
		{
			if (string.IsNullOrWhiteSpace(scannedValue))
				return scannedValue;

			var trimmed = scannedValue.Trim();
			if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
			{
				try
				{
					using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
					if (doc.RootElement.TryGetProperty("qrToken", out var tokenProp) && tokenProp.ValueKind == System.Text.Json.JsonValueKind.String)
					{
						var token = tokenProp.GetString();
						if (!string.IsNullOrWhiteSpace(token))
							return token;
					}
				}
				catch (System.Text.Json.JsonException)
				{
					// Not JSON — fall back to the raw value below.
				}
			}

			return trimmed;
		}

		private static byte[] GenerateQrPng(string content)
		{
			using var generator = new QRCoder.QRCodeGenerator();
			using var qrData = generator.CreateQrCode(content, QRCoder.QRCodeGenerator.ECCLevel.Q);
			using var qrCode = new QRCoder.PngByteQRCode(qrData);
			return qrCode.GetGraphic(20);
		}

		private static List<(string Label, string From, string To)> BuildAnalyticsBuckets(AttendanceAnalyticsPeriod period, string? date, string? month, string? fromMonth, string? toMonth)
		{
			var format = CultureInfo.InvariantCulture;
			switch (period)
			{
				case AttendanceAnalyticsPeriod.Daily:
					if (string.IsNullOrWhiteSpace(date) || !DateTime.TryParseExact(date, "yyyy-MM-dd", format, DateTimeStyles.None, out var day))
						return new List<(string, string, string)>();
					return new List<(string, string, string)>
					{
						(day.ToString("yyyy-MM-dd"), day.ToString("yyyy-MM-dd 00:00:00"), day.ToString("yyyy-MM-dd 23:59:59"))
					};

				case AttendanceAnalyticsPeriod.Weekly:
					if (string.IsNullOrWhiteSpace(date) || !DateTime.TryParseExact(date, "yyyy-MM-dd", format, DateTimeStyles.None, out var weekDay))
						return new List<(string, string, string)>();
					var weekStart = weekDay.AddDays(-(((int)weekDay.DayOfWeek + 6) % 7));
					var weekEnd = weekStart.AddDays(6);
					return new List<(string, string, string)>
					{
						($"{weekStart:yyyy-MM-dd} - {weekEnd:yyyy-MM-dd}", weekStart.ToString("yyyy-MM-dd 00:00:00"), weekEnd.ToString("yyyy-MM-dd 23:59:59"))
					};

				case AttendanceAnalyticsPeriod.Monthly:
					if (string.IsNullOrWhiteSpace(month) || !DateTime.TryParseExact(month, "yyyy-MM", format, DateTimeStyles.None, out var m))
						return new List<(string, string, string)>();
					var first = new DateTime(m.Year, m.Month, 1);
					var last = first.AddMonths(1).AddDays(-1);
					return new List<(string, string, string)>
					{
						(m.ToString("yyyy-MM"), first.ToString("yyyy-MM-dd 00:00:00"), last.ToString("yyyy-MM-dd 23:59:59"))
					};

				case AttendanceAnalyticsPeriod.MonthlyRange:
					if (string.IsNullOrWhiteSpace(fromMonth) || string.IsNullOrWhiteSpace(toMonth)
						|| !DateTime.TryParseExact(fromMonth, "yyyy-MM", format, DateTimeStyles.None, out var from)
						|| !DateTime.TryParseExact(toMonth, "yyyy-MM", format, DateTimeStyles.None, out var to))
						return new List<(string, string, string)>();
					var bucketList = new List<(string, string, string)>();
					for (var cursor = new DateTime(from.Year, from.Month, 1); cursor <= new DateTime(to.Year, to.Month, 1); cursor = cursor.AddMonths(1))
						bucketList.Add((cursor.ToString("yyyy-MM"), cursor.ToString("yyyy-MM-dd 00:00:00"), cursor.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd 23:59:59")));
					return bucketList;

				default:
					return new List<(string, string, string)>();
			}
		}

		private static string BuildPeriodLabel(AttendanceAnalyticsPeriod period, List<(string Label, string From, string To)> buckets)
		{
			if (buckets.Count == 0)
				return string.Empty;
			return (period == AttendanceAnalyticsPeriod.MonthlyRange && buckets.Count > 1)
				? $"{buckets[0].Label} to {buckets[^1].Label}"
				: buckets[0].Label;
		}

		private (string Sql, Dictionary<string, object> Params) BuildClassAnalyticsSql(Guid schoolId, string bucketExpr, string from, string to, int? attendanceType, Guid? classroomId)
		{
			var parameters = new Dictionary<string, object> { { "SchoolId", schoolId }, { "From", from }, { "To", to } };
			var conditions = new List<string>
			{
				"s.SchoolId = @SchoolId",
				"s.ClassroomId IS NOT NULL",
				"s.IsActive = 1",
				"s.Status = 1",
				"s.StartedAt >= @From",
				"s.StartedAt <= @To"
			};
			if (attendanceType.HasValue) { conditions.Add("s.AttendanceType = @AttendanceType"); parameters["AttendanceType"] = attendanceType.Value; }
			if (classroomId.HasValue) { conditions.Add("s.ClassroomId = @ClassroomId"); parameters["ClassroomId"] = classroomId.Value; }
			var where = string.Join(" AND ", conditions);
			var sql = $@"SELECT {bucketExpr} AS Bucket, s.ClassroomId AS Id, c.Name AS Name,
						COUNT(s.Id) AS Sessions,
						ISNULL(SUM(rosterCount.RosterSize), 0) AS Expected,
						ISNULL(SUM(presentCount.Present), 0) AS Present
					FROM dbo.AttendanceSession s
					JOIN dbo.Classroom c ON c.Id = s.ClassroomId
					LEFT JOIN (SELECT ClassroomId, COUNT(*) AS RosterSize FROM dbo.StudentClassroom
								WHERE SchoolId = @SchoolId AND IsActive = 1 GROUP BY ClassroomId) rosterCount ON rosterCount.ClassroomId = s.ClassroomId
					LEFT JOIN (SELECT SessionId, COUNT(*) AS Present FROM dbo.AttendanceRecord
								WHERE SchoolId = @SchoolId AND IsPresent = 1 AND IsActive = 1 GROUP BY SessionId) presentCount ON presentCount.SessionId = s.Id
					WHERE {where}
					GROUP BY {bucketExpr}, s.ClassroomId, c.Name
					ORDER BY c.Name ASC";
			return (sql, parameters);
		}

		private (string Sql, Dictionary<string, object> Params) BuildSubjectAnalyticsSql(Guid schoolId, string bucketExpr, string from, string to, int? attendanceType, Guid? subjectId)
		{
			var parameters = new Dictionary<string, object> { { "SchoolId", schoolId }, { "From", from }, { "To", to } };
			var conditions = new List<string>
			{
				"s.SchoolId = @SchoolId",
				"s.SubjectId IS NOT NULL",
				"s.IsActive = 1",
				"s.Status = 1",
				"s.StartedAt >= @From",
				"s.StartedAt <= @To"
			};
			if (attendanceType.HasValue) { conditions.Add("s.AttendanceType = @AttendanceType"); parameters["AttendanceType"] = attendanceType.Value; }
			if (subjectId.HasValue) { conditions.Add("s.SubjectId = @SubjectId"); parameters["SubjectId"] = subjectId.Value; }
			var where = string.Join(" AND ", conditions);
			var sql = $@"SELECT {bucketExpr} AS Bucket, s.SubjectId AS Id, sj.Subject AS Name,
						COUNT(s.Id) AS Sessions,
						ISNULL(SUM(CASE WHEN s.ClassroomId IS NOT NULL THEN classRoster.RosterSize ELSE subjectRoster.RosterSize END), 0) AS Expected,
						ISNULL(SUM(presentCount.Present), 0) AS Present
					FROM dbo.AttendanceSession s
					JOIN dbo.Subjects sj ON sj.Id = s.SubjectId
					LEFT JOIN (SELECT ClassroomId, COUNT(*) AS RosterSize FROM dbo.StudentClassroom
								WHERE SchoolId = @SchoolId AND IsActive = 1 GROUP BY ClassroomId) classRoster ON classRoster.ClassroomId = s.ClassroomId
					LEFT JOIN (SELECT SubjectId, COUNT(*) AS RosterSize FROM (
									SELECT cs.SubjectId, sc.StudentId FROM dbo.StudentClassroom sc
									JOIN dbo.ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId
									WHERE cs.SchoolId = @SchoolId AND sc.IsActive = 1 AND cs.IsActive = 1
									UNION
									SELECT sms.SubjectId, sms.StudentId FROM dbo.StudentMinorSubject sms
									WHERE sms.SchoolId = @SchoolId AND sms.IsActive = 1
								) subRoster GROUP BY SubjectId) subjectRoster ON subjectRoster.SubjectId = s.SubjectId
					LEFT JOIN (SELECT SessionId, COUNT(*) AS Present FROM dbo.AttendanceRecord
								WHERE SchoolId = @SchoolId AND IsPresent = 1 AND IsActive = 1 GROUP BY SessionId) presentCount ON presentCount.SessionId = s.Id
					WHERE {where}
					GROUP BY {bucketExpr}, s.SubjectId, sj.Subject
					ORDER BY sj.Subject ASC";
			return (sql, parameters);
		}

		private (string Sql, Dictionary<string, object> Params) BuildTotalsSql(Guid schoolId, string bucketExpr, string from, string to, int? attendanceType, Guid? classroomId, Guid? subjectId)
		{
			var parameters = new Dictionary<string, object> { { "SchoolId", schoolId }, { "From", from }, { "To", to } };
			var conditions = new List<string>
			{
				"s.SchoolId = @SchoolId",
				"s.IsActive = 1",
				"s.Status = 1",
				"s.ClassroomId IS NOT NULL OR s.SubjectId IS NOT NULL",
				"s.StartedAt >= @From",
				"s.StartedAt <= @To"
			};
			if (attendanceType.HasValue) { conditions.Add("s.AttendanceType = @AttendanceType"); parameters["AttendanceType"] = attendanceType.Value; }
			if (classroomId.HasValue) { conditions.Add("s.ClassroomId = @ClassroomId"); parameters["ClassroomId"] = classroomId.Value; }
			if (subjectId.HasValue) { conditions.Add("s.SubjectId = @SubjectId"); parameters["SubjectId"] = subjectId.Value; }
			var where = string.Join(" AND ", conditions);
			var sql = $@"SELECT {bucketExpr} AS Bucket,
						COUNT(s.Id) AS Sessions,
						ISNULL(SUM(CASE WHEN s.ClassroomId IS NOT NULL THEN classRoster.RosterSize ELSE subjectRoster.RosterSize END), 0) AS Expected,
						ISNULL(SUM(presentCount.Present), 0) AS Present
					FROM dbo.AttendanceSession s
					LEFT JOIN (SELECT ClassroomId, COUNT(*) AS RosterSize FROM dbo.StudentClassroom
								WHERE SchoolId = @SchoolId AND IsActive = 1 GROUP BY ClassroomId) classRoster ON classRoster.ClassroomId = s.ClassroomId
					LEFT JOIN (SELECT SubjectId, COUNT(*) AS RosterSize FROM (
									SELECT cs.SubjectId, sc.StudentId FROM dbo.StudentClassroom sc
									JOIN dbo.ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId
									WHERE cs.SchoolId = @SchoolId AND sc.IsActive = 1 AND cs.IsActive = 1
									UNION
									SELECT sms.SubjectId, sms.StudentId FROM dbo.StudentMinorSubject sms
									WHERE sms.SchoolId = @SchoolId AND sms.IsActive = 1
								) subRoster GROUP BY SubjectId) subjectRoster ON subjectRoster.SubjectId = s.SubjectId
					LEFT JOIN (SELECT SessionId, COUNT(*) AS Present FROM dbo.AttendanceRecord
								WHERE SchoolId = @SchoolId AND IsPresent = 1 AND IsActive = 1 GROUP BY SessionId) presentCount ON presentCount.SessionId = s.Id
					WHERE {where}
					GROUP BY {bucketExpr}";
			return (sql, parameters);
		}

		private static AttendanceAnalyticsTotalsDto ToTotalsDto(AnalyticsTotalsRow row)
		{
			var totals = new AttendanceAnalyticsTotalsDto
			{
				Sessions = row?.Sessions ?? 0,
				Expected = row?.Expected ?? 0,
				Present = row?.Present ?? 0
			};
			totals.Absent = Math.Max(0, totals.Expected - totals.Present);
			totals.AttendanceRate = totals.Expected > 0 ? Math.Round((decimal)totals.Present / totals.Expected * 100, 1) : 0;
			return totals;
		}

		private static AttendanceAnalyticsRowDto MapAnalyticsRow(AnalyticsRow row)
		{
			var dto = new AttendanceAnalyticsRowDto
			{
				Id = row.Id,
				Name = row.Name,
				Sessions = row.Sessions,
				Expected = row.Expected,
				Present = row.Present,
				Absent = Math.Max(0, row.Expected - row.Present)
			};
			dto.AttendanceRate = dto.Expected > 0 ? Math.Round((decimal)dto.Present / dto.Expected * 100, 1) : 0;
			return dto;
		}

		private class AnalyticsRow
		{
			public string Bucket { get; set; }
			public Guid Id { get; set; }
			public string Name { get; set; }
			public int Sessions { get; set; }
			public int Expected { get; set; }
			public int Present { get; set; }
		}

		private class AnalyticsTotalsRow
		{
			public string Bucket { get; set; }
			public int Sessions { get; set; }
			public int Expected { get; set; }
			public int Present { get; set; }
		}

		private (string Sql, Dictionary<string, object> Params) BuildAbsentStudentsSql(Guid schoolId, int attendanceType, Guid entityId, string from, string to)
		{
			var parameters = new Dictionary<string, object> { { "SchoolId", schoolId }, { "From", from }, { "To", to } };
			if (attendanceType == (int)AttendanceType.Class)
			{
				parameters["ClassroomId"] = entityId;
				var sql = @"SELECT u.Id AS StudentId, (u.FirstName + ' ' + u.LastName) AS StudentName,
							COUNT(s.Id) AS Sessions,
							SUM(CASE WHEN r.Id IS NULL THEN 1 ELSE 0 END) AS AbsentCount,
							SUM(CASE WHEN r.Id IS NOT NULL THEN 1 ELSE 0 END) AS PresentCount
						FROM dbo.AttendanceSession s
						JOIN dbo.StudentClassroom sc ON sc.ClassroomId = s.ClassroomId AND sc.SchoolId = @SchoolId AND sc.IsActive = 1
						JOIN dbo.Users u ON u.Id = sc.StudentId AND u.IsActive = 1
						LEFT JOIN dbo.AttendanceRecord r ON r.SessionId = s.Id AND r.StudentId = u.Id AND r.SchoolId = @SchoolId AND r.IsPresent = 1 AND r.IsActive = 1
						WHERE s.SchoolId = @SchoolId AND s.AttendanceType = 0 AND s.ClassroomId = @ClassroomId AND s.IsActive = 1 AND s.Status = 1
							AND s.StartedAt >= @From AND s.StartedAt <= @To
						GROUP BY u.Id, u.FirstName, u.LastName
						HAVING SUM(CASE WHEN r.Id IS NULL THEN 1 ELSE 0 END) > 0
						ORDER BY AbsentCount DESC, u.FirstName ASC";
				return (sql, parameters);
			}
			else
			{
				parameters["SubjectId"] = entityId;
				var sql = @"SELECT u.Id AS StudentId, (u.FirstName + ' ' + u.LastName) AS StudentName,
							COUNT(s.Id) AS Sessions,
							SUM(CASE WHEN r.Id IS NULL THEN 1 ELSE 0 END) AS AbsentCount,
							SUM(CASE WHEN r.Id IS NOT NULL THEN 1 ELSE 0 END) AS PresentCount
						FROM dbo.AttendanceSession s
						CROSS APPLY (
							SELECT sc.StudentId FROM dbo.StudentClassroom sc
							WHERE s.ClassroomId IS NOT NULL AND sc.ClassroomId = s.ClassroomId AND sc.SchoolId = @SchoolId AND sc.IsActive = 1
							UNION
							SELECT sc2.StudentId FROM dbo.StudentClassroom sc2
							JOIN dbo.ClassroomSubject cs ON cs.ClassroomId = sc2.ClassroomId
							WHERE s.ClassroomId IS NULL AND cs.SubjectId = s.SubjectId AND cs.SchoolId = @SchoolId AND sc2.IsActive = 1 AND cs.IsActive = 1
							UNION
							SELECT sms.StudentId FROM dbo.StudentMinorSubject sms
							WHERE s.ClassroomId IS NULL AND sms.SubjectId = s.SubjectId AND sms.SchoolId = @SchoolId AND sms.IsActive = 1
						) roster
						JOIN dbo.Users u ON u.Id = roster.StudentId AND u.IsActive = 1
						LEFT JOIN dbo.AttendanceRecord r ON r.SessionId = s.Id AND r.StudentId = u.Id AND r.SchoolId = @SchoolId AND r.IsPresent = 1 AND r.IsActive = 1
						WHERE s.SchoolId = @SchoolId AND s.AttendanceType IN (1,2) AND s.SubjectId = @SubjectId AND s.IsActive = 1 AND s.Status = 1
							AND s.StartedAt >= @From AND s.StartedAt <= @To
						GROUP BY u.Id, u.FirstName, u.LastName
						HAVING SUM(CASE WHEN r.Id IS NULL THEN 1 ELSE 0 END) > 0
						ORDER BY AbsentCount DESC, u.FirstName ASC";
				return (sql, parameters);
			}
		}

		private (string Sql, Dictionary<string, object> Params) BuildStudentStatsSql(Guid schoolId, Guid studentId, int attendanceType, Guid entityId, string from, string to)
		{
			var parameters = new Dictionary<string, object> { { "SchoolId", schoolId }, { "StudentId", studentId }, { "From", from }, { "To", to } };
			if (attendanceType == (int)AttendanceType.Class)
			{
				parameters["ClassroomId"] = entityId;
				var sql = @"SELECT COUNT(s.Id) AS Sessions,
							ISNULL(SUM(CASE WHEN r.Id IS NULL THEN 0 ELSE 1 END), 0) AS PresentCount,
							ISNULL(SUM(CASE WHEN r.Id IS NULL THEN 1 ELSE 0 END), 0) AS AbsentCount
						FROM dbo.AttendanceSession s
						LEFT JOIN dbo.AttendanceRecord r ON r.SessionId = s.Id AND r.StudentId = @StudentId AND r.SchoolId = @SchoolId AND r.IsPresent = 1 AND r.IsActive = 1
						WHERE s.SchoolId = @SchoolId AND s.AttendanceType = 0 AND s.ClassroomId = @ClassroomId AND s.IsActive = 1 AND s.Status = 1
							AND s.StartedAt >= @From AND s.StartedAt <= @To
							AND EXISTS (SELECT 1 FROM dbo.StudentClassroom sc WHERE sc.ClassroomId = s.ClassroomId AND sc.StudentId = @StudentId AND sc.SchoolId = @SchoolId AND sc.IsActive = 1)";
				return (sql, parameters);
			}
			else
			{
				parameters["SubjectId"] = entityId;
				var sql = @"SELECT COUNT(s.Id) AS Sessions,
							ISNULL(SUM(CASE WHEN r.Id IS NULL THEN 0 ELSE 1 END), 0) AS PresentCount,
							ISNULL(SUM(CASE WHEN r.Id IS NULL THEN 1 ELSE 0 END), 0) AS AbsentCount
						FROM dbo.AttendanceSession s
						LEFT JOIN dbo.AttendanceRecord r ON r.SessionId = s.Id AND r.StudentId = @StudentId AND r.SchoolId = @SchoolId AND r.IsPresent = 1 AND r.IsActive = 1
						WHERE s.SchoolId = @SchoolId AND s.AttendanceType IN (1,2) AND s.SubjectId = @SubjectId AND s.IsActive = 1 AND s.Status = 1
							AND s.StartedAt >= @From AND s.StartedAt <= @To
							AND (
								EXISTS (SELECT 1 FROM dbo.StudentClassroom sc JOIN dbo.ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId
										WHERE cs.SubjectId = s.SubjectId AND sc.StudentId = @StudentId AND sc.SchoolId = @SchoolId AND sc.IsActive = 1 AND cs.IsActive = 1)
								OR EXISTS (SELECT 1 FROM dbo.StudentMinorSubject sms
										WHERE sms.SubjectId = s.SubjectId AND sms.StudentId = @StudentId AND sms.SchoolId = @SchoolId AND sms.IsActive = 1)
							)";
				return (sql, parameters);
			}
		}

		private async Task<int> GetRosterCountAsync(Guid schoolId, int attendanceType, Guid entityId)
		{
			if (attendanceType == (int)AttendanceType.Class)
			{
				return await _recordQueryRepo.CountAsync(
					"SELECT COUNT(*) FROM dbo.StudentClassroom WHERE ClassroomId = @ClassroomId AND SchoolId = @SchoolId AND IsActive = 1",
					new Dictionary<string, object> { { "ClassroomId", entityId }, { "SchoolId", schoolId } });
			}
			return await _recordQueryRepo.CountAsync(
				@"SELECT COUNT(*) FROM (
					SELECT sc.StudentId FROM dbo.StudentClassroom sc
					JOIN dbo.ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId
					WHERE cs.SubjectId = @SubjectId AND cs.SchoolId = @SchoolId AND sc.IsActive = 1 AND cs.IsActive = 1
					UNION
					SELECT sms.StudentId FROM dbo.StudentMinorSubject sms
					WHERE sms.SubjectId = @SubjectId AND sms.SchoolId = @SchoolId AND sms.IsActive = 1
				) t",
				new Dictionary<string, object> { { "SubjectId", entityId }, { "SchoolId", schoolId } });
		}

		private class StudentStatsRow
		{
			public int Sessions { get; set; }
			public int PresentCount { get; set; }
			public int AbsentCount { get; set; }
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

			if (!string.Equals(role, "ClassTeacher", StringComparison.OrdinalIgnoreCase))
				return false;

			return await _classroomTeacherQueryRepo.CountAsync(
				"SELECT TOP 1 1 FROM TeacherClassroom WHERE TeacherId = @TeacherId AND ClassroomId = @ClassroomId AND SchoolId = @SchoolId AND IsActive = 1",
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

			if (!string.Equals(role, "SubjectTeacher", StringComparison.OrdinalIgnoreCase))
				return false;

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
					if (!session.SubjectId.HasValue)
						return false;
					if (!await IsStudentInSubjectAsync(studentId, session.SubjectId.Value, schoolId))
						return false;
					if (session.ClassroomId.HasValue)
						return await IsStudentInClassroomAsync(studentId, session.ClassroomId.Value, schoolId);
					return true;

				case AttendanceType.SubTopic:
					if (!session.SubjectId.HasValue)
						return false;
					if (!await IsStudentInSubjectAsync(studentId, session.SubjectId.Value, schoolId))
						return false;
					if (session.ClassroomId.HasValue)
						return await IsStudentInClassroomAsync(studentId, session.ClassroomId.Value, schoolId);
					return true;

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

		private async Task<object> BuildRecordDtoAsync(Guid recordId, Guid sessionId, Guid schoolId, bool alreadyMarked = false)
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
				AttendedAt = record.AttendedAt,
				AlreadyMarked = alreadyMarked
			};
		}

		#endregion
	}
}
