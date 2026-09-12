using Dapper;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.Service.Service
{
	public class NotificationService : INotificationService
	{
		private readonly IQueryRepository<Notification> _notificationQuery;
		private readonly ICommandRespository<Notification> _notificationCommand;
		private readonly IDbTransactionScopeFactory _scopeFactory;
		private readonly ILogger _logger;

		public NotificationService(
			IQueryRepository<Notification> notificationQuery,
			ICommandRespository<Notification> notificationCommand,
			IDbTransactionScopeFactory scopeFactory,
			ILogger logger)
		{
			_notificationQuery = notificationQuery;
			_notificationCommand = notificationCommand;
			_scopeFactory = scopeFactory;
			_logger = logger;
		}

		// ── Recipient-facing ──────────────────────────────────────────────

		public async Task<BaseResponse> GetMyNotificationsAsync(AuthenticatedUserClaims claims, int page, int pageSize)
		{
			try
			{
				if (!Guid.TryParse(claims?.UserId, out var userId) || !Guid.TryParse(claims?.SchoolId, out var schoolId))
					return Fail(ResponseCode.Unauthorized, "Invalid authentication");

				page = page < 1 ? 1 : page;
				pageSize = pageSize < 1 || pageSize > 100 ? 20 : pageSize;
				var offset = (page - 1) * pageSize;

				var items = await _notificationQuery.QueryAsync<Notification>(
					@"SELECT * FROM Notification
					  WHERE RecipientId = @RecipientId AND SchoolId = @SchoolId
					  ORDER BY CreatedAt DESC
					  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
					new Dictionary<string, object>
					{
						{ "RecipientId", userId },
						{ "SchoolId", schoolId },
						{ "Offset", offset },
						{ "PageSize", pageSize }
					});

				var unreadCount = await _notificationQuery.CountAsync(
					"SELECT COUNT(*) FROM Notification WHERE RecipientId = @RecipientId AND SchoolId = @SchoolId AND IsRead = 0",
					new Dictionary<string, object> { { "RecipientId", userId }, { "SchoolId", schoolId } });

				var dto = new NotificationListDto
				{
					Items = items.Select(n => new NotificationDto
					{
						Id = n.Id,
						Type = n.Type,
						Title = n.Title,
						Body = n.Body,
						EntityType = n.EntityType,
						EntityId = n.EntityId,
						IsRead = n.IsRead,
						IsDelivered = n.IsDelivered,
						CreatedAt = n.CreatedAt
					}).ToList(),
					UnreadCount = unreadCount
				};

				return Success("Notifications retrieved", dto);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to retrieve notifications - UserId: {UserId}", claims?.UserId);
				return Fail(ResponseCode.ErrorOccured, "An unexpected error occurred");
			}
		}

		public async Task<BaseResponse> MarkDeliveredAsync(List<Guid> notificationIds, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims?.UserId, out var userId))
					return Fail(ResponseCode.Unauthorized, "Invalid authentication");

				if (notificationIds is null || notificationIds.Count == 0)
					return Success("Nothing to mark delivered", null);

				using var scope = _scopeFactory.Create("DbConnectionString");
				await scope.Connection.ExecuteAsync(
					"UPDATE Notification SET IsDelivered = 1 WHERE RecipientId = @RecipientId AND Id IN @Ids",
					new { RecipientId = userId, Ids = notificationIds }, scope.Transaction);
				await scope.CommitAsync();

				return Success("Marked delivered", null);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to mark notifications delivered - UserId: {UserId}", claims?.UserId);
				return Fail(ResponseCode.ErrorOccured, "An unexpected error occurred");
			}
		}

		public async Task<BaseResponse> MarkReadAsync(Guid notificationId, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims?.UserId, out var userId))
					return Fail(ResponseCode.Unauthorized, "Invalid authentication");

				using var scope = _scopeFactory.Create("DbConnectionString");
				await scope.Connection.ExecuteAsync(
					"UPDATE Notification SET IsRead = 1, ReadAt = GETUTCDATE() WHERE Id = @Id AND RecipientId = @RecipientId",
					new { Id = notificationId, RecipientId = userId }, scope.Transaction);
				await scope.CommitAsync();

				return Success("Marked read", null);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to mark notification read - NotificationId: {Id}", notificationId);
				return Fail(ResponseCode.ErrorOccured, "An unexpected error occurred");
			}
		}

		public async Task<BaseResponse> MarkAllReadAsync(AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims?.UserId, out var userId) || !Guid.TryParse(claims?.SchoolId, out var schoolId))
					return Fail(ResponseCode.Unauthorized, "Invalid authentication");

				using var scope = _scopeFactory.Create("DbConnectionString");
				await scope.Connection.ExecuteAsync(
					"UPDATE Notification SET IsRead = 1, ReadAt = GETUTCDATE() WHERE RecipientId = @RecipientId AND SchoolId = @SchoolId AND IsRead = 0",
					new { RecipientId = userId, SchoolId = schoolId }, scope.Transaction);
				await scope.CommitAsync();

				return Success("All notifications marked read", null);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to mark all notifications read - UserId: {UserId}", claims?.UserId);
				return Fail(ResponseCode.ErrorOccured, "An unexpected error occurred");
			}
		}

		// ── Fan-out triggers — each resolves its own recipients and never
		// throws; the operation that triggered these already succeeded and
		// must not be failed by a notification-side error. ─────────────────

		public async Task NotifyLessonPublishedAsync(Guid lessonId, Guid schoolId)
		{
			try
			{
				var lesson = (await _notificationQuery.QueryAsync<LessonLookupRow>(
					"SELECT ClassroomId, Aim FROM LessonContent WHERE Id = @Id AND SchoolId = @SchoolId",
					new Dictionary<string, object> { { "Id", lessonId }, { "SchoolId", schoolId } })).FirstOrDefault();

				if (lesson is null) return;

				var studentIds = (await _notificationQuery.QueryAsync<Guid>(
					"SELECT StudentId FROM StudentClassroom WHERE ClassroomId = @ClassroomId AND SchoolId = @SchoolId AND IsActive = 1",
					new Dictionary<string, object> { { "ClassroomId", lesson.ClassroomId }, { "SchoolId", schoolId } })).ToList();

				await FanOutAsync(
					studentIds, schoolId, NotificationTypes.LessonPublished,
					"New lesson available", $"A new lesson has been published: \"{lesson.Aim}\"",
					"Lesson", lessonId);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to notify lesson published - LessonId: {LessonId}", lessonId);
			}
		}

		public async Task NotifyAssessmentAssignedAsync(Guid assessmentId, string targetType, List<Guid> targetIds, Guid schoolId)
		{
			try
			{
				var assessmentTitle = (await _notificationQuery.QueryAsync<string>(
					"SELECT TOP 1 Title FROM Assessments WHERE Id = @Id",
					new Dictionary<string, object> { { "Id", assessmentId } })).FirstOrDefault() ?? "an assessment";

				var studentIds = new HashSet<Guid>();
				foreach (var targetId in targetIds ?? new List<Guid>())
				{
					foreach (var id in await ResolveAssessmentTargetStudentIdsAsync(targetType, targetId, schoolId))
						studentIds.Add(id);
				}

				await FanOutAsync(
					studentIds.ToList(), schoolId, NotificationTypes.AssessmentAssigned,
					"New assessment assigned", $"You have a new assessment: \"{assessmentTitle}\"",
					"Assessment", assessmentId);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to notify assessment assigned - AssessmentId: {AssessmentId}", assessmentId);
			}
		}

		public async Task NotifyGroupContentSubmittedAsync(Guid contentId, Guid approverId, Guid schoolId, string aim, string groupName)
		{
			try
			{
				await FanOutAsync(
					new List<Guid> { approverId }, schoolId, NotificationTypes.GroupContentSubmitted,
					"Study group content awaiting approval",
					$"New content submitted in \"{groupName}\": \"{aim}\"",
					"GroupLessonContent", contentId);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to notify group content submitted - ContentId: {ContentId}", contentId);
			}
		}

		public async Task NotifyGroupContentApprovedAsync(Guid contentId, Guid schoolId)
		{
			try
			{
				var content = (await _notificationQuery.QueryAsync<GroupContentLookupRow>(
					"SELECT GroupId, Aim, CreatedBy FROM GroupLessonContent WHERE Id = @Id AND SchoolId = @SchoolId",
					new Dictionary<string, object> { { "Id", contentId }, { "SchoolId", schoolId } })).FirstOrDefault();

				if (content is null) return;

				var memberIds = (await _notificationQuery.QueryAsync<Guid>(
					"SELECT StudentId FROM StudentGroupMember WHERE GroupId = @GroupId AND IsActive = 1",
					new Dictionary<string, object> { { "GroupId", content.GroupId } })).ToHashSet();

				// The submitter already knows their own content was approved
				memberIds.Remove(content.CreatedBy);

				await FanOutAsync(
					memberIds.ToList(), schoolId, NotificationTypes.GroupContentApproved,
					"New study group content", $"New content dropped in your study group: \"{content.Aim}\"",
					"GroupLessonContent", contentId);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to notify group content approved - ContentId: {ContentId}", contentId);
			}
		}

		// ── Shared helpers ────────────────────────────────────────────────

		private async Task<List<Guid>> ResolveAssessmentTargetStudentIdsAsync(string targetType, Guid targetId, Guid schoolId)
		{
			switch (targetType)
			{
				case "Student":
					return new List<Guid> { targetId };

				case "Classroom":
					return (await _notificationQuery.QueryAsync<Guid>(
						"SELECT StudentId FROM StudentClassroom WHERE ClassroomId = @ClassroomId AND SchoolId = @SchoolId AND IsActive = 1",
						new Dictionary<string, object> { { "ClassroomId", targetId }, { "SchoolId", schoolId } })).ToList();

				case "Subject":
					return (await _notificationQuery.QueryAsync<Guid>(
						@"SELECT sc.StudentId
						  FROM StudentClassroom sc
						  JOIN ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId AND cs.IsActive = 1
						  WHERE cs.SubjectId = @SubjectId AND sc.IsActive = 1 AND sc.SchoolId = @SchoolId
						  UNION
						  SELECT sms.StudentId FROM StudentMinorSubject sms
						  WHERE sms.SubjectId = @SubjectId AND sms.IsActive = 1 AND sms.SchoolId = @SchoolId",
						new Dictionary<string, object> { { "SubjectId", targetId }, { "SchoolId", schoolId } })).ToList();

				default:
					return new List<Guid>();
			}
		}

		private async Task FanOutAsync(List<Guid> recipientIds, Guid schoolId, string type, string title, string body, string entityType, Guid entityId)
		{
			if (recipientIds is null || recipientIds.Count == 0) return;

			var now = DateTime.UtcNow;
			var dicts = recipientIds.Distinct().Select(recipientId => new Dictionary<string, object>
			{
				{ "Id", Guid.NewGuid() },
				{ "SchoolId", schoolId },
				{ "RecipientId", recipientId },
				{ "Type", type },
				{ "Title", title },
				{ "Body", body },
				{ "EntityType", entityType },
				{ "EntityId", entityId },
				{ "IsDelivered", false },
				{ "IsRead", false },
				{ "CreatedAt", now },
				{ "ReadAt", DBNull.Value }
			}).ToList();

			using var scope = _scopeFactory.Create("DbConnectionString");
			try
			{
				await _notificationCommand.CreateBatchAsync(scope.Transaction, scope.Connection, dicts);
				await scope.CommitAsync();
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to fan out notifications - Type: {Type}, EntityId: {EntityId}", type, entityId);
				try { await scope.RollbackAsync(); } catch { }
			}
		}

		private class LessonLookupRow
		{
			public Guid ClassroomId { get; set; }
			public string Aim { get; set; } = string.Empty;
		}

		private class GroupContentLookupRow
		{
			public Guid GroupId { get; set; }
			public string Aim { get; set; } = string.Empty;
			public Guid CreatedBy { get; set; }
		}

		private static BaseResponse Success(string message, object? data) => new()
		{
			ResponseCode = ResponseCode.successful,
			ResponseMessage = message,
			Status = "successful",
			Data = data
		};

		private static BaseResponse Fail(string code, string message) => new()
		{
			ResponseCode = code,
			ResponseMessage = message,
			Status = "failed",
			Data = null
		};
	}
}
