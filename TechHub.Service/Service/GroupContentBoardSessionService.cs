using Serilog;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Messages;
using TechHub.Core.Model;
using TechHub.Core.ViewModels.Board;
using TechHub.Service.Interface;

namespace TechHub.Service.Service;

public class GroupContentBoardSessionService : IGroupContentBoardSessionService
{
	private readonly IQueryRepository<StudentGroup> _groupQuery;
	private readonly IQueryRepository<StudentGroupMember> _memberQuery;
	private readonly IQueryRepository<GroupLessonContent> _contentQuery;
	private readonly IQueryRepository<TeacherClassroom> _teacherClassroomQuery;
	private readonly IGroupContentBoardPublisherService _publisherService;
	private readonly IGroupContentBoardRepository _repository;
	private readonly ILogger _logger;

	public GroupContentBoardSessionService(
		IQueryRepository<StudentGroup> groupQuery,
		IQueryRepository<StudentGroupMember> memberQuery,
		IQueryRepository<GroupLessonContent> contentQuery,
		IQueryRepository<TeacherClassroom> teacherClassroomQuery,
		IGroupContentBoardPublisherService publisherService,
		IGroupContentBoardRepository repository,
		ILogger logger)
	{
		_groupQuery = groupQuery;
		_memberQuery = memberQuery;
		_contentQuery = contentQuery;
		_teacherClassroomQuery = teacherClassroomQuery;
		_publisherService = publisherService;
		_repository = repository;
		_logger = logger;
	}

	// ── Same approver resolution as GroupService.ResolveGroupApproverAsync ──────
	private async Task<Guid> ResolveGroupApproverAsync(Guid classroomId, Guid schoolId)
	{
		var classTeacherIds = await _teacherClassroomQuery.QueryAsync<Guid>(@"
			SELECT TOP 1 tc.TeacherId
			FROM TeacherClassroom tc
			JOIN Users u ON u.Id = tc.TeacherId
			WHERE tc.ClassroomId = @ClassroomId AND tc.SchoolId = @SchoolId AND tc.IsActive = 1
			AND u.RoleId = @ClassTeacherRoleId AND u.IsActive = 1",
			new Dictionary<string, object>
			{
				{ "ClassroomId", classroomId },
				{ "SchoolId", schoolId },
				{ "ClassTeacherRoleId", (int)TechHub.Core.Enum.UserRole.ClassTeacher }
			});

		var approverId = classTeacherIds.FirstOrDefault();
		if (approverId != Guid.Empty)
			return approverId;

		var headTeacherIds = await _teacherClassroomQuery.QueryAsync<Guid>(
			"SELECT TOP 1 Id FROM Users WHERE SchoolId = @SchoolId AND RoleId = @HeadTeacherRoleId AND IsActive = 1 ORDER BY CreationDate ASC",
			new Dictionary<string, object>
			{
				{ "SchoolId", schoolId },
				{ "HeadTeacherRoleId", (int)TechHub.Core.Enum.UserRole.HeadTeacher }
			});

		return headTeacherIds.FirstOrDefault();
	}

