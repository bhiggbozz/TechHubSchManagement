using Dapper;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Configuration;
using TechHub.Core.Constant;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.LiveClassModels;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.Service.Service
{
	/// <summary>
	/// Live classes: one teacher and up to three students the teacher picked. The picked set is fixed,
	/// at most <see cref="LiveClassSettings.AbsoluteMaxMinutes"/> minutes, started from an Approved
	/// lesson. Every limit is enforced here on the server — never trusted from the browser.
	/// </summary>
	public class LiveClassService : ILiveClassService
	{
		private static readonly string[] TeacherRoles = { "SubjectTeacher", "HeadTeacher", "ClassTeacher" };

		/// <summary>Participants allowed in the audio room: the teacher plus the maximum number of students.</summary>
		private const int RoomCapacity = LiveClassSettings.AbsoluteMaxStudents + 1;

		/// <summary>Unstarted classes older than this are cancelled by the timer.</summary>
		private static readonly TimeSpan ScheduledLifetime = TimeSpan.FromHours(12);

		private readonly IQueryRepository<LiveClass> _query;
		private readonly IDbTransactionScopeFactory _scopeFactory;
		private readonly ILiveKitService _liveKit;
		private readonly ILiveClassBroadcaster _broadcaster;
		private readonly ISchoolFeatureService _features;
		private readonly INotificationService _notifications;
		private readonly LiveClassSettings _settings;
		private readonly ILogger _logger;

		public LiveClassService(
			IQueryRepository<LiveClass> query,
			IDbTransactionScopeFactory scopeFactory,
			ILiveKitService liveKit,
			ILiveClassBroadcaster broadcaster,
			ISchoolFeatureService features,
			INotificationService notifications,
			IOptions<LiveClassSettings> settings,
			ILogger logger)
		{
			_query = query;
			_scopeFactory = scopeFactory;
			_liveKit = liveKit;
			_broadcaster = broadcaster;
			_features = features;
			_notifications = notifications;
			_settings = settings.Value;
			_logger = logger;
		}

		private int ClassMinutes => Math.Clamp(_settings.DurationMinutes, 1, LiveClassSettings.AbsoluteMaxMinutes);

		#region Teacher

		public async Task<BaseResponse> CreateAsync(CreateLiveClassViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					if (!TryCaller(claims, out var userId, out var schoolId, out var role))
						return Fail(ResponseCode.Unauthorized, "Invalid user context");
					if (!TeacherRoles.Contains(role))
						return Fail(ResponseCode.Forbidden, "Only teachers can start live classes");
					if (model == null || model.LessonId == Guid.Empty)
						return Fail(ResponseCode.BadRequest, "A lesson is required");

					var gate = await CheckFeatureAsync(schoolId);
					if (gate != null) return gate;

					var studentIds = (model.StudentIds ?? new List<Guid>()).Where(s => s != Guid.Empty).Distinct().ToList();
					if (studentIds.Count < 1)
						return Fail(ResponseCode.BadRequest, "Pick at least one student");
					if (studentIds.Count > LiveClassSettings.AbsoluteMaxStudents)
						return Fail(ResponseCode.BadRequest, $"A live class can have at most {LiveClassSettings.AbsoluteMaxStudents} students");

					var lesson = (await _query.QueryAsync<LessonRow>(
						"SELECT Id, ClassroomId, SubjectId, Aim, Status, CreatedBy FROM LessonContent WHERE Id = @Id AND SchoolId = @SchoolId AND IsActive = 1",
						new Dictionary<string, object> { { "Id", model.LessonId }, { "SchoolId", schoolId } })).FirstOrDefault();
					if (lesson is null)
						return Fail(ResponseCode.NotFound, "Lesson not found");
					if (!string.Equals(lesson.Status, "Approved", StringComparison.OrdinalIgnoreCase))
						return Fail(ResponseCode.BadRequest, "A live class can only be started from an approved lesson that has not been published yet");
					if (role != "HeadTeacher" && lesson.CreatedBy != userId)
						return Fail(ResponseCode.Forbidden, "You can only run a live class for your own lesson");

					var valid = (await _query.QueryAsync<int>(
						$@"SELECT COUNT(*) FROM Users u
						   WHERE u.SchoolId = @SchoolId AND u.IsActive = 1 AND u.RoleId = 0 AND u.Id IN ({GuidList(studentIds)})
						   AND EXISTS (SELECT 1 FROM StudentClassroom sc WHERE sc.StudentId = u.Id AND sc.ClassroomId = @ClassroomId AND sc.SchoolId = @SchoolId AND sc.IsActive = 1)",
						new Dictionary<string, object> { { "SchoolId", schoolId }, { "ClassroomId", lesson.ClassroomId } })).FirstOrDefault();
					if (valid != studentIds.Count)
						return Fail(ResponseCode.BadRequest, "Every picked student must be an active student in this lesson's class");

					var open = (await _query.QueryAsync<int>(
						"SELECT COUNT(*) FROM LiveClass WHERE LessonId = @LessonId AND IsActive = 1 AND Status IN ('Scheduled','Live')",
						new Dictionary<string, object> { { "LessonId", model.LessonId } })).FirstOrDefault();
					if (open > 0)
						return Fail(ResponseCode.Conflict, "This lesson already has a live class waiting or running");

					var id = Guid.NewGuid();
					var title = string.IsNullOrWhiteSpace(model.Title) ? Truncate(lesson.Aim ?? "Live class", 200) : Truncate(model.Title.Trim(), 200);

					using (var scope = _scopeFactory.Create("DbConnectionString"))
					{
						try
						{
							await scope.Connection.ExecuteAsync(
								@"INSERT INTO LiveClass (Id, SchoolId, LessonId, ClassroomId, SubjectId, TeacherId, Title, Status, DurationMinutes, MaxStudents, BoardSessionId, RoomName)
								  VALUES (@Id, @SchoolId, @LessonId, @ClassroomId, @SubjectId, @TeacherId, @Title, 'Scheduled', @Minutes, @MaxStudents, @BoardSessionId, @RoomName)",
								new
								{
									Id = id, SchoolId = schoolId, LessonId = model.LessonId, lesson.ClassroomId, lesson.SubjectId,
									TeacherId = userId, Title = title, Minutes = ClassMinutes, MaxStudents = LiveClassSettings.AbsoluteMaxStudents,
									BoardSessionId = Guid.NewGuid(), RoomName = $"lc-{id:N}"
								}, scope.Transaction);

							await scope.Connection.ExecuteAsync(
								"INSERT INTO LiveClassStudent (Id, LiveClassId, StudentId, SchoolId) VALUES (NEWID(), @LiveClassId, @StudentId, @SchoolId)",
								studentIds.Select(s => new { LiveClassId = id, StudentId = s, SchoolId = schoolId }), scope.Transaction);

							await scope.CommitAsync();
						}
						catch
						{
							try { await scope.RollbackAsync(); } catch { }
							throw;
						}
					}

					_logger.Information("LiveClass: {LiveClassId} created by {TeacherId} for lesson {LessonId} with {Count} student(s)",
						id, userId, model.LessonId, studentIds.Count);

					return Ok("Live class created", await LoadOneAsync(id, schoolId));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "LiveClass: error creating a live class");
					return Fail(ResponseCode.ErrorOccured, "An error occurred while creating the live class");
				}
			}
		}

		public async Task<BaseResponse> StartAsync(Guid liveClassId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					if (!TryCaller(claims, out var userId, out var schoolId, out var role))
						return Fail(ResponseCode.Unauthorized, "Invalid user context");

					var lc = await GetRawAsync(liveClassId, schoolId);
					if (lc is null || lc.TeacherId != userId || !TeacherRoles.Contains(role))
						return Fail(ResponseCode.NotFound, "Live class not found");

					var gate = await CheckFeatureAsync(schoolId);
					if (gate != null) return gate;

					if (!_liveKit.IsConfigured)
						return Fail(ResponseCode.ErrorOccured, "Live classes are not set up on the server yet");

					if (lc.Status != LiveClassStatus.Scheduled)
						return Fail(ResponseCode.Conflict, "This class has already been started or has finished");

					var teacherBusy = (await _query.QueryAsync<int>(
						"SELECT COUNT(*) FROM LiveClass WHERE TeacherId = @TeacherId AND Status = 'Live' AND Id <> @Id",
						new Dictionary<string, object> { { "TeacherId", userId }, { "Id", liveClassId } })).FirstOrDefault();
					if (teacherBusy > 0)
						return Fail(ResponseCode.Conflict, "You already have a live class running. End it before starting another.");

					var studentBusy = (await _query.QueryAsync<int>(
						@"SELECT COUNT(*) FROM LiveClassStudent mine
						  JOIN LiveClassStudent other ON other.StudentId = mine.StudentId AND other.LiveClassId <> mine.LiveClassId
						  JOIN LiveClass l ON l.Id = other.LiveClassId AND l.Status = 'Live'
						  WHERE mine.LiveClassId = @Id",
						new Dictionary<string, object> { { "Id", liveClassId } })).FirstOrDefault();
					if (studentBusy > 0)
						return Fail(ResponseCode.Conflict, "One of the picked students is already in another live class right now");

					var now = DateTime.UtcNow;
					var ends = now.AddMinutes(lc.DurationMinutes > 0 ? Math.Min(lc.DurationMinutes, LiveClassSettings.AbsoluteMaxMinutes) : ClassMinutes);

					// One statement checks the status and the platform-wide cap together, so two teachers
					// starting at the same moment cannot both squeeze past the limit.
					var started = (await _query.QueryAsync<int>(
						@"UPDATE LiveClass SET Status = 'Live', StartedAt = @Now, EndsAt = @Ends, Warned5 = 0, Warned2 = 0
						  WHERE Id = @Id AND SchoolId = @SchoolId AND Status = 'Scheduled'
						  AND (SELECT COUNT(*) FROM LiveClass WHERE Status = 'Live') < @Max;
						  SELECT @@ROWCOUNT;",
						new Dictionary<string, object> { { "Now", now }, { "Ends", ends }, { "Id", liveClassId }, { "SchoolId", schoolId }, { "Max", _settings.MaxConcurrent } })).FirstOrDefault();

					if (started == 0)
					{
						var current = await GetRawAsync(liveClassId, schoolId);
						return current?.Status != LiveClassStatus.Scheduled
							? Fail(ResponseCode.Conflict, "This class has already been started or has finished")
							: Fail(ResponseCode.Conflict, "No live class slots are free right now. Please try again in a few minutes.");
					}

					if (!await _liveKit.CreateRoomAsync(lc.RoomName, RoomCapacity, emptyTimeoutSeconds: 600))
					{
						// Could not open the audio room: put the class back so the teacher can try again.
						await _query.QueryAsync<int>(
							"UPDATE LiveClass SET Status = 'Scheduled', StartedAt = NULL, EndsAt = NULL WHERE Id = @Id AND Status = 'Live'; SELECT @@ROWCOUNT;",
							new Dictionary<string, object> { { "Id", liveClassId } });
						return Fail(ResponseCode.ErrorOccured, "Could not open the audio room. Please try again in a moment.");
					}

					_logger.Information("LiveClass: {LiveClassId} started by {TeacherId}, ends {EndsAt:o}", liveClassId, userId, ends);

					var detail = await LoadOneAsync(liveClassId, schoolId);
					if (detail != null)
					{
						var teacherName = detail.TeacherName ?? "Your teacher";
						await _notifications.NotifyLiveClassStartedAsync(liveClassId, detail.Students.Select(s => s.StudentId).ToList(), schoolId, teacherName, detail.Title);
					}
					return Ok("Live class started", detail);
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "LiveClass: error starting {LiveClassId}", liveClassId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while starting the live class");
				}
			}
		}

		public async Task<BaseResponse> EndAsync(Guid liveClassId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					if (!TryCaller(claims, out var userId, out var schoolId, out var role))
						return Fail(ResponseCode.Unauthorized, "Invalid user context");

					var lc = await GetRawAsync(liveClassId, schoolId);
					if (lc is null || lc.TeacherId != userId || !TeacherRoles.Contains(role))
						return Fail(ResponseCode.NotFound, "Live class not found");

					if (lc.Status == LiveClassStatus.Scheduled)
						return Fail(ResponseCode.BadRequest, "This class has not started. Cancel it instead.");

					if (lc.Status == LiveClassStatus.Live)
						await EndInternalAsync(lc.Id, lc.SchoolId, lc.RoomName, LiveClassEndReason.TeacherEnded);

					return Ok("Live class ended", await LoadOneAsync(liveClassId, schoolId));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "LiveClass: error ending {LiveClassId}", liveClassId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while ending the live class");
				}
			}
		}

		public async Task<BaseResponse> CancelAsync(Guid liveClassId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					if (!TryCaller(claims, out var userId, out var schoolId, out var role))
						return Fail(ResponseCode.Unauthorized, "Invalid user context");

					var lc = await GetRawAsync(liveClassId, schoolId);
					if (lc is null || lc.TeacherId != userId || !TeacherRoles.Contains(role))
						return Fail(ResponseCode.NotFound, "Live class not found");

					var cancelled = (await _query.QueryAsync<int>(
						@"UPDATE LiveClass SET Status = 'Cancelled', EndedAt = @Now, EndReason = 'Cancelled'
						  WHERE Id = @Id AND SchoolId = @SchoolId AND Status = 'Scheduled'; SELECT @@ROWCOUNT;",
						new Dictionary<string, object> { { "Now", DateTime.UtcNow }, { "Id", liveClassId }, { "SchoolId", schoolId } })).FirstOrDefault();

					return cancelled == 0
						? Fail(ResponseCode.Conflict, "Only a class that has not started can be cancelled")
						: Ok("Live class cancelled", await LoadOneAsync(liveClassId, schoolId));
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "LiveClass: error cancelling {LiveClassId}", liveClassId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while cancelling the live class");
				}
			}
		}

		public async Task<BaseResponse> SetStudentSpeakAsync(Guid liveClassId, Guid studentId, bool allow, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					if (!TryCaller(claims, out var userId, out var schoolId, out var role))
						return Fail(ResponseCode.Unauthorized, "Invalid user context");

					var lc = await GetRawAsync(liveClassId, schoolId);
					if (lc is null || lc.TeacherId != userId || !TeacherRoles.Contains(role))
						return Fail(ResponseCode.NotFound, "Live class not found");
					if (lc.Status != LiveClassStatus.Live)
						return Fail(ResponseCode.BadRequest, "This class is not live");

					var updated = (await _query.QueryAsync<int>(
						"UPDATE LiveClassStudent SET CanSpeak = @Allow WHERE LiveClassId = @Id AND StudentId = @StudentId; SELECT @@ROWCOUNT;",
						new Dictionary<string, object> { { "Allow", allow }, { "Id", liveClassId }, { "StudentId", studentId } })).FirstOrDefault();
					if (updated == 0)
						return Fail(ResponseCode.NotFound, "That student is not in this class");

					// The saved flag decides the token a rejoining student gets; this applies it to their live connection.
					var applied = await _liveKit.SetCanSpeakAsync(lc.RoomName, studentId.ToString(), allow);
					await _broadcaster.SpeakChangedAsync(liveClassId, studentId, allow);

					return Ok(applied ? (allow ? "Student can now speak" : "Student is muted")
									  : "Saved. It takes effect as soon as the student is connected.",
						new { studentId, canSpeak = allow, appliedNow = applied });
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "LiveClass: error changing speak permission in {LiveClassId}", liveClassId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while changing the student's permission");
				}
			}
		}

		#endregion

		#region Teacher or student

		public async Task<BaseResponse> GetAsync(Guid liveClassId, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!TryCaller(claims, out var userId, out var schoolId, out _))
					return Fail(ResponseCode.Unauthorized, "Invalid user context");

				var access = await GetAccessAsync(liveClassId, userId, schoolId);
				if (access is null)
					return Fail(ResponseCode.NotFound, "Live class not found");

				return Ok("Live class retrieved", await LoadOneAsync(liveClassId, schoolId));
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "LiveClass: error loading {LiveClassId}", liveClassId);
				return Fail(ResponseCode.ErrorOccured, "An error occurred while loading the live class");
			}
		}

		public async Task<BaseResponse> MineAsync(AuthenticatedUserClaims claims)
		{
			try
			{
				if (!TryCaller(claims, out var userId, out var schoolId, out var role))
					return Fail(ResponseCode.Unauthorized, "Invalid user context");

				var p = new Dictionary<string, object> { { "UserId", userId }, { "SchoolId", schoolId }, { "Since", DateTime.UtcNow.AddDays(-7) } };
				var recent = "(lc.Status IN ('Scheduled','Live') OR lc.EndedAt > @Since)";

				var list = role == "Student"
					? await LoadAsync($"lc.SchoolId = @SchoolId AND lc.Id IN (SELECT LiveClassId FROM LiveClassStudent WHERE StudentId = @UserId) AND lc.Status <> 'Cancelled' AND {recent}", p)
					: await LoadAsync($"lc.SchoolId = @SchoolId AND lc.TeacherId = @UserId AND {recent}", p);

				return Ok(list.Count == 0 ? "No live classes" : $"{list.Count} live class(es)", list);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "LiveClass: error listing classes");
				return Fail(ResponseCode.ErrorOccured, "An error occurred while loading your live classes");
			}
		}

		public async Task<BaseResponse> JoinAsync(Guid liveClassId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims?.UserId))
			{
				try
				{
					if (!TryCaller(claims, out var userId, out var schoolId, out var role))
						return Fail(ResponseCode.Unauthorized, "Invalid user context");

					var access = await GetAccessAsync(liveClassId, userId, schoolId);
					// Anyone not on the list (or a role that does not match the list) just sees "not found".
					if (access is null
						|| (access.IsTeacher && !TeacherRoles.Contains(role))
						|| (!access.IsTeacher && role != "Student"))
						return Fail(ResponseCode.NotFound, "Live class not found");

					if (access.Status != LiveClassStatus.Live)
						return Fail(ResponseCode.BadRequest, access.Status == LiveClassStatus.Scheduled
							? "This class has not started yet"
							: "This class is over");

					var now = DateTime.UtcNow;
					if (access.EndsAt is null || Utc(access.EndsAt.Value) <= now)
						return Fail(ResponseCode.BadRequest, "This class is over");

					if (!_liveKit.IsConfigured)
						return Fail(ResponseCode.ErrorOccured, "Live classes are not set up on the server yet");

					var lc = (await GetRawAsync(liveClassId, schoolId))!;
					var name = (await _query.QueryAsync<string>(
						"SELECT FirstName + ' ' + LastName FROM Users WHERE Id = @Id",
						new Dictionary<string, object> { { "Id", userId } })).FirstOrDefault() ?? "Participant";

					var canSpeak = true;
					if (!access.IsTeacher)
					{
						canSpeak = (await _query.QueryAsync<bool>(
							"SELECT CanSpeak FROM LiveClassStudent WHERE LiveClassId = @Id AND StudentId = @StudentId",
							new Dictionary<string, object> { { "Id", liveClassId }, { "StudentId", userId } })).FirstOrDefault();

						await _query.QueryAsync<int>(
							@"UPDATE LiveClassStudent SET FirstJoinedAt = COALESCE(FirstJoinedAt, @Now), JoinCount = JoinCount + 1, LastLeftAt = NULL
							  WHERE LiveClassId = @Id AND StudentId = @StudentId; SELECT @@ROWCOUNT;",
							new Dictionary<string, object> { { "Now", now }, { "Id", liveClassId }, { "StudentId", userId } });
					}

					var endsAt = Utc(access.EndsAt.Value);
					var remaining = endsAt - now;
					var token = _liveKit.CreateJoinToken(lc.RoomName, userId.ToString(), name, canSpeak, remaining + TimeSpan.FromMinutes(2), canPublishData: access.IsTeacher);

					return Ok("Ready to join", new LiveClassJoinDto
					{
						LiveClassId = liveClassId,
						Role = access.IsTeacher ? "Teacher" : "Student",
						DisplayName = name,
						Title = lc.Title,
						LessonId = lc.LessonId,
						BoardSessionId = lc.BoardSessionId,
						Audio = new LiveClassAudioDto { Url = _liveKit.ClientUrl, Token = token, RoomName = lc.RoomName },
						Board = new LiveClassBoardDto { Group = $"live:{liveClassId}" },
						EndsAt = endsAt,
						SecondsRemaining = (int)Math.Max(0, remaining.TotalSeconds),
						ServerTime = now,
						CanSpeak = canSpeak
					});
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "LiveClass: error joining {LiveClassId}", liveClassId);
					return Fail(ResponseCode.ErrorOccured, "An error occurred while joining the live class");
				}
			}
		}

		#endregion

		#region Hub and timer support

		public async Task<LiveClassAccess?> GetAccessAsync(Guid liveClassId, Guid userId, Guid schoolId)
		{
			var row = (await _query.QueryAsync<AccessRow>(
				@"SELECT lc.Status, lc.EndsAt,
				         CASE WHEN lc.TeacherId = @UserId THEN 1 ELSE 0 END AS IsTeacher,
				         CASE WHEN EXISTS (SELECT 1 FROM LiveClassStudent ls WHERE ls.LiveClassId = lc.Id AND ls.StudentId = @UserId) THEN 1 ELSE 0 END AS IsStudent
				  FROM LiveClass lc WHERE lc.Id = @Id AND lc.SchoolId = @SchoolId AND lc.IsActive = 1",
				new Dictionary<string, object> { { "Id", liveClassId }, { "UserId", userId }, { "SchoolId", schoolId } })).FirstOrDefault();

			if (row is null || (!row.IsTeacher && !row.IsStudent))
				return null;

			return new LiveClassAccess
			{
				LiveClassId = liveClassId, SchoolId = schoolId, Status = row.Status,
				IsTeacher = row.IsTeacher, IsStudent = row.IsStudent, EndsAt = row.EndsAt
			};
		}

		public async Task RecordStudentLeftAsync(Guid liveClassId, Guid studentId)
		{
			try
			{
				await _query.QueryAsync<int>(
					"UPDATE LiveClassStudent SET LastLeftAt = @Now WHERE LiveClassId = @Id AND StudentId = @StudentId; SELECT @@ROWCOUNT;",
					new Dictionary<string, object> { { "Now", DateTime.UtcNow }, { "Id", liveClassId }, { "StudentId", studentId } });
			}
			catch (Exception ex)
			{
				_logger.Warning(ex, "LiveClass: could not record that student {StudentId} left {LiveClassId}", studentId, liveClassId);
			}
		}

		public async Task TickAsync()
		{
			var now = DateTime.UtcNow;

			var live = (await _query.QueryAsync<TimerRow>(
				"SELECT Id, SchoolId, RoomName, EndsAt, Warned5, Warned2 FROM LiveClass WHERE Status = 'Live' AND IsActive = 1",
				new Dictionary<string, object>())).ToList();

			foreach (var c in live)
			{
				try
				{
					var endsAt = Utc(c.EndsAt);
					if (now >= endsAt)
					{
						await EndInternalAsync(c.Id, c.SchoolId, c.RoomName, LiveClassEndReason.TimeUp);
						continue;
					}

					var left = endsAt - now;
					if (left <= TimeSpan.FromMinutes(2) && !c.Warned2)
					{
						await _query.QueryAsync<int>("UPDATE LiveClass SET Warned5 = 1, Warned2 = 1 WHERE Id = @Id; SELECT @@ROWCOUNT;",
							new Dictionary<string, object> { { "Id", c.Id } });
						await _broadcaster.WarningAsync(c.Id, 2);
					}
					else if (left <= TimeSpan.FromMinutes(5) && !c.Warned5)
					{
						await _query.QueryAsync<int>("UPDATE LiveClass SET Warned5 = 1 WHERE Id = @Id; SELECT @@ROWCOUNT;",
							new Dictionary<string, object> { { "Id", c.Id } });
						await _broadcaster.WarningAsync(c.Id, 5);
					}
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "LiveClass: timer failed for {LiveClassId}", c.Id);
				}
			}

			await _query.QueryAsync<int>(
				@"UPDATE LiveClass SET Status = 'Cancelled', EndedAt = @Now, EndReason = 'Expired'
				  WHERE Status = 'Scheduled' AND CreatedAt < @Cutoff; SELECT @@ROWCOUNT;",
				new Dictionary<string, object> { { "Now", now }, { "Cutoff", now - ScheduledLifetime } });
		}

		#endregion

		#region Helpers

		private async Task EndInternalAsync(Guid id, Guid schoolId, string roomName, string reason)
		{
			var ended = (await _query.QueryAsync<int>(
				@"UPDATE LiveClass SET Status = 'Ended', EndedAt = @Now, EndReason = @Reason
				  WHERE Id = @Id AND SchoolId = @SchoolId AND Status = 'Live'; SELECT @@ROWCOUNT;",
				new Dictionary<string, object> { { "Now", DateTime.UtcNow }, { "Reason", reason }, { "Id", id }, { "SchoolId", schoolId } })).FirstOrDefault();

			if (ended == 0)
				return;

			_logger.Information("LiveClass: {LiveClassId} ended ({Reason})", id, reason);
			await _broadcaster.ClassEndedAsync(id, reason);
			await _liveKit.DeleteRoomAsync(roomName);
		}

		private async Task<BaseResponse?> CheckFeatureAsync(Guid schoolId)
		{
			if (!_settings.RequireSchoolFeature)
				return null;
			return await _features.IsFeatureEnabledAsync(schoolId, FeatureKeys.LiveClass)
				? null
				: Fail(ResponseCode.Forbidden, "Live classes are not switched on for your school yet");
		}

		private async Task<LiveClass?> GetRawAsync(Guid id, Guid schoolId)
			=> (await _query.QueryAsync<LiveClass>(
				"SELECT * FROM LiveClass WHERE Id = @Id AND SchoolId = @SchoolId AND IsActive = 1",
				new Dictionary<string, object> { { "Id", id }, { "SchoolId", schoolId } })).FirstOrDefault();

		private async Task<LiveClassDto?> LoadOneAsync(Guid id, Guid schoolId)
			=> (await LoadAsync("lc.Id = @Id AND lc.SchoolId = @SchoolId",
				new Dictionary<string, object> { { "Id", id }, { "SchoolId", schoolId } })).FirstOrDefault();

		private async Task<List<LiveClassDto>> LoadAsync(string where, Dictionary<string, object> parameters)
		{
			var classes = (await _query.QueryAsync<LiveClassDto>(
				$@"SELECT lc.Id, lc.LessonId, lc.Title, lc.ClassroomId, c.Name AS ClassroomName, lc.SubjectId, s.Subject AS SubjectName,
				          lc.TeacherId, (u.FirstName + ' ' + u.LastName) AS TeacherName, lc.Status, lc.DurationMinutes, lc.MaxStudents,
				          lc.BoardSessionId, lc.CreatedAt, lc.StartedAt, lc.EndsAt, lc.EndedAt, lc.EndReason
				   FROM LiveClass lc
				   LEFT JOIN Classroom c ON c.Id = lc.ClassroomId
				   LEFT JOIN Subjects  s ON s.Id = lc.SubjectId
				   LEFT JOIN Users     u ON u.Id = lc.TeacherId
				   WHERE lc.IsActive = 1 AND {where}
				   ORDER BY lc.CreatedAt DESC", parameters)).ToList();

			if (classes.Count == 0)
				return classes;

			var students = (await _query.QueryAsync<StudentRow>(
				$@"SELECT ls.LiveClassId, ls.StudentId, (u.FirstName + ' ' + u.LastName) AS Name, ls.FirstJoinedAt, ls.CanSpeak
				   FROM LiveClassStudent ls JOIN Users u ON u.Id = ls.StudentId
				   WHERE ls.LiveClassId IN ({GuidList(classes.Select(c => c.Id))})
				   ORDER BY u.FirstName", new Dictionary<string, object>())).ToList();

			foreach (var c in classes)
			{
				c.CreatedAt = Utc(c.CreatedAt);
				c.StartedAt = c.StartedAt.HasValue ? Utc(c.StartedAt.Value) : null;
				c.EndsAt = c.EndsAt.HasValue ? Utc(c.EndsAt.Value) : null;
				c.EndedAt = c.EndedAt.HasValue ? Utc(c.EndedAt.Value) : null;
				c.Students = students.Where(s => s.LiveClassId == c.Id)
					.Select(s => new LiveClassStudentDto
					{
						StudentId = s.StudentId, Name = s.Name,
						FirstJoinedAt = s.FirstJoinedAt.HasValue ? Utc(s.FirstJoinedAt.Value) : null,
						CanSpeak = s.CanSpeak
					}).ToList();
			}
			return classes;
		}

		private static bool TryCaller(AuthenticatedUserClaims? claims, out Guid userId, out Guid schoolId, out string role)
		{
			userId = schoolId = Guid.Empty;
			role = claims?.Role ?? string.Empty;
			return Guid.TryParse(claims?.UserId, out userId) && Guid.TryParse(claims?.SchoolId, out schoolId)
				&& userId != Guid.Empty && schoolId != Guid.Empty;
		}

		private static string GuidList(IEnumerable<Guid> ids)
		{
			var list = ids.Select(g => $"'{g:D}'").ToList();
			return list.Count == 0 ? $"'{Guid.Empty:D}'" : string.Join(",", list);
		}

		private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

		/// <summary>SQL datetime2 comes back with no kind; mark it UTC so clients parse it correctly.</summary>
		private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

		private static BaseResponse Ok(string message, object? data)
			=> new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = message, Status = "successful", Data = data! };

		private static BaseResponse Fail(string code, string message)
			=> new BaseResponse { ResponseCode = code, ResponseMessage = message, Status = "failed" };

		public class LessonRow
		{
			public Guid Id { get; set; }
			public Guid ClassroomId { get; set; }
			public Guid? SubjectId { get; set; }
			public string? Aim { get; set; }
			public string Status { get; set; } = string.Empty;
			public Guid CreatedBy { get; set; }
		}

		public class AccessRow
		{
			public string Status { get; set; } = string.Empty;
			public DateTime? EndsAt { get; set; }
			public bool IsTeacher { get; set; }
			public bool IsStudent { get; set; }
		}

		public class TimerRow
		{
			public Guid Id { get; set; }
			public Guid SchoolId { get; set; }
			public string RoomName { get; set; } = string.Empty;
			public DateTime EndsAt { get; set; }
			public bool Warned5 { get; set; }
			public bool Warned2 { get; set; }
		}

		public class StudentRow
		{
			public Guid LiveClassId { get; set; }
			public Guid StudentId { get; set; }
			public string Name { get; set; } = string.Empty;
			public DateTime? FirstJoinedAt { get; set; }
			public bool CanSpeak { get; set; }
		}

		#endregion
	}
}
