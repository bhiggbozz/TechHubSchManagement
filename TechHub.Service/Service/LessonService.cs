using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;
using MediaType = TechHub.Core.Model.MediaTypes;

namespace TechHub.Service.Service;

public class LessonService : ILessonService
{
	private readonly ICommandRespository<LessonContent> _lessonCommand;
	private readonly ICommandRespository<LessonMedia> _mediaCommand;
	private readonly IQueryRepository<LessonContent> _lessonQuery;
	private readonly IQueryRepository<LessonMedia> _mediaQuery;
	private readonly IQueryRepository<Users> _userQuery;
	private readonly IQueryRepository<Classroom> _classroomQuery;
	private readonly IQueryRepository<ApprovalRequests> _approvalQuery;
	private readonly IQueryRepository<StudentClassroom> _studentClassroomQuery;

	private readonly ICommandRespository<ApprovalRequests> _approvalCommand;
	private readonly IDbTransactionScopeFactory _scopeFactory;
	private readonly IEmailService _emailService;
	private readonly IConfiguration _configuration;	
	private readonly ILogger _logger;


	public LessonService(
	ICommandRespository<LessonContent> lessonCommand,
	ICommandRespository<LessonMedia> mediaCommand,
	IQueryRepository<LessonContent> lessonQuery,
	IQueryRepository<LessonMedia> mediaQuery,
	IQueryRepository<Users> userQuery,
	IQueryRepository<Classroom> classroomQuery,
	IQueryRepository<ApprovalRequests> approvalQuery,
	ICommandRespository<ApprovalRequests> approvalCommand,
	IQueryRepository<StudentClassroom> studentClassroomQuery,
	IDbTransactionScopeFactory scopeFactory,
	IEmailService emailService,
	IConfiguration configuration,
	ILogger logger)
	{
		_lessonCommand = lessonCommand;
		_mediaCommand = mediaCommand;
		_lessonQuery = lessonQuery;
		_mediaQuery = mediaQuery;
		_userQuery = userQuery;
		_classroomQuery = classroomQuery;
		_studentClassroomQuery = studentClassroomQuery;

		_approvalQuery = approvalQuery;
		_approvalCommand = approvalCommand;
		_scopeFactory = scopeFactory;
		_emailService = emailService;
		_configuration = configuration;
		_logger = logger;
	}

	// ── Submit lesson — saves to DB + fires approval simultaneously ──────────
	public async Task<BaseResponse> SubmitLesson(SubmitLessonViewModel model, AuthenticatedUserClaims claims)
	{
		try
		{
			// ===== VALIDATION =====

			if (model is null)
				return BadRequest("Lesson data cannot be empty");

			if (!Guid.TryParse(claims.UserId, out var teacherId))
				return Unauthorized();
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var userRole))
				return BadRequest("Invalid role in token");

			// Only teachers can submit lessons
			//if (userRole != UserRole.SubjectTeacher && userRole != UserRole.HeadTeacher)
			//{
			//	_logger.Warning(
			//		"Unauthorized lesson submission - UserId: {UserId}, Role: {Role}",
			//		teacherId, userRole);
			//	return Forbidden("Only teachers can submit lessons");
			//}

			// Validate teacher exists and is active
			var teacher = await _userQuery.Get(teacherId);
			if (teacher is null || !teacher.IsActive)
				return Forbidden("Your account is not active");

			// Validate classroom belongs to school
			var classroom = await _classroomQuery.Get(model.ClassroomId);
			if (classroom is null || classroom.SchoolId != schoolId)
				return NotFound("Classroom not found");

			//if (model.QuizId.HasValue)
			//{
			//	var quiz = await _quizQuery.Get(model.QuizId.Value);
			//	if (quiz is null)
			//		return BadRequest("Quiz not found in question bank");

			//	if (quiz.SchoolId != schoolId)
			//		return Forbidden("Quiz does not belong to your school");
			//}

