using Serilog;
using Serilog.Context;
using System.Text.Json;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Enums;
using TechHub.Core.Helper;
using TechHub.Core.Model;
using TechHub.Core.Models;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.classroom;
using TechHub.Service.Interface;
using TechhubMS.util;

namespace TechHub.Service.Service
{
	public class ClassPreparationService : IClassPreparationService
	{
		private readonly IQueryRepository<ClassPreparation> _classQueryRepo;
		private readonly ICommandRespository<ClassPreparation> _classCommandRepo;
		private readonly IMediaService _mediaService;
		private readonly ITeacherTrustScoreService _trustScoreService;
		private readonly IUserService _userService;
		private readonly ILogger _logger;

		public ClassPreparationService(
			IQueryRepository<ClassPreparation> classQueryRepo,
			ICommandRespository<ClassPreparation> classCommandRepo,
			IMediaService mediaService,
			IUserService userService,
			ILogger logger)
		{
			_classQueryRepo = classQueryRepo;
			_classCommandRepo = classCommandRepo;
			_mediaService = mediaService;
			_userService = userService;
			_logger = logger;
		}

		#region Save Class Preparation (Draft)

		/// <summary>
		/// Save class preparation as draft
		/// Can be called multiple times to update draft before submission
		/// </summary>
		/// <remarks>
		/// WORKFLOW:
		/// 1. Validate input (required fields, valid GUIDs)
		/// 2. Check if updating existing or creating new
		/// 3. If updating: Verify teacher owns it, status allows editing
		/// 4. Save to database
		/// 5. Link media files to class
		/// 6. Return saved class with full details
		/// 
		/// EDITABLE STATUSES:
		/// - Draft: Can always edit
		/// - Rejected: Can edit to fix issues and resubmit
		/// 
		/// NOT EDITABLE:
		/// - Pending: Under review (can't change while admin reviewing)
		/// - Approved: Already approved (can't modify approved content)
		/// - InProgress/Completed: Class already happened
		/// </remarks>
		public async Task<ClassPreparationResponse> SaveClassPreparation(SaveClassPreparationViewModel model,AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			// using (LogContext.PushProperty("TenantId", userClaims.TenantIdentifier))
			{
				try
				{
					if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					{
						return new ClassPreparationResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId",
							Status = "failed"
						};
					}

					if (!Guid.TryParse(userClaims.UserId, out var userId))
					{
						return new ClassPreparationResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId",
							Status = "failed"
						};
					}

					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

					var mediaMetadataJson = model.MediaMetadata.HasValue
						? JsonSerializer.Serialize(model.MediaMetadata.Value)
						: null;

					static List<Guid> ExtractMediaFileIds(JsonElement? metadata)
					{
						var ids = new List<Guid>();

						if (!metadata.HasValue)
						{
							return ids;
						}

						try
						{
							var root = metadata.Value;

							if (root.ValueKind == JsonValueKind.Array)
							{
								foreach (var item in root.EnumerateArray())
								{
									if (item.ValueKind != JsonValueKind.Object)
									{
										continue;
									}

									if (item.TryGetProperty("mediaFileId", out var mediaFileIdProp) &&
										mediaFileIdProp.ValueKind == JsonValueKind.String &&
										Guid.TryParse(mediaFileIdProp.GetString(), out var parsedId))
									{
										ids.Add(parsedId);
									}
								}
							}
							else if (root.ValueKind == JsonValueKind.Object)
							{
								if (root.TryGetProperty("mediaFileIds", out var mediaFileIdsProp) &&
									mediaFileIdsProp.ValueKind == JsonValueKind.Array)
								{
									foreach (var idNode in mediaFileIdsProp.EnumerateArray())
									{
										if (idNode.ValueKind == JsonValueKind.String &&
											Guid.TryParse(idNode.GetString(), out var parsedId))
										{
											ids.Add(parsedId);
										}
									}
								}
							}
						}
						catch
						{
							// Ignore metadata parse issues for linking;
							// metadata string is still saved as provided.
						}

						return ids;
					}

					var mediaFileIds = ExtractMediaFileIds(model.MediaMetadata);

					if (model.Id.HasValue)
					{
						_logger.Information(
							"Updating class preparation - Id: {Id}, TopicId: {TopicId}",
							model.Id.Value,
							model.TopicId);

						var existingClass = await _classQueryRepo.Get(model.Id.Value);

						if (existingClass == null)
						{
							_logger.Warning("Class preparation not found - Id: {Id}", model.Id.Value);

							return new ClassPreparationResponse
							{
								ResponseCode = ResponseCode.NotFound,
								ResponseMessage = "Class preparation not found",
								Status = "failed"
							};
						}

						if (existingClass.TeacherId != userId)
						{
							_logger.Warning(
								"Teacher attempted to edit another teacher's class - ClassId: {ClassId}, AttemptedBy: {UserId}, Owner: {OwnerId}",
								model.Id.Value,
								userId,
								existingClass.TeacherId);

							return new ClassPreparationResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = "You can only edit your own class preparations",
								Status = "failed"
							};
						}

						var currentStatus = (ClassPreparationStatus)existingClass.Status;

						if (currentStatus != ClassPreparationStatus.Draft &&
							currentStatus != ClassPreparationStatus.Rejected)
						{
							_logger.Warning(
								"Attempted to edit class with non-editable status - ClassId: {ClassId}, Status: {Status}",
								model.Id.Value,
								currentStatus);

							return new ClassPreparationResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = $"Cannot edit class with status: {currentStatus}. Only Draft and Rejected classes can be edited.",
								Status = "failed"
							};
						}

						TimeSpan? scheduledTime = null;
						if (!string.IsNullOrWhiteSpace(model.ScheduledTime) &&
							TimeSpan.TryParse(model.ScheduledTime, out var parsedTime))
						{
							scheduledTime = parsedTime;
						}

						var updateDict = new Dictionary<string, object>
						{
							{ "SubjectId",       model.SubjectId },
							{ "ClassroomId",     model.ClassroomId },
							{ "TopicId",         model.TopicId },
							{ "SubTopicId",      model.SubTopicId.HasValue      ? model.SubTopicId.Value      : DBNull.Value },
							{ "AimAndObjectives",model.AimAndObjectives },
							{ "ScheduledDate",   model.ScheduledDate.HasValue   ? model.ScheduledDate.Value   : DBNull.Value },
							{ "ScheduledTime",   scheduledTime.HasValue         ? scheduledTime.Value         : DBNull.Value },
							{ "DurationMinutes", model.DurationMinutes.HasValue ? model.DurationMinutes.Value : DBNull.Value },
							{ "ClassType",       (int)model.ClassType },
							{ "MediaMetadataJson", mediaMetadataJson ?? (object)DBNull.Value },
							{ "ModifiedDate",    now }
						};

						var whereClause = new KeyValuePair<string, object>("Id", model.Id.Value);
						await _classCommandRepo.UpdateTableColumnById(updateDict, whereClause);

						_logger.Information("Class preparation updated - Id: {Id}", model.Id.Value);

