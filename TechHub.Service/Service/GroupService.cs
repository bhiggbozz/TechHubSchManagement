using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Groups;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.Service.Service
{
	public class GroupService : IGroupService
	{
		private const int ApprovalExpiryDays = 7;

		private readonly IQueryRepository<StudentGroup> _groupQuery;
		private readonly ICommandRespository<StudentGroup> _groupCommand;
		private readonly IQueryRepository<StudentGroupMember> _memberQuery;
		private readonly ICommandRespository<StudentGroupMember> _memberCommand;
		private readonly IQueryRepository<Users> _userQuery;
		private readonly IQueryRepository<StudentClassroom> _studentClassroomQuery;
		private readonly IQueryRepository<TeacherClassroom> _teacherClassroomQuery;
		private readonly IQueryRepository<GroupLessonContent> _contentQuery;
		private readonly ICommandRespository<GroupLessonContent> _contentCommand;
		private readonly IQueryRepository<GroupLessonMedia> _contentMediaQuery;
		private readonly ICommandRespository<GroupLessonMedia> _contentMediaCommand;
		private readonly ICommandRespository<ApprovalRequests> _approvalCommand;
		private readonly IDbTransactionScopeFactory _scopeFactory;
		private readonly ILogger _logger;

		public GroupService(
			IQueryRepository<StudentGroup> groupQuery,
			ICommandRespository<StudentGroup> groupCommand,
			IQueryRepository<StudentGroupMember> memberQuery,
			ICommandRespository<StudentGroupMember> memberCommand,
			IQueryRepository<Users> userQuery,
			IQueryRepository<StudentClassroom> studentClassroomQuery,
			IQueryRepository<TeacherClassroom> teacherClassroomQuery,
			IQueryRepository<GroupLessonContent> contentQuery,
			ICommandRespository<GroupLessonContent> contentCommand,
			IQueryRepository<GroupLessonMedia> contentMediaQuery,
			ICommandRespository<GroupLessonMedia> contentMediaCommand,
			ICommandRespository<ApprovalRequests> approvalCommand,
			IDbTransactionScopeFactory scopeFactory,
			ILogger logger)
		{
			_groupQuery = groupQuery;
			_groupCommand = groupCommand;
			_memberQuery = memberQuery;
			_memberCommand = memberCommand;
			_userQuery = userQuery;
			_studentClassroomQuery = studentClassroomQuery;
			_teacherClassroomQuery = teacherClassroomQuery;
			_contentQuery = contentQuery;
			_contentCommand = contentCommand;
			_contentMediaQuery = contentMediaQuery;
			_contentMediaCommand = contentMediaCommand;
			_approvalCommand = approvalCommand;
			_scopeFactory = scopeFactory;
			_logger = logger;
		}

		public async Task<BaseResponse> CreateGroup(CreateGroupViewModel model, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims?.SchoolId, out var schoolId) || !Guid.TryParse(claims?.UserId, out var studentId))
					return Fail(ResponseCode.Unauthorized, "Invalid authentication");

				if (!Enum.TryParse<UserRole>(claims?.Role, ignoreCase: true, out var role) || role != UserRole.Student)
					return Fail(ResponseCode.Forbidden, "Only students can create a study group");

				if (model is null || string.IsNullOrWhiteSpace(model.Name))
					return Fail(ResponseCode.BadRequest, "Group name is required");

				var classroomId = await _studentClassroomQuery.QueryAsync<Guid>(
					"SELECT TOP 1 ClassroomId FROM StudentClassroom WHERE StudentId = @StudentId AND SchoolId = @SchoolId AND IsActive = 1",
					new Dictionary<string, object> { { "StudentId", studentId }, { "SchoolId", schoolId } });

				var resolvedClassroomId = classroomId.FirstOrDefault();
				if (resolvedClassroomId == Guid.Empty)
					return Fail(ResponseCode.BadRequest, "You must be enrolled in a classroom to create a group");

				var approverId = await ResolveGroupApproverAsync(resolvedClassroomId, schoolId);
				if (approverId == Guid.Empty)
					return Fail(ResponseCode.BadRequest, "This classroom has no assigned class teacher or head teacher to approve group requests — contact your school administrator");

				var groupId = Guid.NewGuid();
				var approvalId = Guid.NewGuid();
				var now = DateTime.UtcNow;

				using var scope = _scopeFactory.Create("DbConnectionString");
				try
				{
					await _groupCommand.Create(scope.Transaction, scope.Connection, new Dictionary<string, object>
					{
						{ "Id", groupId },
						{ "SchoolId", schoolId },
						{ "ClassroomId", resolvedClassroomId },
						{ "Name", model.Name.Trim() },
						{ "Status", "PendingApproval" },
						{ "CreatedBy", studentId },
						{ "CreatedAt", now },
						{ "IsActive", true }
					});

					await _approvalCommand.Create(scope.Transaction, scope.Connection, new Dictionary<string, object>
					{
						{ "Id", approvalId },
						{ "SchoolId", schoolId },
						{ "RequestedBy", studentId },
						{ "ApproverId", approverId },
						{ "OperationType", OperationType.CreateGroup },
						{ "EntityType", "StudentGroup" },
						{ "EntityId", groupId },
						{ "Payload", System.Text.Json.JsonSerializer.Serialize(new { GroupName = model.Name.Trim(), ClassroomId = resolvedClassroomId }) },
						{ "Status", ApprovalStatus.Pending },
						{ "RejectionReason", DBNull.Value },
						{ "CreatedAt", now },
						{ "RespondedAt", DBNull.Value },
						{ "ExpiresAt", now.AddDays(ApprovalExpiryDays) }
					});

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Failed to commit group creation transaction - GroupId: {GroupId}", groupId);
					try { await scope.RollbackAsync(); }
					catch (Exception rbEx)
					{
						_logger.Error(rbEx, "Rollback failed during group creation - GroupId: {GroupId}", groupId);
					}
					throw;
				}

				_logger.Information(
					"Student group created - GroupId: {GroupId}, CreatedBy: {StudentId}, ApproverId: {ApproverId}",
					groupId, studentId, approverId);

				return Success("Group created, pending approval", new
				{
					GroupId = groupId,
					Name = model.Name.Trim(),
					ClassroomId = resolvedClassroomId,
					Status = "PendingApproval",
					CreatedAt = now
				});
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Unexpected error while creating student group");
				return Fail(ResponseCode.ErrorOccured, "An unexpected error occurred");
			}
		}

		/// <summary>
		/// Resolves who approves group-related requests for a classroom: its
		/// ClassTeacher (via TeacherClassroom), falling back to the school's
		/// longest-tenured active HeadTeacher when no ClassTeacher is assigned.
		/// Returns Guid.Empty if neither exists. Shared by group creation and
		/// content submission so both route to the same approver.
		/// </summary>
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
					{ "ClassTeacherRoleId", (int)UserRole.ClassTeacher }
				});

			var approverId = classTeacherIds.FirstOrDefault();
			if (approverId != Guid.Empty)
				return approverId;

			var headTeacherIds = await _teacherClassroomQuery.QueryAsync<Guid>(
				"SELECT TOP 1 Id FROM Users WHERE SchoolId = @SchoolId AND RoleId = @HeadTeacherRoleId AND IsActive = 1 ORDER BY CreationDate ASC",
				new Dictionary<string, object>
				{
					{ "SchoolId", schoolId },
					{ "HeadTeacherRoleId", (int)UserRole.HeadTeacher }
				});

			return headTeacherIds.FirstOrDefault();
		}

		public async Task<BaseResponse> AddMembers(Guid groupId, AddGroupMembersViewModel model, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims?.SchoolId, out var schoolId) || !Guid.TryParse(claims?.UserId, out var studentId))
					return Fail(ResponseCode.Unauthorized, "Invalid authentication");

				if (model?.StudentIds is null || !model.StudentIds.Any())
					return Fail(ResponseCode.BadRequest, "At least one student is required");

				var group = await _groupQuery.Get(groupId);
				if (group is null || group.SchoolId != schoolId || !group.IsActive)
					return Fail(ResponseCode.NotFound, "Group not found");

				if (group.CreatedBy != studentId)
					return Fail(ResponseCode.Forbidden, "Only the group creator can add members");

				var requestedStudentIds = model.StudentIds.Distinct().Where(id => id != group.CreatedBy).ToList();
				if (!requestedStudentIds.Any())
					return Fail(ResponseCode.BadRequest, "At least one student is required");

				var invalidStudentIds = new List<Guid>();
				foreach (var candidateId in requestedStudentIds)
				{
					var student = await _userQuery.Get(candidateId);
					var inClassroom = student is not null && student.SchoolId == schoolId && student.IsActive &&
						student.RoleId == (int)UserRole.Student &&
						(await _studentClassroomQuery.CountAsync(
							"SELECT COUNT(*) FROM StudentClassroom WHERE StudentId = @StudentId AND ClassroomId = @ClassroomId AND IsActive = 1",
							new Dictionary<string, object> { { "StudentId", candidateId }, { "ClassroomId", group.ClassroomId } })) > 0;

					if (!inClassroom)
						invalidStudentIds.Add(candidateId);
				}

				if (invalidStudentIds.Any())
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "One or more students are not in this group's classroom",
						Status = "failed",
						Data = new { InvalidStudentIds = invalidStudentIds }
					};

				var activeMemberIds = (await _memberQuery.QueryAsync<Guid>(
					"SELECT StudentId FROM StudentGroupMember WHERE GroupId = @GroupId AND IsActive = 1",
					new Dictionary<string, object> { { "GroupId", groupId } })).ToList();

				var alreadyMemberIds = requestedStudentIds.Intersect(activeMemberIds).ToList();
				var newMemberIds = requestedStudentIds.Except(alreadyMemberIds).ToList();
				var now = DateTime.UtcNow;

				foreach (var memberId in newMemberIds)
				{
					await _memberCommand.Create(new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "GroupId", groupId },
						{ "StudentId", memberId },
						{ "SchoolId", schoolId },
						{ "InvitedBy", studentId },
						{ "CreatedAt", now },
						{ "IsActive", true }
					});
				}

				return Success(
					newMemberIds.Any() ? $"{newMemberIds.Count} member(s) added" : "No new members added — already in the group",
					new { AddedStudentIds = newMemberIds, AlreadyMemberStudentIds = alreadyMemberIds });
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Unexpected error while adding group members - GroupId: {GroupId}", groupId);
				return Fail(ResponseCode.ErrorOccured, "An unexpected error occurred");
			}
		}

		/// <summary>
		/// Any active member (including the creator) can submit content — audio/media
		/// and/or a board recording, all optional individually, mirroring how a normal
		/// lesson can already be submitted with any subset of its media. Blocked if the
		/// group has no other active member yet ("nobody to teach") or was rejected.
		/// Goes through the same ClassTeacher/HeadTeacher approval as group creation;
		/// other members only see it once approved — the creator always sees their own.
		/// </summary>
		public async Task<BaseResponse> SubmitContent(Guid groupId, SubmitGroupContentViewModel model, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims?.SchoolId, out var schoolId) || !Guid.TryParse(claims?.UserId, out var studentId))
					return Fail(ResponseCode.Unauthorized, "Invalid authentication");

				if (model is null || string.IsNullOrWhiteSpace(model.Aim) || string.IsNullOrWhiteSpace(model.Description))
					return Fail(ResponseCode.BadRequest, "Aim and description are required");

				var group = await _groupQuery.Get(groupId);
				if (group is null || group.SchoolId != schoolId || !group.IsActive)
					return Fail(ResponseCode.NotFound, "Group not found");

				if (string.Equals(group.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
					return Fail(ResponseCode.BadRequest, "This group's creation request was rejected — it cannot receive new content");

				var isMember = group.CreatedBy == studentId || (await _memberQuery.CountAsync(
					"SELECT COUNT(*) FROM StudentGroupMember WHERE GroupId = @GroupId AND StudentId = @StudentId AND IsActive = 1",
					new Dictionary<string, object> { { "GroupId", groupId }, { "StudentId", studentId } })) > 0;

				if (!isMember)
					return Fail(ResponseCode.Forbidden, "You are not a member of this group");

				var otherMemberCount = await _memberQuery.CountAsync(
					"SELECT COUNT(*) FROM StudentGroupMember WHERE GroupId = @GroupId AND IsActive = 1",
					new Dictionary<string, object> { { "GroupId", groupId } });

				if (otherMemberCount == 0)
					return Fail(ResponseCode.BadRequest, "Add at least one other student to this group before creating content — there's no one to teach yet");

				foreach (var file in model.MediaFiles ?? new List<TechHub.Core.ViewModel.LessonMediaViewModel>())
				{
					if (string.IsNullOrWhiteSpace(file.CloudinaryUrl) || string.IsNullOrWhiteSpace(file.PublicId))
						return Fail(ResponseCode.BadRequest, $"Invalid media file data for {file.OriginalFileName}");
				}

				var approverId = await ResolveGroupApproverAsync(group.ClassroomId, schoolId);
				if (approverId == Guid.Empty)
					return Fail(ResponseCode.BadRequest, "This classroom has no assigned class teacher or head teacher to approve content — contact your school administrator");

				var contentId = Guid.NewGuid();
				var approvalId = Guid.NewGuid();
				var now = DateTime.UtcNow;

				var contentDict = new Dictionary<string, object>
				{
					{ "Id", contentId },
					{ "SchoolId", schoolId },
					{ "GroupId", groupId },
					{ "SubjectId", model.SubjectId },
					{ "TopicId", model.TopicId.HasValue ? (object)model.TopicId.Value : DBNull.Value },
					{ "SubTopic", string.IsNullOrWhiteSpace(model.SubTopic) ? (object)DBNull.Value : model.SubTopic.Trim() },
					{ "Aim", model.Aim.Trim() },
					{ "Description", model.Description.Trim() },
					{ "Status", "PendingApproval" },
					{ "CreatedBy", studentId },
					{ "ApprovedBy", DBNull.Value },
					{ "ApprovedAt", DBNull.Value },
					{ "RejectedBy", DBNull.Value },
					{ "RejectionReason", DBNull.Value },
					{ "CreatedAt", now },
					{ "ModifiedAt", DBNull.Value }
				};

				var mediaDicts = (model.MediaFiles ?? new List<TechHub.Core.ViewModel.LessonMediaViewModel>())
					.Select((file, index) => new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "GroupContentId", contentId },
						{ "SchoolId", schoolId },
						{ "FileName", file.FileName },
						{ "OriginalFileName", file.OriginalFileName },
						{ "FileExtension", file.FileExtension },
						{ "MediaType", TechHub.Core.Model.MediaTypes.Resolve(file.FileExtension) },
						{ "FileSizeBytes", file.FileSizeBytes },
						{ "CloudinaryUrl", file.CloudinaryUrl },
						{ "PublicId", file.PublicId },
						{ "Duration", file.Duration.HasValue ? (object)file.Duration.Value : DBNull.Value },
						{ "Status", "Ready" },
						{ "DisplayOrder", file.DisplayOrder > 0 ? file.DisplayOrder : index + 1 },
						{ "CreatedAt", now },
						{ "IsActive", true },
						{ "MetaData", (object)file.MetaData ?? DBNull.Value }
					}).ToList();

				var subjectName = (await _userQuery.QueryAsync<string>(
					"SELECT TOP 1 Subject FROM Subjects WHERE Id = @SubjectId",
					new Dictionary<string, object> { { "SubjectId", model.SubjectId } })).FirstOrDefault() ?? "Unknown";

				using var scope = _scopeFactory.Create("DbConnectionString");
				try
				{
					await _contentCommand.Create(scope.Transaction, scope.Connection, contentDict);

					if (mediaDicts.Any())
						await _contentMediaCommand.CreateBatchAsync(scope.Transaction, scope.Connection, mediaDicts);

					await _approvalCommand.Create(scope.Transaction, scope.Connection, new Dictionary<string, object>
					{
						{ "Id", approvalId },
						{ "SchoolId", schoolId },
						{ "RequestedBy", studentId },
						{ "ApproverId", approverId },
						{ "OperationType", OperationType.SubmitGroupContent },
						{ "EntityType", "GroupLessonContent" },
						{ "EntityId", contentId },
						{ "Payload", System.Text.Json.JsonSerializer.Serialize(new
							{
								GroupId = groupId,
								GroupName = group.Name,
								Aim = model.Aim.Trim(),
								Description = model.Description.Trim(),
								SubjectName = subjectName,
								MediaCount = mediaDicts.Count
							}) },
						{ "Status", ApprovalStatus.Pending },
						{ "RejectionReason", DBNull.Value },
						{ "CreatedAt", now },
						{ "RespondedAt", DBNull.Value },
						{ "ExpiresAt", now.AddDays(ApprovalExpiryDays) }
					});

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Failed to commit group content submission transaction - ContentId: {ContentId}", contentId);
					try { await scope.RollbackAsync(); }
					catch (Exception rbEx)
					{
						_logger.Error(rbEx, "Rollback failed during group content submission - ContentId: {ContentId}", contentId);
					}
					throw;
				}

				_logger.Information(
					"Group content submitted - ContentId: {ContentId}, GroupId: {GroupId}, CreatedBy: {StudentId}, ApproverId: {ApproverId}",
					contentId, groupId, studentId, approverId);

				return Success("Content submitted, pending approval", new
				{
					ContentId = contentId,
					GroupId = groupId,
					Status = "PendingApproval",
					CreatedAt = now
				});
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Unexpected error while submitting group content - GroupId: {GroupId}", groupId);
				return Fail(ResponseCode.ErrorOccured, "An unexpected error occurred");
			}
		}

		/// <summary>Only the group creator may remove a member; the creator cannot be removed this way.</summary>
		public async Task<BaseResponse> RemoveMember(Guid groupId, Guid studentId, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims?.SchoolId, out var schoolId) || !Guid.TryParse(claims?.UserId, out var callerId))
					return Fail(ResponseCode.Unauthorized, "Invalid authentication");

				var group = await _groupQuery.Get(groupId);
				if (group is null || group.SchoolId != schoolId || !group.IsActive)
					return Fail(ResponseCode.NotFound, "Group not found");

				if (group.CreatedBy != callerId)
					return Fail(ResponseCode.Forbidden, "Only the group creator can remove a student");

				if (studentId == group.CreatedBy)
					return Fail(ResponseCode.BadRequest, "The group creator cannot be removed");

				var isActiveMember = (await _memberQuery.CountAsync(
					"SELECT COUNT(*) FROM StudentGroupMember WHERE GroupId = @GroupId AND StudentId = @StudentId AND IsActive = 1",
					new Dictionary<string, object> { { "GroupId", groupId }, { "StudentId", studentId } })) > 0;

				if (!isActiveMember)
					return Fail(ResponseCode.NotFound, "This student is not a member of the group");

				await _memberCommand.UpdateAsync(
					"UPDATE StudentGroupMember SET IsActive = 0 WHERE GroupId = @GroupId AND StudentId = @StudentId AND IsActive = 1",
					new Dictionary<string, object> { { "GroupId", groupId }, { "StudentId", studentId } });

				_logger.Information(
					"Student removed from group - GroupId: {GroupId}, StudentId: {StudentId}, By: {CallerId}",
					groupId, studentId, callerId);

				return Success("Student removed from group", null);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Unexpected error while removing group member - GroupId: {GroupId}, StudentId: {StudentId}", groupId, studentId);
				return Fail(ResponseCode.ErrorOccured, "An unexpected error occurred");
			}
		}

		public async Task<BaseResponse> GetMyGroups(AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims?.SchoolId, out var schoolId) || !Guid.TryParse(claims?.UserId, out var studentId))
					return Fail(ResponseCode.Unauthorized, "Invalid authentication");

				var groups = await _groupQuery.QueryAsync<MyGroupDto>(@"
					SELECT
						g.Id AS GroupId,
						g.Name,
						g.Status,
						CAST(CASE WHEN g.CreatedBy = @StudentId THEN 1 ELSE 0 END AS BIT) AS IsCreator,
						(SELECT COUNT(*) FROM StudentGroupMember m WHERE m.GroupId = g.Id AND m.IsActive = 1) AS MemberCount
					FROM StudentGroup g
					WHERE g.SchoolId = @SchoolId AND g.IsActive = 1
					AND (
						g.CreatedBy = @StudentId
						OR EXISTS (SELECT 1 FROM StudentGroupMember m WHERE m.GroupId = g.Id AND m.StudentId = @StudentId AND m.IsActive = 1)
					)
					ORDER BY g.CreatedAt DESC",
					new Dictionary<string, object> { { "StudentId", studentId }, { "SchoolId", schoolId } });

				return Success("My groups retrieved", groups.ToList());
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Unexpected error while fetching my groups");
				return Fail(ResponseCode.ErrorOccured, "An unexpected error occurred");
			}
		}

		public async Task<BaseResponse> GetGroupDetail(Guid groupId, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims?.SchoolId, out var schoolId) || !Guid.TryParse(claims?.UserId, out var studentId))
					return Fail(ResponseCode.Unauthorized, "Invalid authentication");

				var group = await _groupQuery.Get(groupId);
				if (group is null || group.SchoolId != schoolId || !group.IsActive)
					return Fail(ResponseCode.NotFound, "Group not found");

				var isMember = group.CreatedBy == studentId || (await _memberQuery.CountAsync(
					"SELECT COUNT(*) FROM StudentGroupMember WHERE GroupId = @GroupId AND StudentId = @StudentId AND IsActive = 1",
					new Dictionary<string, object> { { "GroupId", groupId }, { "StudentId", studentId } })) > 0;

				if (!isMember)
					return Fail(ResponseCode.Forbidden, "You are not a member of this group");

				var members = await _memberQuery.QueryAsync<GroupMemberDto>(@"
					SELECT u.Id AS StudentId, u.FirstName, u.LastName, CAST(0 AS BIT) AS IsCreator
					FROM StudentGroupMember m
					JOIN Users u ON u.Id = m.StudentId
					WHERE m.GroupId = @GroupId AND m.IsActive = 1
					ORDER BY u.FirstName, u.LastName",
					new Dictionary<string, object> { { "GroupId", groupId } });

				var creator = await _userQuery.Get(group.CreatedBy);
				var memberList = members.ToList();
				memberList.Insert(0, new GroupMemberDto
				{
					StudentId = group.CreatedBy,
					FirstName = creator?.FirstName ?? "Unknown",
					LastName = creator?.LastName ?? string.Empty,
					IsCreator = true
				});

				var isCreator = group.CreatedBy == studentId;
				var contentList = await _contentQuery.QueryAsync<GroupContentSummaryDto>(@"
					SELECT c.Id AS ContentId, c.Aim, s.Subject AS SubjectName, c.Status, c.CreatedBy, c.CreatedAt,
						(SELECT COUNT(*) FROM GroupLessonMedia m WHERE m.GroupContentId = c.Id AND m.IsActive = 1) AS MediaCount,
						CAST(0 AS BIT) AS HasRecording
					FROM GroupLessonContent c
					JOIN Subjects s ON s.Id = c.SubjectId
					WHERE c.GroupId = @GroupId
						AND (@IsCreator = 1 OR c.CreatedBy = @StudentId OR c.Status = 'Approved')
					ORDER BY c.CreatedAt DESC",
					new Dictionary<string, object>
					{
						{ "GroupId", groupId },
						{ "IsCreator", isCreator },
						{ "StudentId", studentId }
					});

				return Success("Group detail retrieved", new GroupDetailDto
				{
					GroupId = group.Id,
					Name = group.Name,
					Status = group.Status,
					ClassroomId = group.ClassroomId,
					CreatedBy = group.CreatedBy,
					Members = memberList,
					Content = contentList.ToList()
				});
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Unexpected error while fetching group detail - GroupId: {GroupId}", groupId);
				return Fail(ResponseCode.ErrorOccured, "An unexpected error occurred");
			}
		}

		private static BaseResponse Success(string message, object data) => new()
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