			// Validate media files
			//if (!model.MediaFiles.Any())
			//	return BadRequest("At least one media file is required");
			if (!model.IsDraft)
			{
				if (!model.MediaFiles.Any())
					return BadRequest("At least one media file is required");

				foreach (var file in model.MediaFiles)
				{
					if (string.IsNullOrWhiteSpace(file.CloudinaryUrl) ||
						string.IsNullOrWhiteSpace(file.PublicId))
						return BadRequest(
							$"Invalid media file data for {file.OriginalFileName}");
				}
			}
			else
			{
				foreach (var file in model.MediaFiles)
				{
					if (string.IsNullOrWhiteSpace(file.CloudinaryUrl) ||
						string.IsNullOrWhiteSpace(file.PublicId))
						return BadRequest(
							$"Invalid media file data for {file.OriginalFileName}");
				}
			}

			
			var now = DateTime.UtcNow;
			var lessonId = Guid.NewGuid();

			DateTime? accessEndsAt = null;

			if (model.AccessDate.HasValue && model.DurationMinutes.HasValue)
			{
				var accessTime = model.AccessTime ?? TimeSpan.Zero;
				var accessStart = model.AccessDate.Value.Date + accessTime;
				accessEndsAt = accessStart.AddMinutes(model.DurationMinutes.Value);
			}

			// ===== BUILD LESSON DICT =====
			var lessonDict = new Dictionary<string, object>
			{
				{ "Id",          lessonId },
				{ "SchoolId",    schoolId },
				{ "ClassroomId", model.ClassroomId },
				{ "SubjectId",   model.SubjectId },
				{ "TopicId",     model.TopicId },
				{ "SubTopicId",     model.SubTopicId },
				{ "SubTopic",     string.Empty },
				{ "Aim",         model.Aim.Trim() },
				{ "Description", model.Description.Trim() },
				{ "Status",      LessonStatus.PendingApproval },
				{ "CreatedBy",   teacherId },
				{ "ApprovedBy",  DBNull.Value },
				{ "RejectedBy",  DBNull.Value },
				{ "RejectionReason", DBNull.Value },
				{ "CreatedAt",   now },
				{ "ModifiedAt",  now },
				{ "ApprovedAt",  DBNull.Value },
				{ "QuizId",  model.QuizId.HasValue ? (object)model.QuizId.Value : DBNull.Value },
				{ "AccessDate",      model.AccessDate.HasValue ? (object)model.AccessDate.Value.Date : DBNull.Value },
				{ "AccessTime",      model.AccessTime.HasValue ? (object)model.AccessTime.Value: DBNull.Value },
				{ "DurationMinutes", model.DurationMinutes.HasValue ? (object)model.DurationMinutes.Value : DBNull.Value },
				{ "AccessEndsAt",    accessEndsAt.HasValue ? (object)accessEndsAt.Value : DBNull.Value }
			};

			// ===== BUILD MEDIA DICTS =====
			var mediaDicts = model.MediaFiles.Select((file, index) =>
				new Dictionary<string, object>
				{
					{ "Id",               Guid.NewGuid() },
					{ "LessonContentId",  lessonId },
					{ "SchoolId",         schoolId },
					{ "FileName",         file.FileName },
					{ "OriginalFileName", file.OriginalFileName },
					{ "FileExtension",    file.FileExtension },
					{ "MediaType",        MediaType.Resolve(file.FileExtension) },
					{ "FileSizeBytes",    file.FileSizeBytes },
					{ "CloudinaryUrl",    file.CloudinaryUrl },
					{ "PublicId",         file.PublicId },
					{ "Duration",         file.Duration.HasValue
											  ? (object)file.Duration.Value
											  : null },
					{ "Status",           "Ready" },
					{ "DisplayOrder",     file.DisplayOrder > 0
											  ? file.DisplayOrder
											  : index + 1 },
					{ "CreatedAt",        now },
					{ "IsActive",         true },
					{ "MetaData",         file.MetaData },

				}).ToList();


			Guid approvalId;