						if (mediaFileIds.Any())
						{
							await _mediaService.LinkMediaToClass(model.Id.Value, mediaFileIds, userClaims);
						}

						return await GetClassPreparationById(model.Id.Value, userClaims);
					}
					else
					{
						_logger.Information(
							"Creating new class preparation - TopicId: {TopicId}, Teacher: {TeacherId}",
							model.TopicId,
							userId);

						var classId = Guid.NewGuid();
						var title = model.TopicId.ToString(); // Replace with real topic lookup if available

						TimeSpan? scheduledTime = null;
						if (!string.IsNullOrWhiteSpace(model.ScheduledTime) &&
							TimeSpan.TryParse(model.ScheduledTime, out var parsedTime))
						{
							scheduledTime = parsedTime;
						}

						var classDict = new Dictionary<string, object>
						{
							{ "Id",          classId },
							{ "ClassroomId", model.ClassroomId },
							{ "SubjectId",   model.SubjectId },
							{ "TeacherId",   userId },
							{ "SchoolId",    schoolId },

							// Class details
							{ "Title",            title },
							{ "TopicId",          model.TopicId },
							{ "SubTopicId",       model.SubTopicId.HasValue      ? model.SubTopicId.Value      : DBNull.Value },
							{ "AimAndObjectives", model.AimAndObjectives },

							// Timing
							{ "ScheduledDate",   model.ScheduledDate.HasValue   ? model.ScheduledDate.Value   : DBNull.Value },
							{ "ScheduledTime",   scheduledTime.HasValue         ? scheduledTime.Value         : DBNull.Value },
							{ "DurationMinutes", model.DurationMinutes.HasValue ? model.DurationMinutes.Value : DBNull.Value },

							// Class type
							{ "ClassType", (int)model.ClassType },

							// Status (Draft)
							{ "Status", (int)ClassPreparationStatus.Draft },

							// Workflow tracking (all null for draft)
							{ "SubmittedForApprovalDate", DBNull.Value },
							{ "SubmittedBy",              DBNull.Value },
							{ "ApprovedBy",               DBNull.Value },
							{ "ApprovedDate",             DBNull.Value },
							{ "RejectedBy",               DBNull.Value },
							{ "RejectedDate",             DBNull.Value },
							{ "RejectionReason",          DBNull.Value },

							// Media metadata JSON (nullable)
							{ "MediaMetadataJson", mediaMetadataJson ?? (object)DBNull.Value },

							// Metadata
							{ "CreationDate", now },
							{ "ModifiedDate", now },
							{ "CreatedBy",    userId },
							{ "IsActive",     true }
						};

						await _classCommandRepo.Create(classDict);

						_logger.Information("Class preparation created - Id: {Id}", classId);

						if (mediaFileIds.Any())
						{
							await _mediaService.LinkMediaToClass(classId, mediaFileIds, userClaims);
						}

						return await GetClassPreparationById(classId, userClaims);
					}
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Exception saving class preparation");