	// ── Shared view-access check for playback/download — creator or the
	// classroom's resolved approver can view any status; other group members
	// only once the student's submission is Approved. Resolves ONE specific
	// GroupLessonContent by Id (not "the latest row for this student in this
	// group") so permission checks can never be gated by the wrong submission's
	// status. ────────────────────────────────────────────────────────────────
	private async Task<(bool Ok, BaseResponse? Error, Guid SchoolId, GroupLessonContent? Content)> ValidateViewAccessAsync(
		string routeGroupId, string routeContentId, AuthenticatedUserClaims claims)
	{
		if (!Guid.TryParse(claims?.SchoolId, out var schoolId) || !Guid.TryParse(claims?.UserId, out var callerId))
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "Invalid user claims",
				Status = "failed"
			}, Guid.Empty, null);
		}

		if (!Guid.TryParse(routeGroupId, out var groupId) || !Guid.TryParse(routeContentId, out var contentId))
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.BadRequest,
				ResponseMessage = "Invalid GroupId or ContentId",
				Status = "failed"
			}, Guid.Empty, null);
		}

		var group = await _groupQuery.Get(groupId);
		if (group is null || group.SchoolId != schoolId || !group.IsActive)
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.NotFound,
				ResponseMessage = "Group not found",
				Status = "failed"
			}, Guid.Empty, null);
		}

		var content = await _contentQuery.Get(contentId);
		if (content is null || content.SchoolId != schoolId || content.GroupId != groupId)
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.NotFound,
				ResponseMessage = "Content not found",
				Status = "failed"
			}, Guid.Empty, null);
		}

		var isMember = group.CreatedBy == callerId || (await _memberQuery.CountAsync(
			"SELECT COUNT(*) FROM StudentGroupMember WHERE GroupId = @GroupId AND StudentId = @StudentId AND IsActive = 1",
			new Dictionary<string, object> { { "GroupId", groupId }, { "StudentId", callerId } })) > 0;

		var approverId = await ResolveGroupApproverAsync(group.ClassroomId, schoolId);
		var isApprover = approverId != Guid.Empty && approverId == callerId;

		if (!isMember && !isApprover)
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.Forbidden,
				ResponseMessage = "You do not have access to this content",
				Status = "failed"
			}, Guid.Empty, null);
		}

		var isSelf = callerId == content.CreatedBy;
		var isApproved = string.Equals(content.Status, "Approved", StringComparison.OrdinalIgnoreCase);

		if (!isSelf && !isApprover && !isApproved)
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.Forbidden,
				ResponseMessage = "This content is awaiting approval",
				Status = "failed"
			}, Guid.Empty, null);
		}

		return (true, null, schoolId, content);
	}

	// ── Shared membership + content-ownership check — batch/manifest/status
	// calls all need it. Resolves ONE specific GroupLessonContent (owned by
	// the caller) instead of the group's "latest" submission, so a recording
	// session is always tied to exactly the submission the frontend intends. ──
	private async Task<(bool Ok, BaseResponse? Error, Guid SchoolId, Guid StudentId, GroupLessonContent? Content)> ValidateOwnContentAsync(
		string routeGroupId, string routeContentId, AuthenticatedUserClaims claims)
	{
		if (!Guid.TryParse(claims?.SchoolId, out var schoolId) || !Guid.TryParse(claims?.UserId, out var studentId))
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "Invalid user claims",
				Status = "failed"
			}, Guid.Empty, Guid.Empty, null);
		}

		if (!Guid.TryParse(routeGroupId, out var groupId) || !Guid.TryParse(routeContentId, out var contentId))
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.BadRequest,
				ResponseMessage = "Invalid GroupId or ContentId",
				Status = "failed"
			}, Guid.Empty, Guid.Empty, null);
		}

		var group = await _groupQuery.Get(groupId);
		if (group is null || group.SchoolId != schoolId || !group.IsActive)
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.NotFound,
				ResponseMessage = "Group not found",
				Status = "failed"
			}, Guid.Empty, Guid.Empty, null);
		}

		var isMember = group.CreatedBy == studentId || (await _memberQuery.CountAsync(
			"SELECT COUNT(*) FROM StudentGroupMember WHERE GroupId = @GroupId AND StudentId = @StudentId AND IsActive = 1",
			new Dictionary<string, object> { { "GroupId", groupId }, { "StudentId", studentId } })) > 0;

		if (!isMember)
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.Forbidden,
				ResponseMessage = "You are not a member of this group",
				Status = "failed"
			}, Guid.Empty, Guid.Empty, null);
		}

		var content = await _contentQuery.Get(contentId);
		if (content is null || content.SchoolId != schoolId || content.GroupId != groupId)
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.NotFound,
				ResponseMessage = "Content not found",
				Status = "failed"
			}, Guid.Empty, Guid.Empty, null);
		}

		if (content.CreatedBy != studentId)
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.Forbidden,
				ResponseMessage = "You can only record for your own content",
				Status = "failed"
			}, Guid.Empty, Guid.Empty, null);
		}

		return (true, null, schoolId, studentId, content);
	}

	public async Task<BaseResponse> PublishBatchAsync(string routeGroupId, string routeContentId, GroupContentBoardBatchViewModel model, AuthenticatedUserClaims claims)
	{
		try
		{
			if (routeGroupId != model.GroupId)
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = $"Group ID in route ({routeGroupId}) does not match body ({model.GroupId})",
					Status = "failed"
				};
			}

			if (routeContentId != model.ContentId)
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = $"Content ID in route ({routeContentId}) does not match body ({model.ContentId})",
					Status = "failed"
				};
			}

			var (ok, error, schoolId, studentId, content) = await ValidateOwnContentAsync(routeGroupId, routeContentId, claims);
			if (!ok) return error!;

			if (!string.Equals(content!.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.Forbidden,
					ResponseMessage = $"This content has already been {content.Status} — its recording can no longer be changed",
					Status = "failed"
				};
			}

			var message = GroupContentBatchMessage.FromViewModel(model, schoolId.ToString(), studentId.ToString());

			await _publisherService.PublishBatchAsync(message);

			_logger.Information(
				"Published group-content batch {BatchIndex} for Group: {GroupId}, Student: {StudentId}, Content: {ContentId}",
				model.BatchIndex, model.GroupId, studentId, model.ContentId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Batch accepted",
				Status = "successful"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected error publishing group-content batch - GroupId: {GroupId}, ContentId: {ContentId}", routeGroupId, routeContentId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An unexpected error occurred",
				Status = "failed"
			};
		}
	}

	public async Task<BaseResponse> SaveManifestAsync(string routeGroupId, string routeContentId, GroupContentManifestViewModel model, AuthenticatedUserClaims claims)
	{
		try
		{
			if (routeGroupId != model.GroupId)
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = $"Group ID in route ({routeGroupId}) does not match body ({model.GroupId})",
					Status = "failed"
				};
			}

			if (routeContentId != model.ContentId)
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = $"Content ID in route ({routeContentId}) does not match body ({model.ContentId})",
					Status = "failed"
				};
			}

			var (ok, error, schoolId, studentId, content) = await ValidateOwnContentAsync(routeGroupId, routeContentId, claims);
			if (!ok) return error!;

			if (!string.Equals(content!.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.Forbidden,
					ResponseMessage = $"This content has already been {content.Status} — its recording can no longer be changed",
					Status = "failed"
				};
			}

			await _repository.SaveManifestAsync(model.GroupId, studentId.ToString(), model.ContentId, schoolId.ToString(), model);

			_logger.Information(
				"Saved group-content manifest for Group: {GroupId}, Student: {StudentId}, Content: {ContentId}",
				model.GroupId, studentId, model.ContentId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Manifest saved",
				Status = "successful"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected error saving group-content manifest - GroupId: {GroupId}, ContentId: {ContentId}", routeGroupId, routeContentId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An unexpected error occurred",
				Status = "failed"
			};
		}
	}

	// ── Tells the frontend: resume the paused recording, this content already
	// has one, or start fresh. Scoped to ONE specific content item — a decided
	// (Approved/Rejected) submission short-circuits with its own status. ──────
	public async Task<BaseResponse> GetStatusAsync(string routeGroupId, string routeContentId, AuthenticatedUserClaims claims)
	{
		try
		{
			var (ok, error, _, studentId, content) = await ValidateOwnContentAsync(routeGroupId, routeContentId, claims);
			if (!ok) return error!;

			if (!string.Equals(content!.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Content status retrieved",
					Status = "successful",
					Data = new GroupContentStatusDto
					{
						Status = content.Status,
						ContentId = content.Id,
						RejectionReason = content.RejectionReason
					}
				};
			}

			// Still pending review — check whether this specific content already
			// has a finished recording, one mid-flight, or none at all.
			var manifest = await _repository.GetManifestAsync(routeGroupId, studentId.ToString(), routeContentId);
			if (manifest is not null)
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Content status retrieved",
					Status = "successful",
					Data = new GroupContentStatusDto
					{
						Status = "Recorded",
						ContentId = content.Id,
						HasBoardRecording = true,
						HasManifest = true,
						LastBatchIndex = manifest.StrokeBatches.Any() ? manifest.StrokeBatches.Max(b => b.BatchIndex) : null
					}
				};
			}

			var lastBatchIndex = await _repository.GetLatestBatchIndexAsync(routeGroupId, studentId.ToString(), routeContentId);
			if (lastBatchIndex.HasValue)
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Content status retrieved",
					Status = "successful",
					Data = new GroupContentStatusDto
					{
						Status = "RecordingInProgress",
						ContentId = content.Id,
						HasBoardRecording = true,
						LastBatchIndex = lastBatchIndex
					}
				};
			}

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Content status retrieved",
				Status = "successful",
				Data = new GroupContentStatusDto { Status = "NoActiveContent", ContentId = content.Id }
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected error fetching group-content status - GroupId: {GroupId}, ContentId: {ContentId}", routeGroupId, routeContentId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An unexpected error occurred",
				Status = "failed"
			};
		}
	}

	public async Task<BaseResponse> GetManifestForViewAsync(string routeGroupId, string routeContentId, AuthenticatedUserClaims claims)
	{
		try
		{
			var (ok, error, _, content) = await ValidateViewAccessAsync(routeGroupId, routeContentId, claims);
			if (!ok) return error!;

			var manifest = await _repository.GetManifestAsync(routeGroupId, content!.CreatedBy.ToString(), routeContentId);
			if (manifest is null)
				return new BaseResponse
				{
					ResponseCode = ResponseCode.NotFound,
					ResponseMessage = "No recording found for this content",
					Status = "failed"
				};

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Manifest retrieved",
				Status = "successful",
				Data = manifest
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected error fetching group-content manifest - GroupId: {GroupId}, ContentId: {ContentId}", routeGroupId, routeContentId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An unexpected error occurred",
				Status = "failed"
			};
		}
	}

	public async Task<BaseResponse> GetBatchForViewAsync(string routeGroupId, string routeContentId, int batchIndex, AuthenticatedUserClaims claims)
	{
		try
		{
			var (ok, error, _, content) = await ValidateViewAccessAsync(routeGroupId, routeContentId, claims);
			if (!ok) return error!;

			var batch = await _repository.GetBatchAsync(routeGroupId, content!.CreatedBy.ToString(), routeContentId, batchIndex);
			if (batch is null)
				return new BaseResponse
				{
					ResponseCode = ResponseCode.NotFound,
					ResponseMessage = "Batch not found",
					Status = "failed"
				};

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Batch retrieved",
				Status = "successful",
				Data = batch
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected error fetching group-content batch - GroupId: {GroupId}, ContentId: {ContentId}, BatchIndex: {BatchIndex}", routeGroupId, routeContentId, batchIndex);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An unexpected error occurred",
				Status = "failed"
			};
		}
	}
}