			using var scope = _scopeFactory.Create("DbConnectionString");
			try
			{
				await _lessonCommand.Create(scope.Transaction, scope.Connection, lessonDict);

				await _mediaCommand.CreateBatchAsync(scope.Transaction, scope.Connection, mediaDicts);

				approvalId = Guid.NewGuid();
				var approverId = teacher.LineManagerId;

				if (!model.IsDraft && !model.BypassApproval && approverId.HasValue)
				{
					var expiryDays = int.Parse(_configuration["Approvals:ExpiryDays"] ?? "5");

					var approvalDict = new Dictionary<string, object>
					{
						{ "Id",            approvalId },
						{ "SchoolId",      schoolId },
						{ "RequestedBy",   teacherId },
						{ "ApproverId",    approverId.Value },
						{ "OperationType", OperationType.SubmitLesson },
						{ "EntityType",    "LessonContent" },
						{ "EntityId",      lessonId },
						{ "Payload",       System.Text.Json.JsonSerializer
											   .Serialize(model) },
						{ "Status",        ApprovalStatus.Pending },
						{ "RejectionReason", DBNull.Value },
						{ "CreatedAt",     now },
						{ "RespondedAt",   DBNull.Value },
						{ "ExpiresAt",     now.AddDays(expiryDays) },

					};
				

					//if (!model.IsDraft && !model.BypassApproval)
					//{
					//	var approverId = teacher.LineManager;

					//	if (approverId.HasValue)
					//	{
					//		approvalId = Guid.NewGuid();
					//		var expiryDays = int.Parse(
					//			_configuration["Approvals:ExpiryDays"] ?? "3");

					//		var approvalDict = new Dictionary<string, object>
					//		{
					//			{ "Id",              approvalId },
					//			{ "SchoolId",        schoolId },
					//			{ "RequestedBy",     teacherId },
					//			{ "ApproverId",      approverId.Value },
					//			{ "OperationType",   OperationType.SubmitLesson },
					//			{ "EntityType",      "LessonContent" },
					//			{ "EntityId",        lessonId },
					//			{ "Payload",         System.Text.Json.JsonSerializer
					//									 .Serialize(model) },
					//			{ "Status",          ApprovalStatus.Pending },
					//			{ "RejectionReason", DBNull.Value },
					//			{ "CreatedAt",       now },
					//			{ "RespondedAt",     DBNull.Value },
					//			{ "ExpiresAt",       now.AddDays(expiryDays) }
					//		};

					//		await _approvalCommand.Create(
					//			scope.Transaction, scope.Connection, approvalDict);
						
					//}

					await _approvalCommand.Create(
						scope.Transaction, scope.Connection, approvalDict);
				}

				await scope.CommitAsync();
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Rolling back lesson submission - TeacherId: {TeacherId}",
					teacherId);
				try { await scope.RollbackAsync(); }
				catch (Exception rbEx)
				{
					_logger.Error(rbEx, "Rollback failed - TeacherId: {TeacherId}", teacherId);
				}
				throw;
			}

			// ===== POST-COMMIT — notify approver (fire and forget) ===========
			if (!model.BypassApproval && teacher.LineManagerId.HasValue)
			{
				_ = Task.Run(async () =>
				{
					try
					{
						var approver = await _userQuery.Get(teacher.LineManagerId.Value);
						var placeholders = new Dictionary<string, string>
						{
							{ "@@Name",      $"{approver.FirstName} {approver.LastName}" },
							{ "@@TeacherName", $"{teacher.FirstName} {teacher.LastName}" },
							{ "@@SubTopic",  model.SubTopic },
							{ "@@Link",      _configuration["App:BaseUrl"] + "/approvals" }
						};

						var template = await _emailService.GetRenderedTemplate(
							(int)EmailTemplateKey.LessonApprovalRequest, placeholders);

						if (template != null)
							await _emailService.SendAsync(
								approver.EmailAddress,
								$"{approver.FirstName} {approver.LastName}",
								"Lesson Approval Required",
								template);
					}
					catch (Exception ex)
					{
						_logger.Error(ex,
							"Failed to send approval email - LessonId: {LessonId}",
							lessonId);
					}
				});
			}

			_logger.Information(
				"Lesson submitted - LessonId: {LessonId}, TeacherId: {TeacherId}, " +
				"MediaCount: {MediaCount}, BypassApproval: {Bypass}",
				lessonId, teacherId, mediaDicts.Count, model.BypassApproval);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = model.BypassApproval
					? "Lesson published successfully"
					: "Lesson submitted for approval",
				Status = "successful",
				Data = new
				{
					LessonId = lessonId,
					Status = model.BypassApproval
						? LessonStatus.Approved
						: LessonStatus.PendingApproval,
					MediaCount = mediaDicts.Count,
					ApprovalId = model.BypassApproval ? (Guid?)null : approvalId
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected error submitting lesson");
			return ServerError();
		}
	}

	public async Task<BaseResponse> GetLessonsByClassroom(Guid classroomId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			// Only return approved/published lessons to students
			// Teachers see their own pending ones too
			if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role))
				return BadRequest("Invalid role");

			var isTeacher = role == UserRole.SubjectTeacher ||
							role == UserRole.HeadTeacher ||
							role == UserRole.Administrator ||
							role == UserRole.SuperAdministrator;

			string query;

			if (isTeacher)
			{
				// Teachers see all lessons for the classroom
				query = $@"
                    SELECT lc.*, 
                           u.FirstName + ' ' + u.LastName AS TeacherName
                    FROM   LessonContent lc
                    JOIN   Users u ON u.Id = lc.CreatedBy
                    WHERE  lc.ClassroomId = '{classroomId}'
                    AND    lc.SchoolId    = '{schoolId}'
                    ORDER  BY lc.CreatedAt DESC";
			}
			else
			{
				// Students only see approved lessons
				query = $@"
                    SELECT lc.*,
                           u.FirstName + ' ' + u.LastName AS TeacherName
                    FROM   LessonContent lc
                    JOIN   Users u ON u.Id = lc.CreatedBy
                    WHERE  lc.ClassroomId = '{classroomId}'
                    AND    lc.SchoolId    = '{schoolId}'
                    AND    lc.Status      = '{LessonStatus.Approved}'
                    ORDER  BY lc.CreatedAt DESC";
			}

			var lessons = await _lessonQuery.GetByQuery(query);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Lessons retrieved successfully",
				Status = "successful",
				Data = lessons
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error fetching lessons - ClassroomId: {ClassroomId}", classroomId);
			return ServerError();
		}
	}

	// ── Get single lesson with all its media ──────────────────────────────────
	public async Task<BaseResponse> GetLessonById(Guid lessonId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			var lesson = await _lessonQuery.Get(lessonId);
			if (lesson is null || lesson.SchoolId != schoolId)
				return NotFound("Lesson not found");

			// Get all active media for this lesson
			var mediaQuery = $@"
                SELECT * FROM LessonMedia
                WHERE  LessonContentId = '{lessonId}'
                AND    IsActive        = 1
                ORDER  BY DisplayOrder ASC";

			var mediaFiles = await _mediaQuery.GetByQuery(mediaQuery);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Lesson retrieved successfully",
				Status = "successful",
				Data = new
				{
					Lesson = lesson,
					Media = mediaFiles
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error fetching lesson - LessonId: {LessonId}", lessonId);
			return ServerError();
		}
	}

	public async Task<BaseResponse> RespondToLesson(Guid lessonId, bool approved,string rejectionReason, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.UserId, out var approverId))
				return Unauthorized();
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			var lesson = await _lessonQuery.Get(lessonId);
			if (lesson is null || lesson.SchoolId != schoolId)
				return NotFound("Lesson not found");

			if (lesson.Status != LessonStatus.PendingApproval)
				return BadRequest($"Lesson is already {lesson.Status}");

			// Validate approver is the teacher's line manager
			var teacher = await _userQuery.Get(lesson.CreatedBy);
			if (teacher?.LineManagerId != approverId)
			{
				_logger.Warning(
					"Unauthorized lesson approval attempt - " +
					"LessonId: {LessonId}, AttemptedBy: {ApproverId}",
					lessonId, approverId);
				return Forbidden("You are not authorized to approve this lesson");
			}

			var now = DateTime.UtcNow;
			var updateDict = new Dictionary<string, object>
			{
				{ "Status",     approved
									? LessonStatus.Approved
									: LessonStatus.Rejected },
				{ "ModifiedAt", now }
			};

			if (approved)
			{
				updateDict["ApprovedBy"] = approverId;
				updateDict["ApprovedAt"] = now;
			}
			else
			{
				updateDict["RejectedBy"] = approverId;
				updateDict["RejectionReason"] = rejectionReason ?? string.Empty;
			}

			await _lessonCommand.UpdateTableColumnById(
				updateDict,
				new KeyValuePair<string, object>("Id", lessonId));

			// Also update the approval request record
			var approvalQuery = $@"
                SELECT * FROM ApprovalRequests
                WHERE  EntityId      = '{lessonId}'
                AND    OperationType = '{OperationType.SubmitLesson}'
                AND    Status        = '{ApprovalStatus.Pending}'";

			var approval = await _approvalQuery.Get(approvalQuery);
			if (approval != null)
			{
				await _approvalCommand.UpdateTableColumnById(
					new Dictionary<string, object>
					{
						{ "Status",      approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected },
						{ "RespondedAt", now },
						{ "RejectionReason", rejectionReason ?? string.Empty }
					},
					new KeyValuePair<string, object>("Id", approval.Id));
			}

			// Notify teacher — fire and forget
			_ = Task.Run(async () =>
			{
				try
				{
					var placeholders = new Dictionary<string, string>
					{
						{ "@@Name",    $"{teacher.FirstName} {teacher.LastName}" },
						{ "@@SubTopic", lesson.SubTopic },
						{ "@@Status",  approved ? "approved" : "rejected" },
						{ "@@Reason",  rejectionReason ?? string.Empty },
						{ "@@Link",    _configuration["App:BaseUrl"] + "/lessons" }
					};

					var template = await _emailService.GetRenderedTemplate(
						(int)EmailTemplateKey.LessonApprovalOutcome, placeholders);

					if (template != null)
						await _emailService.SendAsync(
							teacher.EmailAddress,
							$"{teacher.FirstName} {teacher.LastName}",
							approved ? "Lesson Approved" : "Lesson Rejected",
							template);
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Failed to send approval outcome email - LessonId: {LessonId}",
						lessonId);
				}
			});

			_logger.Information(
				"Lesson {Status} - LessonId: {LessonId}, By: {ApproverId}",
				approved ? "approved" : "rejected", lessonId, approverId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = approved
					? "Lesson approved and published to classroom"
					: "Lesson rejected",
				Status = "successful"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error responding to lesson - LessonId: {LessonId}", lessonId);
			return ServerError();
		}
	}

	public async Task<BaseResponse> GetPendingApprovals(AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.UserId, out var approverId))
				return Unauthorized();
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			var query = $@"
                SELECT lc.*,
                       u.FirstName + ' ' + u.LastName AS TeacherName,
                       u.EmailAddress                  AS TeacherEmail
                FROM   LessonContent lc
                JOIN   Users u ON u.Id = lc.CreatedBy
                WHERE  u.LineManagerId = '{approverId}'
                AND    lc.SchoolId     = '{schoolId}'
                AND    lc.Status       = '{LessonStatus.PendingApproval}'
                ORDER  BY lc.CreatedAt ASC";

			var pending = await _lessonQuery.GetByQuery(query);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Pending lessons retrieved",
				Status = "successful",
				Data = pending
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error fetching pending lessons");
			return ServerError();
		}
	}

	// ── Service method ───────────────────────────────────────────────────────────
	public async Task<BaseResponse> GetLessonsByTeacher(AuthenticatedUserClaims claims,string? status = null,int pageNumber = 1,int pageSize = 50)
	{
		try
		{
			if (!Guid.TryParse(claims.UserId, out var teacherId))
				return Unauthorized();

			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			if (pageNumber < 1) pageNumber = 1;
			if (pageSize < 1 || pageSize > 100) pageSize = 50;

			// Validate status if provided
			var validStatuses = new[]
			{
			//LessonStatus.Draft,
				LessonStatus.PendingApproval,
				LessonStatus.Approved,
				LessonStatus.Rejected,
				LessonStatus.Published
			};

			if (!string.IsNullOrWhiteSpace(status) &&
				!validStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
			{
				return BadRequest($"Invalid status. Valid values: {string.Join(", ", validStatuses)}");
			}

			// Single query — get everything then count in memory
			// Avoids multiple DB round trips
			var allQuery = $@"
					SELECT * FROM LessonContent
					WHERE  CreatedBy = '{teacherId}'
					AND    SchoolId  = '{schoolId}'
					ORDER  BY CreatedAt DESC";

			var allLessons = await _lessonQuery.GetByQuery(allQuery);
			var allList = allLessons.Where(l => l != null).ToList();

			var summary = new
			{
				Total = allList.Count,
				//Draft = allList.Count(l => l.Status == LessonStatus.Draft),
				PendingApproval = allList.Count(l => l.Status == LessonStatus.PendingApproval),
				Approved = allList.Count(l => l.Status == LessonStatus.Approved),
				Rejected = allList.Count(l => l.Status == LessonStatus.Rejected),
				Published = allList.Count(l => l.Status == LessonStatus.Published)
			};

			var filtered = string.IsNullOrWhiteSpace(status) ? allList : allList
					.Where(l => l.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();

			// ── Pagination ───────────────────────────────────────────────────────
			var totalCount = filtered.Count;
			var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

			var paginated = filtered
				.Skip((pageNumber - 1) * pageSize)
				.Take(pageSize)
				.ToList();

			_logger.Information(
				"Lessons fetched - TeacherId: {TeacherId}, Total: {Total}, " + "Filter: {Status}, Page: {Page}/{TotalPages}",
				teacherId, totalCount,
				status ?? "All",
				pageNumber, totalPages);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = paginated.Any()
					? $"{totalCount} lesson(s) found"
					: "No lessons found",
				Status = "successful",
				Data = new
				{
					Summary = summary,
					Lessons = paginated,
					TotalCount = totalCount,
					PageNumber = pageNumber,
					PageSize = pageSize,
					TotalPages = totalPages,
					HasPreviousPage = pageNumber > 1,
					HasNextPage = pageNumber < totalPages,
					FilteredBy = status ?? "All"
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,"Error fetching lessons - TeacherId: {TeacherId}",
				claims?.UserId);
			return ServerError();
		}
	}

	public async Task<BaseResponse> GetLessonForClass(Guid lessonId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.UserId, out var userId))
				return Unauthorized();
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			// Single query — pulls everything needed to start the class
			var lessonQuery = $@"
				SELECT
					lc.Id,
					lc.Aim,
					lc.Description,
					lc.Status,
					lc.CreatedAt,
					lc.ApprovedAt,
					lc.SubTopic,
					lc.SubTopicId,

					-- Classroom
					c.Id          AS ClassroomId,
					c.ClassName,

					-- Subject
					s.Id          AS SubjectId,
					s.Subject     AS SubjectName,

					-- Topic
					t.Id          AS TopicId,
					t.Name        AS TopicName,

					-- Teacher who created it
					u.Id          AS TeacherId,
					u.FirstName + ' ' + u.LastName AS TeacherName,
					u.EmailAddress AS TeacherEmail,

					-- Who approved it
					ap.FirstName + ' ' + ap.LastName AS ApprovedByName

				FROM   LessonContent lc
				JOIN   Classroom     c  ON c.Id  = lc.ClassroomId
				JOIN   Subjects      s  ON s.Id  = lc.SubjectId
				JOIN   Topic         t  ON t.Id  = lc.TopicId
				JOIN   Users         u  ON u.Id  = lc.CreatedBy
				LEFT JOIN Users      ap ON ap.Id = lc.ApprovedBy

				WHERE  lc.Id       = '{lessonId}'
				AND    lc.SchoolId = '{schoolId}'
				AND    lc.Status   = '{LessonStatus.Approved}'";

			var lessons = await _lessonQuery.QueryAsync<LessonForClassDto>(
				lessonQuery, new Dictionary<string, object>());

			var lesson = lessons.FirstOrDefault();
			if (lesson is null)
				return NotFound("Lesson not found or has not been approved yet");

			// Fetch all active media ordered for playback
			var mediaQuery = $@"
				SELECT
					Id,
					FileName,
					OriginalFileName,
					FileExtension,
					MediaType,
					CloudinaryUrl,
					PublicId,
					FileSizeBytes,
					Duration,
					DisplayOrder,
					MetaData
				FROM   LessonMedia
				WHERE  LessonContentId = '{lessonId}'
				AND    IsActive        = 1
				ORDER  BY DisplayOrder ASC";

			var media = await _mediaQuery.QueryAsync<LessonMediaDto>(mediaQuery, new Dictionary<string, object>());

			_logger.Information("Lesson loaded for class - LessonId: {LessonId}, UserId: {UserId}",lessonId, userId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Lesson loaded successfully",
				Status = "successful",
				Data = new
				{
					Lesson = lesson,
					Media = media.ToList(),
					MediaCount = media.Count()
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error loading lesson for class - LessonId: {LessonId}", lessonId);
			return ServerError();
		}
	}
	public async Task<BaseResponse> GetLessonsForStudent(Guid classroomId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.UserId, out var studentId))
				return Unauthorized();
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			// Verify student belongs to this classroom
			var membershipQuery = $@"
            SELECT TOP 1 Id FROM StudentClassroom
            WHERE  StudentId  = '{studentId}'
            AND    ClassroomId = '{classroomId}'
            AND    IsActive = 1
            AND    SchoolId    = '{schoolId}'";

			var membership = await _studentClassroomQuery.Get(membershipQuery);
			if (membership is null)
				return Forbidden("You are not enrolled in this classroom");

			// Fetch only Published lessons with subject and topic info
			var lessonsQuery = $@"
            SELECT
                lc.Id,
                lc.Aim,
                lc.Description,
                lc.Status,
                lc.CreatedAt,
                lc.ApprovedAt,
                lc.SubTopic,

                s.Id          AS SubjectId,
                s.Subject     AS SubjectName,

                t.Id          AS TopicId,
                t.Name        AS TopicName,

                u.FirstName + ' ' + u.LastName AS TeacherName,

                -- Media count
                (SELECT COUNT(*) FROM LessonMedia lm
                 WHERE lm.LessonContentId = lc.Id
                 AND   lm.IsActive = 1)    AS MediaCount

              

            FROM   LessonContent lc
            JOIN   Subjects      s  ON s.Id = lc.SubjectId
            JOIN   Topic         t  ON t.Id = lc.TopicId
            JOIN   Users         u  ON u.Id = lc.CreatedBy

            WHERE  lc.ClassroomId = '{classroomId}'
            AND    lc.SchoolId    = '{schoolId}'
            AND    lc.Status      = '{LessonStatus.Published}'

            ORDER  BY lc.ApprovedAt DESC";

			var rows = await _lessonQuery.QueryAsync<StudentLessonItemDto>(
				lessonsQuery, new Dictionary<string, object>());

			var lessons = rows.ToList();

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = lessons.Any()
					? $"{lessons.Count} lesson(s) found"
					: "No lessons available for this classroom",
				Status = "successful",
				Data = new
				{
					ClassroomId = classroomId,
					Count = lessons.Count,
					Lessons = lessons
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error fetching lessons for student - ClassroomId: {ClassroomId}, StudentId: {StudentId}",
				classroomId, claims?.UserId);
			return ServerError();
		}
	}

	public async Task<BaseResponse> GetLessonsByClassroomForAdmin(Guid classroomId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			// Verify classroom belongs to this school
			var classroom = await _classroomQuery.Get(classroomId);
			if (classroom is null || classroom.SchoolId != schoolId)
				return NotFound("Classroom not found");

			var lessonsQuery = $@"
            SELECT
                lc.Id,
                lc.Aim,
                lc.Description,
                lc.Status,
                lc.CreatedAt,
                lc.ApprovedAt,
                lc.SubTopic,
                lc.RejectionReason,

                s.Id          AS SubjectId,
                s.Subject     AS SubjectName,

                t.Id          AS TopicId,
                t.Name        AS TopicName,

                u.FirstName + ' ' + u.LastName  AS TeacherName,
                ap.FirstName + ' ' + ap.LastName AS ApprovedByName,

                (SELECT COUNT(*) FROM LessonMedia lm
                 WHERE lm.LessonContentId = lc.Id
                 AND   lm.IsActive = 1)    AS MediaCount

            FROM   LessonContent lc
            JOIN   Subjects      s   ON s.Id  = lc.SubjectId
            JOIN   Topic         t   ON t.Id  = lc.TopicId
            JOIN   Users         u   ON u.Id  = lc.CreatedBy
            LEFT JOIN Users      ap  ON ap.Id = lc.ApprovedBy

            WHERE  lc.ClassroomId = '{classroomId}'
            AND    lc.SchoolId    = '{schoolId}'

            ORDER  BY lc.CreatedAt DESC";

			var rows = await _lessonQuery.QueryAsync<AdminLessonItemDto>(
				lessonsQuery, new Dictionary<string, object>());

			var lessons = rows.ToList();

			// Status summary for admin dashboard
			var summary = new
			{
				Total = lessons.Count,
				Published = lessons.Count(l => l.Status == LessonStatus.Published),
				PendingApproval = lessons.Count(l => l.Status == LessonStatus.PendingApproval),
				Approved = lessons.Count(l => l.Status == LessonStatus.Approved),
				Rejected = lessons.Count(l => l.Status == LessonStatus.Rejected)
			};

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = lessons.Any()
					? $"{lessons.Count} lesson(s) found"
					: "No lessons found for this classroom",
				Status = "successful",
				Data = new
				{
					ClassroomId = classroomId,
					Summary = summary,
					Lessons = lessons
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error fetching lessons for admin - ClassroomId: {ClassroomId}",
				classroomId);
			return ServerError();
		}
	}

	//public async Task<BaseResponse> GetLessonManifestForStudent(string sessionId, AuthenticatedUserClaims claims)
	//{
	//	try
	//	{
	//		if (!Guid.TryParse(claims.UserId, out var studentId))
	//			return Unauthorized();
	//		if (!Guid.TryParse(claims.SchoolId, out var schoolId))
	//			return Unauthorized();

	//		// 1. Fetch the board session from MongoDB
	//		var session = await _repository.GetSessionAsync(sessionId, claims.SchoolId);
	//		if (session is null)
	//			return NotFound("Session not found");

	//		// 2. Verify the lesson exists and is Published
	//		var lessonQuery = $@"
 //           SELECT lc.Id, lc.ClassroomId, lc.Status, lc.SchoolId
 //           FROM   LessonContent lc
 //           WHERE  lc.Id       = '{session.LessonId}'
 //           AND    lc.SchoolId = '{schoolId}'
 //           AND    lc.Status   = '{LessonStatus.Published}'";

	//		var lesson = await _lessonQuery.Get(lessonQuery);
	//		if (lesson is null)
	//			return NotFound("Lesson not found or not yet published");

	//		// 3. Verify student is enrolled in the lesson's classroom
	//		var membershipQuery = $@"
 //           SELECT TOP 1 Id FROM StudentClassroom
 //           WHERE  StudentId   = '{studentId}'
 //           AND    ClassroomId = '{lesson.ClassroomId}'
 //           AND    SchoolId    = '{schoolId}'
 //           AND    IsActive    = 1";

	//		var membership = await _studentClassroomQuery.Get(membershipQuery);
	//		if (membership is null)
	//			return Forbidden("You are not enrolled in this classroom");

	//		_logger.Information(
	//			"Manifest fetched for student - SessionId: {SessionId}, StudentId: {StudentId}",
	//			sessionId, studentId);

	//		return new BaseResponse
	//		{
	//			ResponseCode = ResponseCode.successful,
	//			ResponseMessage = "Manifest retrieved successfully",
	//			Status = "successful",
	//			Data = session.Manifest
	//		};
	//	}
	//	catch (Exception ex)
	//	{
	//		_logger.Error(ex,
	//			"Error fetching manifest - SessionId: {SessionId}, StudentId: {StudentId}",
	//			sessionId, claims?.UserId);
	//		return ServerError();
	//	}
	//}

	private BaseResponse BadRequest(string message) => new BaseResponse
	{
		ResponseCode = ResponseCode.BadRequest,
		ResponseMessage = message,
		Status = "failed"
	};
	private BaseResponse Unauthorized() => new BaseResponse
	{
		ResponseCode = ResponseCode.Unauthorized,
		ResponseMessage = "Invalid authentication",
		Status = "failed"
	};
	private BaseResponse Forbidden(string message) => new BaseResponse
	{
		ResponseCode = ResponseCode.Forbidden,
		ResponseMessage = message,
		Status = "failed"
	};
	private BaseResponse NotFound(string message) => new BaseResponse
	{
		ResponseCode = ResponseCode.NotFound,
		ResponseMessage = message,
		Status = "failed"
	};
	private BaseResponse ServerError() => new BaseResponse
	{
		ResponseCode = ResponseCode.ErrorOccured,
		ResponseMessage = "An unexpected error occurred",
		Status = "failed"
	};
}