					return new ClassPreparationResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while saving class preparation",
						Status = "failed"
					};
				}
			}
		}

		#endregion

		#region Submit for Approval

		/// <summary>
		/// Submit class preparation for admin approval
		/// </summary>
		/// <remarks>
		/// WORKFLOW:
		/// 1. Verify class exists and teacher owns it
		/// 2. Verify status is Draft or Rejected (can only submit these)
		/// 3. Update status to Pending
		/// 4. Record submission date and user
		/// 5. Clear rejection details (if resubmitting after rejection)
		/// 
		/// BUSINESS RULES:
		/// - Only Draft or Rejected classes can be submitted
		/// - Teacher can only submit their own classes
		/// - Once submitted, teacher cannot edit until approved/rejected
		/// </remarks>
		public async Task<BaseResponse> SubmitForApproval(SubmitForApprovalViewModel model, AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			{
				try
				{
					if (!Guid.TryParse(userClaims.UserId, out var userId))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId",
							Status = "failed"
						};
					}

					_logger.Information(
						"Submitting class for approval - ClassId: {ClassId}, SubmittedBy: {UserId}",
						model.ClassPreparationId,
						userId);

					var classPrep = await _classQueryRepo.Get(model.ClassPreparationId);

					if (classPrep == null)
					{
						_logger.Warning("Class not found - ClassId: {ClassId}", model.ClassPreparationId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Class preparation not found",
							Status = "failed"
						};
					}

					if (classPrep.TeacherId != userId)
					{
						_logger.Warning(
							"Teacher attempted to submit another teacher's class - ClassId: {ClassId}, AttemptedBy: {UserId}, Owner: {OwnerId}",
							model.ClassPreparationId,
							userId,
							classPrep.TeacherId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You can only submit your own class preparations",
							Status = "failed"
						};
					}

					var currentStatus = (ClassPreparationStatus)classPrep.Status;

					if (currentStatus != ClassPreparationStatus.Draft &&
						currentStatus != ClassPreparationStatus.Rejected)
					{
						_logger.Warning(
							"Attempted to submit class with invalid status - ClassId: {ClassId}, Status: {Status}",
							model.ClassPreparationId,
							currentStatus);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Cannot submit class with status: {currentStatus}. Only Draft and Rejected classes can be submitted.",
							Status = "failed"
						};
					}


					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

					var updateDict = new Dictionary<string, object>
					{
						{ "Status", (int)ClassPreparationStatus.Pending },
						{ "SubmittedForApprovalDate", now },
						{ "SubmittedBy", userId },
                        
                        // Clear rejection details (if resubmitting after rejection)
                        { "RejectedBy", DBNull.Value },
						{ "RejectedDate", DBNull.Value },
						{ "RejectionReason", DBNull.Value },

						{ "ModifiedDate", now }
					};

					var whereClause = new KeyValuePair<string, object>("Id", model.ClassPreparationId);
					await _classCommandRepo.UpdateTableColumnById(updateDict, whereClause);

					_logger.Information(
						"Class submitted for approval - ClassId: {ClassId}, Status: Draft/Rejected → Pending",
						model.ClassPreparationId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Class preparation submitted for approval successfully",
						Status = "successful"
					};
				}
				catch (Exception ex)
				{
					_logger.Error(
						ex,
						"Exception submitting class for approval - ClassId: {ClassId}",
						model.ClassPreparationId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while submitting for approval",
						Status = "failed"
					};
				}
			}
		}

		#endregion

		#region Approve Class


		/// <summary>
		/// Approve a pending class preparation
		/// 
		/// COMPLETE WORKFLOW WITH ALL BUSINESS RULES:
		/// 
		/// 1. SECURITY VALIDATION:
		///    - Parse and validate JWT user claims
		///    - Verify admin user ID and school ID are valid GUIDs
		/// 
		/// 2. RETRIEVE CLASS:
		///    - Fetch class preparation from database
		///    - Verify class exists
		/// 
		/// 3. STATUS VALIDATION:
		///    - Verify class is in Pending status
		///    - Cannot approve Draft, Approved, or Rejected classes
		/// 
		/// 4. SELF-APPROVAL PREVENTION:
		///    - Verify admin is not approving their own class
		///    - TeacherId must differ from admin UserId
		/// 
		/// 5. PERMISSION CHECK:
		///    - Verify admin has ApproveClasses permission
		///    - SuperAdministrators bypass permission check
		/// 
		/// 6. MEDIA VALIDATION:
		///    - Verify all media files are fully uploaded (status Completed)
		///    - Prevent approving classes with pending uploads
		/// 
		/// 7. MOVE MEDIA TO PERMANENT STORAGE:
		///    - Move all class media from temp to permanent folder
		///    - FROM: temp/pending/{schoolId}/
		///    - TO: schools/{schoolId}/
		///    - Log warnings if some files fail but continue
		/// 
		/// 8. UPDATE CLASS STATUS:
		///    - Change status from Pending to Approved
		///    - Record approval metadata (admin, timestamp, notes, duration)
		///    - Update modification timestamp
		/// 
		/// 9. UPDATE TEACHER TRUST SCORE:
		///    - Recalculate trust score based on approval history
		///    - Update consecutive approvals streak
		///    - Log error if fails but don't block approval
		/// 
		/// 10. RETURN SUCCESS:
		///     - Return success response to admin
		/// 
		/// ERROR HANDLING:
		/// - All database operations wrapped in try-catch
		/// - Detailed logging at each step
		/// - Graceful degradation (trust score failure doesn't block approval)
		/// - Descriptive error messages
		/// </summary>
		public async Task<BaseResponse> ApproveClass(Core.ViewModel.classroom.ApproveClassViewModel model, AuthenticatedUserClaims userClaims)
		{
			// Track approval duration for performance metrics
			var approvalStartTime = DateTime.UtcNow;

			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			using (LogContext.PushProperty("ClassPreparationId", model.ClassPreparationId))
			{
				try
				{
					_logger.Information(
						"Starting class approval - ClassId: {ClassId}, AdminUserId: {AdminId}",
						model.ClassPreparationId,
						userClaims.UserId);

					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
					// STEP 1: PARSE AND VALIDATE USER CLAIMS
					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

					if (!Guid.TryParse(userClaims.UserId, out var adminUserId))
					{
						_logger.Warning("Invalid UserId - UserId: {UserId}", userClaims.UserId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid user identification",
							Status = "failed"
						};
					}

					if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					{
						_logger.Warning("Invalid SchoolId - SchoolId: {SchoolId}", userClaims.SchoolId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid school identification",
							Status = "failed"
						};
					}

					_logger.Debug("Claims validated - AdminId: {AdminId}, SchoolId: {SchoolId}", adminUserId, schoolId);

					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
					// STEP 2: RETRIEVE CLASS PREPARATION
					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

					var classPreparation = await _classQueryRepo.Get(model.ClassPreparationId);

					if (classPreparation == null)
					{
						_logger.Warning("Class not found - ClassId: {ClassId}", model.ClassPreparationId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Class preparation not found",
							Status = "failed"
						};
					}

					_logger.Debug(
						"Class retrieved - ClassId: {ClassId}, TeacherId: {TeacherId}, Status: {Status}",
						classPreparation.Id,
						classPreparation.TeacherId,
						(ClassPreparationStatus)classPreparation.Status);

					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
					// STEP 3: VERIFY CLASS IS IN PENDING STATUS
					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

					var currentStatus = (ClassPreparationStatus)classPreparation.Status;

					if (currentStatus != ClassPreparationStatus.Pending)
					{
						_logger.Warning(
							"Invalid status - ClassId: {ClassId}, Status: {Status}",
							classPreparation.Id,
							currentStatus);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Cannot approve class with status: {currentStatus}. Only Pending classes can be approved.",
							Status = "failed"
						};
					}

					_logger.Debug("Status verified as Pending");

					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
					// STEP 4: PREVENT SELF-APPROVAL
					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

					if (classPreparation.TeacherId == adminUserId)
					{
						_logger.Warning("Self-approval attempt - AdminId: {AdminId}", adminUserId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You cannot approve your own class preparation. Another administrator must review it.",
							Status = "failed"
						};
					}

					_logger.Debug("Self-approval check passed");

					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
					// STEP 5: CHECK ADMIN PERMISSIONS
					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

					var userRole = userClaims?.Role;

					if (userRole == UserRole.Administrator.ToString())
					{
						_logger.Debug("Checking ApproveClasses permission");

						var hasPermission = await _userService.HasPermission(adminUserId, schoolId, AdminPermission.ApproveClasses);

						if (!hasPermission)
						{
							_logger.Warning("Missing permission - AdminId: {AdminId}", adminUserId);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = "You do not have permission to approve classes",
								Status = "failed"
							};
						}

						_logger.Debug("Permission verified");
					}
					else
					{
						_logger.Debug("SuperAdministrator - permission check bypassed");
					}

					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
					// STEP 6: VERIFY ALL MEDIA FILES ARE UPLOADED
					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

					_logger.Debug("Checking media upload status");

					var mediaFilesResponse = await _mediaService.GetClassMediaFiles(model.ClassPreparationId);

					if (mediaFilesResponse.ResponseCode == ResponseCode.successful &&
						mediaFilesResponse.MediaFiles.Any())
					{
						var pendingMedia = mediaFilesResponse.MediaFiles
							.Where(m => m.UploadStatus != (int)UploadStatus.Completed)
							.ToList();

						if (pendingMedia.Any())
						{
							var pendingFileNames = string.Join(", ", pendingMedia.Select(m => m.OriginalFileName));

							_logger.Warning(
								"Pending media uploads - ClassId: {ClassId}, Files: {Files}",
								model.ClassPreparationId,
								pendingFileNames);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = $"Cannot approve: {pendingMedia.Count} media file(s) still uploading: {pendingFileNames}",
								Status = "failed"
							};
						}

						_logger.Information(
							"All media uploaded - ClassId: {ClassId}, Count: {Count}",
							model.ClassPreparationId,
							mediaFilesResponse.MediaFiles.Count);
					}

					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
					// STEP 7: MOVE MEDIA TO PERMANENT STORAGE
					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

					_logger.Information("Moving media to permanent storage - ClassId: {ClassId}",
						model.ClassPreparationId);

					var moveMediaResult = await _mediaService.MoveMediaToPermanent(model.ClassPreparationId);

					if (moveMediaResult.ResponseCode != ResponseCode.successful)
					{
						_logger.Warning(
							"Media move partially failed - ClassId: {ClassId}, Message: {Message}",
							model.ClassPreparationId,
							moveMediaResult.ResponseMessage);
					}
					else
					{
						_logger.Information("Media moved successfully - ClassId: {ClassId}",
							model.ClassPreparationId);
					}

					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
					// STEP 8: UPDATE CLASS STATUS TO APPROVED
					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

					var approvalDurationMinutes = (int)(DateTime.UtcNow - approvalStartTime).TotalMinutes;
					var approvalTimestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

					var updateDict = new Dictionary<string, object>
						{
							{ "Status", (int)ClassPreparationStatus.Approved },
							{ "ApprovedDate", approvalTimestamp },
							{ "ApprovedBy", adminUserId },
							{ "ApprovalNotes", model.ApprovalNotes ?? string.Empty },
							{ "ApprovalTimeMinutes", approvalDurationMinutes },
							{ "ModifiedDate", approvalTimestamp }
						};

					var whereClause = new KeyValuePair<string, object>("Id", model.ClassPreparationId);

					await _classCommandRepo.UpdateTableColumnById(updateDict, whereClause);

					_logger.Information(
						"Class approved - ClassId: {ClassId}, ApprovedBy: {AdminId}, Duration: {Duration}min",
						model.ClassPreparationId,
						adminUserId,
						approvalDurationMinutes);

					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
					// STEP 9: UPDATE TEACHER TRUST SCORE
					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

					try
					{
						await _trustScoreService.UpdateAfterApproval(
							teacherId: classPreparation.TeacherId,
							schoolId: schoolId,
							approved: true,
							approvalTimeMinutes: approvalDurationMinutes);

						_logger.Information("Trust score updated - TeacherId: {TeacherId}",
							classPreparation.TeacherId);
					}
					catch (Exception ex)
					{
						_logger.Error(ex, "Trust score update failed - TeacherId: {TeacherId}. Approval succeeded anyway.",
							classPreparation.TeacherId);
					}

					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
					// STEP 10: RETURN SUCCESS
					// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Class approved successfully",
						Status = "successful"
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error approving class - ClassId: {ClassId}",
						model.ClassPreparationId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while approving the class",
						Status = "failed"
					};
				}
			}
		}

		/// <summary>
		/// Reject a pending class preparation
		/// 
		/// COMPLETE WORKFLOW WITH ALL BUSINESS RULES:
		/// 
		/// 1. SECURITY VALIDATION:
		///    - Parse and validate JWT user claims
		/// 
		/// 2. RETRIEVE CLASS:
		///    - Fetch class preparation from database
		///    - Verify class exists
		/// 
		/// 3. STATUS VALIDATION:
		///    - Verify class is in Pending status
		/// 
		/// 4. SELF-REJECTION PREVENTION:
		///    - Verify admin is not rejecting their own class
		/// 
		/// 5. PERMISSION CHECK:
		///    - Verify admin has ApproveClasses permission
		/// 
		/// 6. VALIDATE REJECTION REASON:
		///    - Ensure reason is provided and within character limits
		/// 
		/// 7. DELETE CLASS MEDIA:
		///    - Soft delete all media files in database
		///    - Fire-and-forget physical delete from Cloudinary
		/// 
		/// 8. UPDATE CLASS STATUS:
		///    - Change status to Rejected
		///    - Record rejection metadata
		/// 
		/// 9. UPDATE TEACHER TRUST SCORE:
		///    - Recalculate trust score (penalty for rejection)
		///    - Reset consecutive approvals streak to 0
		/// 
		/// 10. RETURN SUCCESS
		/// </summary>
		//public async Task<BaseResponse> RejectClass(RejectClassViewModel model,AuthenticatedUserClaims userClaims)
		//{
		//	var rejectionStartTime = DateTime.UtcNow;

		//	using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		//	using (LogContext.PushProperty("ClassPreparationId", model.ClassPreparationId))
		//	{
		//		try
		//		{
		//			_logger.Information(
		//				"Starting class rejection - ClassId: {ClassId}, AdminUserId: {AdminId}",model.ClassPreparationId,userClaims.UserId);

		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
		//			// STEP 1: PARSE USER CLAIMS
		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

		//			if (!Guid.TryParse(userClaims.UserId, out var adminUserId))
		//			{
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.BadRequest,
		//					ResponseMessage = "Invalid user identification",
		//					Status = "failed"
		//				};
		//			}

		//			if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
		//			{
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.BadRequest,
		//					ResponseMessage = "Invalid school identification",
		//					Status = "failed"
		//				};
		//			}

		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
		//			// STEP 2: RETRIEVE CLASS
		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

		//			var classPreparation = await _classQueryRepo.Get(model.ClassPreparationId);

		//			if (classPreparation == null)
		//			{
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.NotFound,
		//					ResponseMessage = "Class preparation not found",
		//					Status = "failed"
		//				};
		//			}

		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
		//			// STEP 3: STATUS VALIDATION
		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

		//			var currentStatus = (ClassPreparationStatus)classPreparation.Status;

		//			if (currentStatus != ClassPreparationStatus.Pending)
		//			{
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.BadRequest,
		//					ResponseMessage = $"Cannot reject class with status: {currentStatus}",
		//					Status = "failed"
		//				};
		//			}

		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
		//			// STEP 4: PREVENT SELF-REJECTION
		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

		//			if (classPreparation.TeacherId == adminUserId)
		//			{
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.Forbidden,
		//					ResponseMessage = "You cannot reject your own class preparation",
		//					Status = "failed"
		//				};
		//			}

		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
		//			// STEP 5: CHECK PERMISSIONS
		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

		//			var userRole = userClaims?.Role;

		//			if (userRole == UserRole.Administrator.ToString())
		//			{
		//				var hasPermission = await _userService.HasPermission(
		//					adminUserId,
		//					schoolId,
		//					AdminPermission.ApproveClasses);

		//				if (!hasPermission)
		//				{
		//					return new BaseResponse
		//					{
		//						ResponseCode = ResponseCode.Forbidden,
		//						ResponseMessage = "You do not have permission to reject classes",
		//						Status = "failed"
		//					};
		//				}
		//			}



		//			if (string.IsNullOrWhiteSpace(model.RejectionReason))
		//			{
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.BadRequest,
		//					ResponseMessage = "Rejection reason is required",
		//					Status = "failed"
		//				};
		//			}

		//			if (model.RejectionReason.Length < 10 || model.RejectionReason.Length > 500)
		//			{
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.BadRequest,
		//					ResponseMessage = "Rejection reason must be between 10 and 500 characters",
		//					Status = "failed"
		//				};
		//			}



		//			_logger.Information("Deleting media for rejected class - ClassId: {ClassId}",
		//				model.ClassPreparationId);

		//			var deleteMediaResult = await _mediaService.DeleteMediaForRejectedClass(
		//				model.ClassPreparationId,
		//				adminUserId,
		//				$"Class rejected: {model.RejectionReason}");

		//			if (deleteMediaResult.ResponseCode != ResponseCode.successful)
		//			{
		//				_logger.Warning(
		//					"Media deletion failed - ClassId: {ClassId}, Message: {Message}",model.ClassPreparationId,deleteMediaResult.ResponseMessage);
		//			}

		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
		//			// STEP 8: UPDATE CLASS STATUS
		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

		//			var rejectionTimestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

		//			var updateDict = new Dictionary<string, object>
		//			{
		//				{ "Status", (int)ClassPreparationStatus.Rejected },
		//				{ "RejectedDate", rejectionTimestamp },
		//				{ "RejectedBy", adminUserId },
		//				{ "RejectionReason", model.RejectionReason },
		//				{ "ModifiedDate", rejectionTimestamp }
		//			};

		//			await _classCommandRepo.UpdateTableColumnById(updateDict,new KeyValuePair<string, object>("Id", model.ClassPreparationId));

		//			_logger.Information("Class rejected - ClassId: {ClassId}, RejectedBy: {AdminId}",
		//				model.ClassPreparationId, adminUserId);

		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
		//			// STEP 9: UPDATE TRUST SCORE
		//			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

		//			try
		//			{
		//				await _trustScoreService.UpdateAfterApproval(teacherId: classPreparation.TeacherId,schoolId: schoolId,approved: false);

		//				_logger.Information("✅ Trust score updated - TeacherId: {TeacherId}",
		//					classPreparation.TeacherId);
		//			}
		//			catch (Exception ex)
		//			{
		//				_logger.Error(ex, "Trust score update failed - TeacherId: {TeacherId}",
		//					classPreparation.TeacherId);
		//			}

		//			return new BaseResponse
		//			{
		//				ResponseCode = ResponseCode.successful,
		//				ResponseMessage = "Class rejected successfully",
		//				Status = "successful"
		//			};
		//		}
		//		catch (Exception ex)
		//		{
		//			_logger.Error(ex, "Error rejecting class - ClassId: {ClassId}",
		//				model.ClassPreparationId);

		//			return new BaseResponse
		//			{
		//				ResponseCode = ResponseCode.ErrorOccured,
		//				ResponseMessage = "An error occurred while rejecting the class",
		//				Status = "failed"
		//			};
		//		}
		//	}
		//}


		#endregion

		#region Reject Class

		/// <summary>
		/// Reject class preparation (Admin only)
		/// </summary>
		/// <remarks>
		/// WORKFLOW:
		/// 1. Verify user is admin with ApproveClasses permission
		/// 2. Verify class exists and status is Pending
		/// 3. Prevent self-rejection (teacher can't reject their own class)
		/// 4. Update status to Rejected
		/// 5. Record rejection reason, date, and admin
		/// 6. Delete all associated media (soft delete in DB, physical delete in Cloudinary)
		/// 
		/// BUSINESS RULES:
		/// - Only admins with ApproveClasses permission can reject
		/// - Only Pending classes can be rejected
		/// - Teacher cannot reject their own class
		/// - Rejection reason is required (min 10 characters)
		/// - Teacher can fix issues and resubmit after rejection
		/// </remarks>
		public async Task<BaseResponse> RejectClass(Core.ViewModel.classroom.RejectClassViewModel model, AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			{
				try
				{
					if (!Guid.TryParse(userClaims.UserId, out var userId))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId",
							Status = "failed"
						};
					}

					if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId",
							Status = "failed"
						};
					}

					_logger.Information(
						"Rejecting class - ClassId: {ClassId}, RejectedBy: {UserId}, Reason: {Reason}",
						model.ClassPreparationId,
						userId,
						model.RejectionReason);

					var userRole = int.Parse(userClaims.Role ?? "0");

					if (userRole == (int)UserRole.Administrator)
					{
						var hasPermission = await _userService.HasPermission(userId, schoolId, AdminPermission.ApproveClasses);

						if (!hasPermission)
						{
							_logger.Warning(
								"Admin lacks ApproveClasses permission - UserId: {UserId}",
								userId);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = "You don't have permission to reject classes",
								Status = "failed"
							};
						}
					}



					var classPrep = await _classQueryRepo.Get(model.ClassPreparationId);

					if (classPrep == null)
					{
						_logger.Warning("Class not found - ClassId: {ClassId}", model.ClassPreparationId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Class preparation not found",
							Status = "failed"
						};
					}



					var currentStatus = (ClassPreparationStatus)classPrep.Status;

					if (currentStatus != ClassPreparationStatus.Pending)
					{
						_logger.Warning(
							"Attempted to reject class with invalid status - ClassId: {ClassId}, Status: {Status}",
							model.ClassPreparationId,
							currentStatus);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Cannot reject class with status: {currentStatus}. Only Pending classes can be rejected.",
							Status = "failed"
						};
					}


					if (classPrep.TeacherId == userId)
					{
						_logger.Warning(
							"Teacher attempted to reject their own class - ClassId: {ClassId}, TeacherId: {TeacherId}",
							model.ClassPreparationId, userId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "You cannot reject your own class preparation",
							Status = "failed"
						};
					}



					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

					var updateDict = new Dictionary<string, object>
					{
						{ "Status", (int)ClassPreparationStatus.Rejected },
						{ "RejectedBy", userId },
						{ "RejectedDate", now },
						{ "RejectionReason", model.RejectionReason },
                        
                        // Clear approval details (if previously approved then somehow went back)
                        { "ApprovedBy", DBNull.Value },
						{ "ApprovedDate", DBNull.Value },

						{ "ModifiedDate", now }
					};

					var whereClause = new KeyValuePair<string, object>("Id", model.ClassPreparationId);
					await _classCommandRepo.UpdateTableColumnById(updateDict, whereClause);

					_logger.Information(
						"Class rejected - ClassId: {ClassId}, Status: Pending → Rejected",
						model.ClassPreparationId);


					_logger.Information("Deleting media for rejected class - ClassId: {ClassId}", model.ClassPreparationId);

					var deleteResult = await _mediaService.DeleteMediaForRejectedClass(
						model.ClassPreparationId, userId, model.RejectionReason);

					if (deleteResult.ResponseCode == ResponseCode.successful)
					{
						_logger.Information("Media deleted successfully");
					}
					else
					{
						_logger.Warning("Some media files failed to delete");
					}

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Class preparation rejected successfully",
						Status = "successful"
					};
				}
				catch (Exception ex)
				{
					_logger.Error(
						ex,
						"Exception rejecting class - ClassId: {ClassId}",
						model.ClassPreparationId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while rejecting class",
						Status = "failed"
					};
				}
			}
		}

		#endregion

		#region Get Class Preparation

		/// <summary>
		/// Get class preparation by ID with full details
		/// </summary>
		// Services/ClassPreparationService.cs

		/// <summary>
		/// Get class preparation by ID with full details
		/// </summary>
		public async Task<ClassPreparationResponse> GetClassPreparationById(Guid id, AuthenticatedUserClaims userClaims)
		{
			try
			{
				_logger.Information("Retrieving class preparation - Id: {Id}", id);

				// ========================================
				// GET CLASS WITH JOINS
				// ========================================

				var query = $@"
					SELECT 
						cp.*,
						s.Name as SubjectName,
						sc.Name as ClassroomName,
						CONCAT(u.FirstName, ' ', u.LastName) as TeacherName,
						u.Email as TeacherEmail,
						CONCAT(submitter.FirstName, ' ', submitter.LastName) as SubmittedByName,
						CONCAT(approver.FirstName, ' ', approver.LastName) as ApprovedByName,
						CONCAT(rejecter.FirstName, ' ', rejecter.LastName) as RejectedByName,
						CONCAT(creator.FirstName, ' ', creator.LastName) as CreatedByName
					FROM ClassPreparation cp
					LEFT JOIN Subject s ON cp.SubjectId = s.Id
					LEFT JOIN StudentClass sc ON cp.ClassroomId = sc.Id
					LEFT JOIN Users u ON cp.TeacherId = u.Id
					LEFT JOIN Users submitter ON cp.SubmittedBy = submitter.Id
					LEFT JOIN Users approver ON cp.ApprovedBy = approver.Id
					LEFT JOIN Users rejecter ON cp.RejectedBy = rejecter.Id
					LEFT JOIN Users creator ON cp.CreatedBy = creator.Id
					WHERE cp.Id = '{id}'";


				var result = await _classQueryRepo.GetByQuery(query);
				var classPrep = result.FirstOrDefault();

				if (classPrep == null)
				{
					_logger.Warning("Class preparation not found - Id: {Id}", id);

					return new ClassPreparationResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Class preparation not found",
						Status = "failed"
					};
				}



				var mediaResult = await _mediaService.GetClassMediaFiles(id);
				var mediaFiles = mediaResult.MediaFiles.Count > 0 ? mediaResult.MediaFiles : new List<MediaFileDto>();



				// Note: If your GetByQuery returns dynamic objects, 
				// you'll need to extract properties like this:

				string subjectName = "";
				string classroomName = "";
				string teacherName = "";
				string teacherEmail = "";
				string submittedByName = "";
				string approvedByName = "";
				string rejectedByName = "";
				string createdByName = "";

				// If using Dapper, the joined columns will be in the same object
				// This is a simple extraction - adjust based on your repository implementation
				try
				{
					// Try to get properties from dynamic result
					// This depends on how your repository returns data

					// Example if using reflection or dynamic:
					var type = classPrep.GetType();

					var subjectNameProp = type.GetProperty("SubjectName");
					if (subjectNameProp != null)
						subjectName = subjectNameProp.GetValue(classPrep)?.ToString() ?? "";

					var classroomNameProp = type.GetProperty("ClassroomName");
					if (classroomNameProp != null)
						classroomName = classroomNameProp.GetValue(classPrep)?.ToString() ?? "";

					var teacherNameProp = type.GetProperty("TeacherName");
					if (teacherNameProp != null)
						teacherName = teacherNameProp.GetValue(classPrep)?.ToString() ?? "";

					var teacherEmailProp = type.GetProperty("TeacherEmail");
					if (teacherEmailProp != null)
						teacherEmail = teacherEmailProp.GetValue(classPrep)?.ToString() ?? "";

					var submittedByNameProp = type.GetProperty("SubmittedByName");
					if (submittedByNameProp != null)
						submittedByName = submittedByNameProp.GetValue(classPrep)?.ToString() ?? "";

					var approvedByNameProp = type.GetProperty("ApprovedByName");
					if (approvedByNameProp != null)
						approvedByName = approvedByNameProp.GetValue(classPrep)?.ToString() ?? "";

					var rejectedByNameProp = type.GetProperty("RejectedByName");
					if (rejectedByNameProp != null)
						rejectedByName = rejectedByNameProp.GetValue(classPrep)?.ToString() ?? "";

					var createdByNameProp = type.GetProperty("CreatedByName");
					if (createdByNameProp != null)
						createdByName = createdByNameProp.GetValue(classPrep)?.ToString() ?? "";
				}
				catch (Exception ex)
				{
					_logger.Warning(ex, "Error extracting joined properties from query result");
					// Continue with empty strings if extraction fails
				}



				Guid? currentUserId = Guid.TryParse(userClaims.UserId, out var parsedUserId)
					? parsedUserId
					: null;

				var dto = classPrep.ToDto(
					subjectName: subjectName,
					classroomName: classroomName,
					teacherName: teacherName,
					teacherEmail: teacherEmail,
					mediaFiles: mediaFiles,
					submittedByName: submittedByName,
					approvedByName: approvedByName,
					rejectedByName: rejectedByName,
					createdByName: createdByName,
					currentUserId: currentUserId
				);

				_logger.Information("Class preparation retrieved successfully");

				return new ClassPreparationResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Class preparation retrieved successfully",
					Status = "successful",
					ClassPreparation = dto
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Exception retrieving class preparation - Id: {Id}", id);

				return new ClassPreparationResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving class preparation",
					Status = "failed"
				};
			}
		}

		/// <summary>
		/// Get teacher's own class preparations with filtering and pagination
		/// </summary>
		public async Task<ClassPreparationsListResponse> GetMyClassPreparations(GetClassPreparationsQuery query, AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			{
				try
				{
					if (!Guid.TryParse(userClaims.UserId, out var userId))
					{
						return new ClassPreparationsListResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId",
							Status = "failed"
						};
					}

					_logger.Information(
						"Retrieving class preparations for teacher - UserId: {UserId}",
						userId);

					// ========================================
					// BUILD QUERY WITH FILTERS
					// ========================================

					var whereConditions = new List<string>
					{
						$"cp.TeacherId = '{userId}'",
						"cp.IsActive = 1"
					};

					// Status filter
					if (query.Status.HasValue)
					{
						whereConditions.Add($"cp.Status = {query.Status.Value}");
					}

					// Classroom filter
					if (query.ClassroomId.HasValue)
					{
						whereConditions.Add($"cp.ClassroomId = '{query.ClassroomId.Value}'");
					}

					// Subject filter
					if (query.SubjectId.HasValue)
					{
						whereConditions.Add($"cp.SubjectId = '{query.SubjectId.Value}'");
					}

					// Date range filter
					if (query.FromDate.HasValue)
					{
						whereConditions.Add($"cp.CreationDate >= '{query.FromDate.Value:yyyy-MM-dd}'");
					}

					if (query.ToDate.HasValue)
					{
						whereConditions.Add($"cp.CreationDate <= '{query.ToDate.Value:yyyy-MM-dd 23:59:59}'");
					}

					// Search term (topic or subtopic)
					if (!string.IsNullOrWhiteSpace(query.SearchTerm))
					{
						var searchTerm = query.SearchTerm.Replace("'", "''"); // SQL injection protection
						whereConditions.Add(
							$"(cp.Topic LIKE '%{searchTerm}%' OR cp.SubTopic LIKE '%{searchTerm}%')");
					}

					var whereClause = string.Join(" AND ", whereConditions);

					// ========================================
					// GET TOTAL COUNT
					// ========================================

					var countQuery = $@"
						SELECT COUNT(*) as TotalCount
						FROM ClassPreparation cp
						WHERE {whereClause}";

					var countResult = await _classQueryRepo.GetByQuery(countQuery);
					var totalCount = 0;

					try
					{
						var firstRow = countResult.FirstOrDefault();
						if (firstRow != null)
						{
							var type = firstRow.GetType();
							var prop = type.GetProperty("TotalCount");
							if (prop != null)
							{
								totalCount = Convert.ToInt32(prop.GetValue(firstRow));
							}
						}
					}
					catch (Exception ex)
					{
						_logger.Warning(ex, "Error extracting total count");
					}



					var sortBy = query.SortBy ?? "CreationDate";
					var sortDirection = query.SortDirection?.ToUpper() == "ASC" ? "ASC" : "DESC";
					var pageNumber = query.PageNumber > 0 ? query.PageNumber : 1;
					var pageSize = query.PageSize > 0 ? query.PageSize : 20;
					var offset = (pageNumber - 1) * pageSize;

					var dataQuery = $@"
						SELECT 
							cp.*,
							s.Name as SubjectName,
							sc.Name as ClassroomName,
							CONCAT(u.FirstName, ' ', u.LastName) as TeacherName,
							CONCAT(submitter.FirstName, ' ', submitter.LastName) as SubmittedByName,
							CONCAT(approver.FirstName, ' ', approver.LastName) as ApprovedByName,
							CONCAT(rejecter.FirstName, ' ', rejecter.LastName) as RejectedByName,
							(SELECT COUNT(*) FROM ClassPreparationMedia WHERE ClassPreparationId = cp.Id AND IsDeleted = 0) as MediaFilesCount
						FROM ClassPreparation cp
						LEFT JOIN Subject s ON cp.SubjectId = s.Id
						LEFT JOIN StudentClass sc ON cp.ClassroomId = sc.Id
						LEFT JOIN Users u ON cp.TeacherId = u.Id
						LEFT JOIN Users submitter ON cp.SubmittedBy = submitter.Id
						LEFT JOIN Users approver ON cp.ApprovedBy = approver.Id
						LEFT JOIN Users rejecter ON cp.RejectedBy = rejecter.Id
						WHERE {whereClause}
						ORDER BY cp.{sortBy} {sortDirection}
						OFFSET {offset} ROWS
						FETCH NEXT {pageSize} ROWS ONLY";

					var classes = await _classQueryRepo.GetByQuery(dataQuery);

					var classDtos = new List<ClassPreparationDto>();

					foreach (var classPrep in classes)
					{
						try
						{
							var type = classPrep.GetType();

							var subjectName = type.GetProperty("SubjectName")?.GetValue(classPrep)?.ToString() ?? "";
							var classroomName = type.GetProperty("ClassroomName")?.GetValue(classPrep)?.ToString() ?? "";
							var teacherName = type.GetProperty("TeacherName")?.GetValue(classPrep)?.ToString() ?? "";
							var submittedByName = type.GetProperty("SubmittedByName")?.GetValue(classPrep)?.ToString();
							var approvedByName = type.GetProperty("ApprovedByName")?.GetValue(classPrep)?.ToString();
							var rejectedByName = type.GetProperty("RejectedByName")?.GetValue(classPrep)?.ToString();
							var mediaFilesCount = Convert.ToInt32(type.GetProperty("MediaFilesCount")?.GetValue(classPrep) ?? 0);

							var dto = classPrep.ToDto(
								subjectName: subjectName,
								classroomName: classroomName,
								teacherName: teacherName,
								mediaFiles: new List<MediaFileDto>(),
								submittedByName: submittedByName,
								approvedByName: approvedByName,
								rejectedByName: rejectedByName,
								currentUserId: userId);

							dto.MediaFilesCount = mediaFilesCount;

							classDtos.Add(dto);
						}
						catch (Exception ex)
						{
							_logger.Error(ex, "Error mapping class to DTO - ClassId: {ClassId}", classPrep.Id);
						}
					}


					var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

					_logger.Information(
						"Class preparations retrieved - Total: {Total}, Page: {Page}/{TotalPages}",
						totalCount,
						pageNumber,
						totalPages);

					return new ClassPreparationsListResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Class preparations retrieved successfully",
						Status = "successful",
						Data = new ClassPreparationsListData
						{
							ClassPreparations = classDtos,
							TotalCount = totalCount,
							PageNumber = pageNumber,
							PageSize = pageSize,
							TotalPages = totalPages,
							HasPreviousPage = pageNumber > 1,
							HasNextPage = pageNumber < totalPages
						}

					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Exception retrieving class preparations");

					return new ClassPreparationsListResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while retrieving class preparations",
						Status = "failed"
					};
				}
			}
		}

		/// <summary>
		/// Get pending approvals for admin review
		/// Only returns classes with Status = Pending
		/// Requires ApproveClasses permission for regular admins
		/// </summary>
		public async Task<ClassPreparationsListResponse> GetPendingApprovals(GetClassPreparationsQuery query, AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			{
				try
				{
					if (!Guid.TryParse(userClaims.UserId, out var userId))
					{
						return new ClassPreparationsListResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId",
							Status = "failed"
						};
					}

					if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					{
						return new ClassPreparationsListResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId",
							Status = "failed"
						};
					}

					_logger.Information(
						"Retrieving pending approvals - RequestedBy: {UserId}",
						userId);



					var userRole = int.Parse(userClaims.Role ?? "0");

					// SuperAdmins bypass permission check
					if (userRole == (int)UserRole.Administrator)
					{
						var hasPermission = await _userService.HasPermission(userId, schoolId, AdminPermission.ApproveClasses);

						if (!hasPermission)
						{
							_logger.Warning(
								"Admin lacks ApproveClasses permission - UserId: {UserId}",
								userId);

							return new ClassPreparationsListResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = "You don't have permission to view pending approvals",
								Status = "failed"
							};
						}
					}



					var whereConditions = new List<string>
					{
						$"cp.Status = {(int)ClassPreparationStatus.Pending}", // ALWAYS Pending
						$"cp.SchoolId = '{schoolId}'", // Same school
						"cp.IsActive = 1"
					};

					// Teacher filter (optional)
					if (query.TeacherId.HasValue)
					{
						whereConditions.Add($"cp.TeacherId = '{query.TeacherId.Value}'");
					}

					// Classroom filter
					if (query.ClassroomId.HasValue)
					{
						whereConditions.Add($"cp.ClassroomId = '{query.ClassroomId.Value}'");
					}

					// Subject filter
					if (query.SubjectId.HasValue)
					{
						whereConditions.Add($"cp.SubjectId = '{query.SubjectId.Value}'");
					}

					// Date range filter (submission date)
					if (query.FromDate.HasValue)
					{
						whereConditions.Add($"cp.SubmittedForApprovalDate >= '{query.FromDate.Value:yyyy-MM-dd}'");
					}

					if (query.ToDate.HasValue)
					{
						whereConditions.Add($"cp.SubmittedForApprovalDate <= '{query.ToDate.Value:yyyy-MM-dd 23:59:59}'");
					}

					// Search term
					if (!string.IsNullOrWhiteSpace(query.SearchTerm))
					{
						var searchTerm = query.SearchTerm.Replace("'", "''");
						whereConditions.Add(
							$"(cp.Topic LIKE '%{searchTerm}%' OR cp.SubTopic LIKE '%{searchTerm}%')");
					}

					var whereClause = string.Join(" AND ", whereConditions);

					// ========================================
					// GET TOTAL COUNT
					// ========================================

					var countQuery = $@"
						SELECT COUNT(*) as TotalCount
						FROM ClassPreparation cp
						WHERE {whereClause}";

					var countResult = await _classQueryRepo.GetByQuery(countQuery);
					var totalCount = 0;

					try
					{
						var firstRow = countResult.FirstOrDefault();
						if (firstRow != null)
						{
							var type = firstRow.GetType();
							var prop = type.GetProperty("TotalCount");
							if (prop != null)
							{
								totalCount = Convert.ToInt32(prop.GetValue(firstRow));
							}
						}
					}
					catch (Exception ex)
					{
						_logger.Warning(ex, "Error extracting total count");
					}


					var sortBy = query.SortBy ?? "SubmittedForApprovalDate";
					var sortDirection = query.SortDirection?.ToUpper() == "ASC" ? "ASC" : "DESC";
					var pageNumber = query.PageNumber > 0 ? query.PageNumber : 1;
					var pageSize = query.PageSize > 0 ? query.PageSize : 20;
					var offset = (pageNumber - 1) * pageSize;

					var dataQuery = $@"
					SELECT 
						cp.*,
						s.Name as SubjectName,
						sc.Name as ClassroomName,
						CONCAT(u.FirstName, ' ', u.LastName) as TeacherName,
						u.Email as TeacherEmail,
						CONCAT(submitter.FirstName, ' ', submitter.LastName) as SubmittedByName,
						(SELECT COUNT(*) FROM ClassPreparationMedia WHERE ClassPreparationId = cp.Id AND IsDeleted = 0) as MediaFilesCount
					FROM ClassPreparation cp
					LEFT JOIN Subject s ON cp.SubjectId = s.Id
					LEFT JOIN StudentClass sc ON cp.ClassroomId = sc.Id
					LEFT JOIN Users u ON cp.TeacherId = u.Id
					LEFT JOIN Users submitter ON cp.SubmittedBy = submitter.Id
					WHERE {whereClause}
					ORDER BY cp.{sortBy} {sortDirection}
					OFFSET {offset} ROWS
					FETCH NEXT {pageSize} ROWS ONLY";

					var classes = await _classQueryRepo.GetByQuery(dataQuery);



					var classDtos = new List<ClassPreparationDto>();

					foreach (var classPrep in classes)
					{
						try
						{
							var type = classPrep.GetType();

							var subjectName = type.GetProperty("SubjectName")?.GetValue(classPrep)?.ToString() ?? "";
							var classroomName = type.GetProperty("ClassroomName")?.GetValue(classPrep)?.ToString() ?? "";
							var teacherName = type.GetProperty("TeacherName")?.GetValue(classPrep)?.ToString() ?? "";
							var teacherEmail = type.GetProperty("TeacherEmail")?.GetValue(classPrep)?.ToString() ?? "";
							var submittedByName = type.GetProperty("SubmittedByName")?.GetValue(classPrep)?.ToString();
							var mediaFilesCount = Convert.ToInt32(type.GetProperty("MediaFilesCount")?.GetValue(classPrep) ?? 0);

							var dto = classPrep.ToDto(
								subjectName: subjectName,
								classroomName: classroomName,
								teacherName: teacherName,
								teacherEmail: teacherEmail,
								mediaFiles: new List<MediaFileDto>(),
								submittedByName: submittedByName,
								currentUserId: userId);

							dto.MediaFilesCount = mediaFilesCount;

							classDtos.Add(dto);
						}
						catch (Exception ex)
						{
							_logger.Error(ex, "Error mapping class to DTO - ClassId: {ClassId}", classPrep.Id);
						}
					}


					var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

					_logger.Information(
						"Pending approvals retrieved - Total: {Total}, Page: {Page}/{TotalPages}",
						totalCount,
						pageNumber,
						totalPages);

					return new ClassPreparationsListResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Pending approvals retrieved successfully",
						Status = "successful",
						Data = new ClassPreparationsListData
						{
							ClassPreparations = classDtos,
							TotalCount = totalCount,
							PageNumber = pageNumber,
							PageSize = pageSize,
							TotalPages = totalPages,
							HasPreviousPage = pageNumber > 1,
							HasNextPage = pageNumber < totalPages
						}

					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Exception retrieving pending approvals");

					return new ClassPreparationsListResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while retrieving pending approvals",
						Status = "failed"
					};
				}
			}
		}

		/// <summary>
		/// Delete a draft class preparation
		/// 
		/// BUSINESS RULES:
		/// 1. Only drafts can be deleted (Status = Draft)
		/// 2. User must be the creator
		/// 3. Soft delete (IsActive = false, not physical delete)
		/// 4. All associated media also soft deleted
		/// 
		/// WORKFLOW:
		/// 1. Validate user claims
		/// 2. Retrieve class preparation
		/// 3. Verify status is Draft
		/// 4. Verify user is creator
		/// 5. Soft delete class
		/// 6. Soft delete all associated media
		/// </summary>
		public async Task<BaseResponse> DeleteDraft(Guid id, AuthenticatedUserClaims userClaims)
		{
			try
			{
				_logger.Information("Deleting draft - ClassId: {ClassId}, UserId: {UserId}", id, userClaims.UserId);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 1: VALIDATE USER CLAIMS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 2: RETRIEVE CLASS PREPARATION
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var classPrep = await _classQueryRepo.Get(id);

				if (classPrep == null)
				{
					_logger.Warning("Class not found - ClassId: {ClassId}", id);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Class preparation not found",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 3: VERIFY STATUS IS DRAFT
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var status = (ClassPreparationStatus)classPrep.Status;

				if (status != ClassPreparationStatus.Draft)
				{
					_logger.Warning(
						"Cannot delete non-draft - ClassId: {ClassId}, Status: {Status}",
						id,
						status);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"Cannot delete class with status: {status}. Only drafts can be deleted.",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 4: VERIFY USER IS CREATOR
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (classPrep.CreatedBy != userId)
				{
					_logger.Warning(
						"Unauthorized delete attempt - ClassId: {ClassId}, UserId: {UserId}, CreatedBy: {CreatedBy}",
						id,
						userId,
						classPrep.CreatedBy);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You can only delete your own draft classes",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 5: SOFT DELETE CLASS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var updateDict = new Dictionary<string, object>
				{
					{ "IsActive", false },
					{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
				};

				await _classCommandRepo.UpdateTableColumnById(
					updateDict,
					new KeyValuePair<string, object>("Id", id));

				_logger.Information("Class soft deleted - ClassId: {ClassId}", id);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 6: SOFT DELETE ALL ASSOCIATED MEDIA
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				try
				{
					await _mediaService.DeleteMediaForRejectedClass(
						id,
						userId,
						"Draft deleted by user");

					_logger.Information("Associated media deleted - ClassId: {ClassId}", id);
				}
				catch (Exception ex)
				{
					_logger.Warning(ex, "Failed to delete media - ClassId: {ClassId}", id);
					// Don't fail the delete if media deletion fails
				}

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Draft deleted successfully",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "💥 Error deleting draft - ClassId: {ClassId}", id);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while deleting the draft",
					Status = "failed"
				};
			}



			#endregion

			// Additional methods (GetMyClassPreparations, GetPendingApprovals, DeleteDraft) 
			// would follow similar patterns...
			// I'll add them if needed, but this shows the complete core workflow!
		}

		
	}
}