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
	// only once the student's submission is Approved ───────────────────────────
	private async Task<(bool Ok, BaseResponse? Error, Guid SchoolId)> ValidateViewAccessAsync(
		string routeGroupId, string targetStudentId, AuthenticatedUserClaims claims)
	{
		if (!Guid.TryParse(claims?.SchoolId, out var schoolId) || !Guid.TryParse(claims?.UserId, out var callerId))
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "Invalid user claims",
				Status = "failed"
			}, Guid.Empty);
		}

		if (!Guid.TryParse(routeGroupId, out var groupId) || !Guid.TryParse(targetStudentId, out var targetId))
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.BadRequest,
				ResponseMessage = "Invalid GroupId or StudentId",
				Status = "failed"
			}, Guid.Empty);
		}

		var group = await _groupQuery.Get(groupId);
		if (group is null || group.SchoolId != schoolId || !group.IsActive)
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.NotFound,
				ResponseMessage = "Group not found",
				Status = "failed"
			}, Guid.Empty);
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
			}, Guid.Empty);
		}

		var isSelf = callerId == targetId;

		if (!isSelf && !isApprover)
		{
			var existing = (await _contentQuery.QueryAsync<GroupLessonContent>(
				"SELECT TOP 1 * FROM GroupLessonContent WHERE GroupId = @GroupId AND CreatedBy = @StudentId ORDER BY CreatedAt DESC",
				new Dictionary<string, object> { { "GroupId", groupId }, { "StudentId", targetId } }))
				.FirstOrDefault();

			var isApproved = existing is not null && string.Equals(existing.Status, "Approved", StringComparison.OrdinalIgnoreCase);

			if (!isApproved)
			{
				return (false, new BaseResponse
				{
					ResponseCode = ResponseCode.Forbidden,
					ResponseMessage = "This content is awaiting approval",
					Status = "failed"
				}, Guid.Empty);
			}
		}

		return (true, null, schoolId);
	}

	// ── Shared membership check — both batch and manifest calls need it ─────────
	private async Task<(bool Ok, BaseResponse? Error, Guid SchoolId, Guid StudentId)> ValidateMemberAsync(
		string modelGroupId, AuthenticatedUserClaims claims)
	{
		if (!Guid.TryParse(claims?.SchoolId, out var schoolId) || !Guid.TryParse(claims?.UserId, out var studentId))
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "Invalid user claims",
				Status = "failed"
			}, Guid.Empty, Guid.Empty);
		}

		if (!Guid.TryParse(modelGroupId, out var groupId))
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.BadRequest,
				ResponseMessage = "Invalid GroupId",
				Status = "failed"
			}, Guid.Empty, Guid.Empty);
		}

		var group = await _groupQuery.Get(groupId);
		if (group is null || group.SchoolId != schoolId || !group.IsActive)
		{
			return (false, new BaseResponse
			{
				ResponseCode = ResponseCode.NotFound,
				ResponseMessage = "Group not found",
				Status = "failed"
			}, Guid.Empty, Guid.Empty);
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
			}, Guid.Empty, Guid.Empty);
		}

		return (true, null, schoolId, studentId);
	}

	public async Task<BaseResponse> PublishBatchAsync(string routeGroupId, GroupContentBoardBatchViewModel model, AuthenticatedUserClaims claims)
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

			var (ok, error, schoolId, studentId) = await ValidateMemberAsync(model.GroupId, claims);
			if (!ok) return error!;

			var message = GroupContentBatchMessage.FromViewModel(model, schoolId.ToString(), studentId.ToString());

			await _publisherService.PublishBatchAsync(message);

			_logger.Information(
				"Published group-content batch {BatchIndex} for Group: {GroupId}, Student: {StudentId}",
				model.BatchIndex, model.GroupId, studentId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Batch accepted",
				Status = "successful"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected error publishing group-content batch - GroupId: {GroupId}", routeGroupId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An unexpected error occurred",
				Status = "failed"
			};
		}
	}

	public async Task<BaseResponse> SaveManifestAsync(string routeGroupId, GroupContentManifestViewModel model, AuthenticatedUserClaims claims)
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

			var (ok, error, schoolId, studentId) = await ValidateMemberAsync(model.GroupId, claims);
			if (!ok) return error!;

			await _repository.SaveManifestAsync(model.GroupId, studentId.ToString(), schoolId.ToString(), model);

			_logger.Information(
				"Saved group-content manifest for Group: {GroupId}, Student: {StudentId}",
				model.GroupId, studentId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Manifest saved",
				Status = "successful"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected error saving group-content manifest - GroupId: {GroupId}", routeGroupId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An unexpected error occurred",
				Status = "failed"
			};
		}
	}

	// ── Tells the frontend: resume the paused recording, wait on approval, or start fresh ──
	public async Task<BaseResponse> GetStatusAsync(string routeGroupId, AuthenticatedUserClaims claims)
	{
		try
		{
			var (ok, error, _, studentId) = await ValidateMemberAsync(routeGroupId, claims);
			if (!ok) return error!;

			var groupId = Guid.Parse(routeGroupId);

			// One finalized submission is authoritative — the recording slot for
			// this group+student has already been used, no matter its outcome.
			var existing = (await _contentQuery.QueryAsync<GroupLessonContent>(
				"SELECT TOP 1 * FROM GroupLessonContent WHERE GroupId = @GroupId AND CreatedBy = @StudentId ORDER BY CreatedAt DESC",
				new Dictionary<string, object> { { "GroupId", groupId }, { "StudentId", studentId } }))
				.FirstOrDefault();

			if (existing is not null)
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Content status retrieved",
					Status = "successful",
					Data = new GroupContentStatusDto
					{
						Status = existing.Status,
						ContentId = existing.Id,
						RejectionReason = existing.RejectionReason
					}
				};
			}

			// No SQL row yet — check whether a recording is mid-flight in Mongo.
			var manifest = await _repository.GetManifestAsync(routeGroupId, studentId.ToString());
			if (manifest is not null)
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Content status retrieved",
					Status = "successful",
					Data = new GroupContentStatusDto
					{
						Status = "AwaitingSubmission",
						HasBoardRecording = true,
						HasManifest = true,
						LastBatchIndex = manifest.StrokeBatches.Any() ? manifest.StrokeBatches.Max(b => b.BatchIndex) : null
					}
				};
			}

			var lastBatchIndex = await _repository.GetLatestBatchIndexAsync(routeGroupId, studentId.ToString());
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
				Data = new GroupContentStatusDto { Status = "NoActiveContent" }
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected error fetching group-content status - GroupId: {GroupId}", routeGroupId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An unexpected error occurred",
				Status = "failed"
			};
		}
	}

	public async Task<BaseResponse> GetManifestForViewAsync(string routeGroupId, string targetStudentId, AuthenticatedUserClaims claims)
	{
		try
		{
			var (ok, error, _) = await ValidateViewAccessAsync(routeGroupId, targetStudentId, claims);
			if (!ok) return error!;

			var manifest = await _repository.GetManifestAsync(routeGroupId, targetStudentId);
			if (manifest is null)
				return new BaseResponse
				{
					ResponseCode = ResponseCode.NotFound,
					ResponseMessage = "No recording found for this student in this group",
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
			_logger.Error(ex, "Unexpected error fetching group-content manifest - GroupId: {GroupId}, StudentId: {StudentId}", routeGroupId, targetStudentId);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An unexpected error occurred",
				Status = "failed"
			};
		}
	}

	public async Task<BaseResponse> GetBatchForViewAsync(string routeGroupId, string targetStudentId, int batchIndex, AuthenticatedUserClaims claims)
	{
		try
		{
			var (ok, error, _) = await ValidateViewAccessAsync(routeGroupId, targetStudentId, claims);
			if (!ok) return error!;

			var batch = await _repository.GetBatchAsync(routeGroupId, targetStudentId, batchIndex);
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
			_logger.Error(ex, "Unexpected error fetching group-content batch - GroupId: {GroupId}, StudentId: {StudentId}, BatchIndex: {BatchIndex}", routeGroupId, targetStudentId, batchIndex);
			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An unexpected error occurred",
				Status = "failed"
			};
		}
	}
}
