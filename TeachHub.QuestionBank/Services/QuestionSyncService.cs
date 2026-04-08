using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.DTO;
using TechHub.QuestionBank.Core.Entities;
using TechHub.QuestionBank.Core.Enums;
using TechHub.QuestionBank.Core.Response;
using TechHub.QuestionBank.Core.ViewModel;
using TechHub.QuestionBank.Services.interfaces;
using TechHub.Service.Interface;

namespace TechHub.QuestionBank.Services;

public class QuestionSyncService : IQuestionSyncService
{
	private readonly IQueryRepository<Questions> _questionQueryRepo;
	private readonly ICommandRespository<Questions> _questionCommandRepo;
	private readonly IQueryRepository<QuestionOptions> _optionQueryRepo;
	private readonly ICommandRespository<QuestionOptions> _optionCommandRepo;
	private readonly IQuestionService _questionService;
	private readonly ILogger _logger;

	public QuestionSyncService(IQueryRepository<Questions> questionQueryRepo,ICommandRespository<Questions> questionCommandRepo,IQueryRepository<QuestionOptions> optionQueryRepo,
		ICommandRespository<QuestionOptions> optionCommandRepo,IQuestionService questionService,ILogger logger)
	{
		_questionQueryRepo = questionQueryRepo;
		_questionCommandRepo = questionCommandRepo;
		_optionQueryRepo = optionQueryRepo;
		_optionCommandRepo = optionCommandRepo;
		_questionService = questionService;
		_logger = logger;
	}

	/// <summary>
	/// Sync batch of offline-created or edited questions
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///    - Parse and validate UserId and SchoolId
	///
	/// 2. VALIDATE BATCH
	///    - Batch must not be empty
	///    - Batch size capped to prevent abuse
	///
	/// 3. PROCESS EACH QUESTION
	///    For each question in batch:
	///
	///    a. NEW QUESTION (no ServerId)
	///       - ClientId not found on server
	///       - Delegate to QuestionService.CreateQuestion
	///       - Map ClientId to new ServerId in result
	///
	///    b. EXISTING QUESTION (ServerId present)
	///       - Previously synced, edited offline
	///       - Check dirty state before updating
	///       - Conflict detected → add to conflicts list
	///       - No conflict → delegate to UpdateQuestion
	///
	///    c. FAILED QUESTION
	///       - Any exception during processing
	///       - Added to failed list
	///       - Does not block rest of batch
	///       - Frontend retries failed items
	///
	/// 4. RETURN SYNC RESULT
	///    - SyncedItems: ClientId → ServerId maps
	///    - FailedClientIds: items to retry
	///    - Conflicts: items needing teacher resolution
	///
	/// OFFLINE CONSIDERATION:
	/// - Batch may contain mix of new and edited questions
	/// - Each processed independently
	/// - Partial success is valid — not all or nothing
	/// - Frontend cleans local storage only for
	///   successfully synced items
	/// - Failed items remain in local storage for retry
	/// </summary>
	public async Task<SyncResponse> SyncQuestions(SyncQuestionsViewModel model,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("DeviceId", model.DeviceId))
		{
			try
			{
				_logger.Information("Starting sync - " +"DeviceId: {DeviceId}, " +"BatchSize: {BatchSize}, " +"UserId: {UserId}",
					model.DeviceId,
					model.Questions?.Count ?? 0,
					userClaims.UserId);

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new SyncResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new SyncResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				if (model.Questions == null || !model.Questions.Any())
				{
					return new SyncResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "No questions provided for sync",
						Status = "failed"
					};
				}

				// Cap batch size to prevent abuse
				// and memory pressure
				if (model.Questions.Count > 50)
				{
					return new SyncResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage ="Sync batch cannot exceed 50 questions. " + "Split into smaller batches",
						Status = "failed"
					};
				}

				var syncedItems = new List<SyncedItemMap>();
				var failedClientIds = new List<string>();
				var conflicts = new List<ConflictItem>();

				foreach (var questionModel in model.Questions)
				{
					try
					{

						if (questionModel.Status == QuestionStatus.PendingReview)
						{
							_logger.Warning("Cannot sync PendingReview question - " +"ClientId: {ClientId}",questionModel.ClientId);

							failedClientIds.Add(questionModel.ClientId);
							// Inform frontend this question
							// cannot sync until teacher verifies it
							continue;
						}
						var isNewQuestion = !questionModel.ServerId.HasValue || questionModel.ServerId == Guid.Empty;
						// Determine if this is a new question
						// or an edit to an existing synced question
						//var isNewQuestion =!questionModel.ServerId.HasValue || questionModel.ServerId == Guid.Empty;

						if (isNewQuestion)
						{
							// ─────────────────────────────────────────
							// NEW QUESTION
							// Never been on server before
							// Delegate to CreateQuestion
							// ─────────────────────────────────────────

							_logger.Information("Syncing new question - " +"ClientId: {ClientId}", questionModel.ClientId);
							var createModel = MapToCreateViewModel(questionModel);
							var createResult = await _questionService.CreateQuestion(createModel, userClaims);

							if (createResult.ResponseCode == ResponseCode.successful)
							{
								syncedItems.Add(new SyncedItemMap
								{
									ClientId = questionModel.ClientId,
									ServerId = createResult.QuestionId,
									SyncedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
									IsDuplicate = createResult.IsDuplicate
								});

								_logger.Information("New question synced - " +"ClientId: {ClientId}, " +"ServerId: {ServerId}",questionModel.ClientId,createResult.QuestionId);
							}
							else
							{
								_logger.Warning("New question sync failed - " +"ClientId: {ClientId}, " +"Reason: {Reason}",questionModel.ClientId,createResult.ResponseMessage);

								failedClientIds.Add(questionModel.ClientId);
							}
						}
						else
						{
							// ─────────────────────────────────────────
							// EXISTING QUESTION
							// Previously synced, edited offline
							// Delegate to UpdateQuestion
							// UpdateQuestion handles dirty detection
							// and returns conflict if detected
							// ─────────────────────────────────────────

							_logger.Information("Syncing edited question - " +"ClientId: {ClientId}, " +"ServerId: {ServerId}",questionModel.ClientId, questionModel.ServerId);

							// Map SyncViewModel to UpdateViewModel
							var updateModel =MapToUpdateViewModel(questionModel);

							var updateResult = await _questionService.UpdateQuestion(updateModel,userClaims);

							if (updateResult.IsConflict)
							{
								// Conflict detected
								// Add to conflicts list
								// Frontend shows teacher both versions
								// Teacher resolves via ResolveConflict
								conflicts.Add(new ConflictItem
								{
									ClientId = questionModel.ClientId,
									ServerId = questionModel.ServerId!.Value,
									ConflictReason =updateResult.ConflictDetail.ReasonDescription,
									ServerVersion =updateResult.ConflictDetail.ServerVersion
								});

								_logger.Warning("Conflict detected - " +"ClientId: {ClientId}, " +"ServerId: {ServerId}",questionModel.ClientId,questionModel.ServerId);
							}
							else if (updateResult.ResponseCode
								== ResponseCode.successful)
							{
								syncedItems.Add(new SyncedItemMap
								{
									ClientId = questionModel.ClientId,
									ServerId = questionModel.ServerId!.Value,
									SyncedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
									IsDuplicate = false
								});

								_logger.Information("Edited question synced - " +"ClientId: {ClientId}, " +"ServerId: {ServerId}",questionModel.ClientId,questionModel.ServerId);
							}
							else
							{
								_logger.Warning("Edited question sync failed - " +"ClientId: {ClientId}, " +"Reason: {Reason}",questionModel.ClientId,updateResult.ResponseMessage);

								failedClientIds.Add(questionModel.ClientId);
							}
						}
					}
					catch (Exception ex)
					{
						// Individual question failure
						// does not block the rest of the batch
						_logger.Error(ex,"Error syncing question - " +"ClientId: {ClientId}",questionModel.ClientId);

						failedClientIds.Add(questionModel.ClientId);
					}
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 4: RETURN SYNC RESULT
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var totalProcessed = model.Questions.Count;
				var totalSynced = syncedItems.Count;
				var totalFailed = failedClientIds.Count;
				var totalConflicts = conflicts.Count;

				_logger.Information(
					"Sync completed - " +"Total: {Total}, " +"Synced: {Synced}, " +"Failed: {Failed}, " +"Conflicts: {Conflicts}",
					totalProcessed,totalSynced,totalFailed,totalConflicts);

				// Sync is considered successful even with
				// partial failures or conflicts
				// Frontend handles each category differently:
				// SyncedItems   → clean local storage
				// FailedItems   → keep local, retry later
				// Conflicts     → present to teacher for resolution
				return new SyncResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage =$"Sync completed. " +$"Synced: {totalSynced}, " +$"Failed: {totalFailed}, " +$"Conflicts: {totalConflicts}",
					Status = "successful",
					SyncedItems = syncedItems,
					FailedClientIds = failedClientIds,
					Conflicts = conflicts
				};
			}
			catch (Exception ex)
			{
				_logger.Error(
					ex,"Error during sync batch - " +"DeviceId: {DeviceId}",model.DeviceId);

				return new SyncResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage ="An error occurred during sync",
					Status = "failed",
					SyncedItems = new List<SyncedItemMap>(),
					FailedClientIds = model.Questions?
						.Select(q => q.ClientId)
						.ToList()
						?? new List<string>(),
					// All items failed
					// Frontend retries entire batch
					Conflicts = new List<ConflictItem>()
				};
			}
		}
	}

	/// <summary>
	/// Check for conflicts before sync attempt
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///
	/// 2. VALIDATE REQUEST
	///    - Must have at least one fingerprint
	///
	/// 3. PROCESS EACH FINGERPRINT
	///    For each local question fingerprint:
	///
	///    a. NO SERVERID
	///       - Never synced before
	///       - Treat as new to server
	///       - Safe to sync
	///
	///    b. SERVERID PRESENT
	///       - Fetch server version
	///       - If not found → server deleted it
	///         → conflict (ServerDeletedLocally)
	///       - If ModifiedDate differs from
	///         LastModifiedAtDevice
	///         → conflict (BothVersionsEdited)
	///       - If ModifiedDate matches
	///         → safe to sync
	///
	/// 4. RETURN CONFLICT CHECK RESULT
	/// </summary>
	public async Task<ConflictCheckResponse> CheckForConflicts(ConflictCheckViewModel model,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("DeviceId", model.DeviceId))
		{
			try
			{
				_logger.Information("Checking conflicts - " +"DeviceId: {DeviceId}, " +"Count: {Count}",model.DeviceId,model.LocalQuestions?.Count ?? 0);				

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new ConflictCheckResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new ConflictCheckResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				if (model.LocalQuestions == null|| !model.LocalQuestions.Any())
				{
					return new ConflictCheckResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage ="No question fingerprints provided",
						Status = "failed"
					};
				}


				var safeToSync = new List<string>();
				var newToServer = new List<string>();
				var conflicts = new List<ConflictDetail>();

				foreach (var fingerprint in model.LocalQuestions)
				{
					try
					{
						// Never synced — new to server
						if (!fingerprint.ServerId.HasValue || fingerprint.ServerId == Guid.Empty)
						{
							newToServer.Add(fingerprint.ClientId);
							continue;
						}

						// Fetch server version of this question
						var serverQuestion =
							await _questionQueryRepo.Get(fingerprint.ServerId.Value,DatabaseTarget.QuestionBank);

						// Server deleted this question
						if (serverQuestion == null || serverQuestion.IsDeleted)
						{
							conflicts.Add(new ConflictDetail
							{
								ClientId = fingerprint.ClientId,
								ServerId = fingerprint.ServerId!.Value,
								Reason =
									ConflictReason.ServerDeletedLocally,
								ReasonDescription ="This question was deleted from " +"the server by another user. " +"You can keep your local version " +"or discard it",
								LocalModifiedAt =fingerprint.LastModifiedAtDevice,
								ServerModifiedAt = null
								// No server version to show
							});

							continue;
						}

						// Cross-tenant safety check
						if (serverQuestion.SchoolId != schoolId)
						{
							// Should never happen
							// Log as security event
							_logger.Warning("Cross-tenant fingerprint detected - " +"ServerId: {ServerId}, " +"SchoolId: {SchoolId}",fingerprint.ServerId,schoolId);

						    failedFingerprints:continue;
						}

						// Compare modification timestamps
						var serverModified = serverQuestion
							.ModifiedDate?.Trim();
						var clientModified = fingerprint
							.LastModifiedAtDevice?.Trim();

						if (serverModified != clientModified)
						{
							// Both versions changed since last sync
							// Fetch full server DTO for conflict screen
							var serverDto = MapQueryResultToDto(
								serverQuestion);

							conflicts.Add(new ConflictDetail
							{
								ClientId = fingerprint.ClientId,
								ServerId = fingerprint.ServerId!.Value,
								Reason =ConflictReason.BothVersionsEdited,
								ReasonDescription ="This question was edited on " +"another device after your " +"last sync",
								LocalModifiedAt = clientModified,
								ServerModifiedAt = serverModified,
								ServerVersion = serverDto
							});
						}
						else
						{
							// Timestamps match — safe to sync
							safeToSync.Add(fingerprint.ClientId);
						}
					}
					catch (Exception ex)
					{
						_logger.Error(
							ex,"Error checking fingerprint - " +"ClientId: {ClientId}", fingerprint.ClientId);
						// Skip this fingerprint
						// Do not block the rest of the check
					}
				}

				_logger.Information(
					"Conflict check completed - " +"Safe: {Safe}, " +"New: {New}, " +"Conflicts: {Conflicts}",safeToSync.Count,newToServer.Count,conflicts.Count);


				return new ConflictCheckResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Conflict check completed",
					Status = "successful",
					SafeToSync = safeToSync,
					NewToServer = newToServer,
					Conflicts = conflicts,
					TotalChecked = model.LocalQuestions.Count,
					TotalConflicts = conflicts.Count,
					TotalSafeToSync =safeToSync.Count + newToServer.Count
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,"Error during conflict check - " +"DeviceId: {DeviceId}",model.DeviceId);

				return new ConflictCheckResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage ="An error occurred during conflict check",
					Status = "failed"
				};
			}
		}
	}

	/// <summary>
	/// Resolve conflicts after teacher makes decision
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///
	/// 2. VALIDATE REQUEST
	///
	/// 3. PROCESS EACH RESOLUTION
	///
	///    KeepLocal
	///    → Update server with local version
	///    → Frontend cleans local record after confirm
	///
	///    KeepServer
	///    → No server update needed
	///    → Tell frontend to overwrite local with server
	///    → Frontend cleans local record
	///
	///    KeepMerged
	///    → Update server with merged version
	///    → Frontend cleans local record after confirm
	///
	///    DiscardLocal
	///    → No server update needed
	///    → Tell frontend to discard local entirely
	///
	/// 4. RETURN RESOLUTION RESULT
	/// </summary>
	public async Task<BaseResponse> ResolveConflict(ResolveConflictViewModel model,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("DeviceId", model.DeviceId))
		{
			try
			{
				_logger.Information(
					"Resolving conflicts - " +"DeviceId: {DeviceId}, " + "Count: {Count}",model.DeviceId,model.Resolutions?.Count ?? 0);

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}


				if (model.Resolutions == null || !model.Resolutions.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "No resolutions provided",
						Status = "failed"
					};
				}

				var resolved = new List<ResolvedItem>();
				var failed = new List<string>();

				foreach (var resolution in model.Resolutions)
				{
					try
					{
						var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

						switch (resolution.Choice)
						{
							case ResolutionChoice.KeepLocal:
							case ResolutionChoice.KeepMerged:


								var dataToSave = resolution.Choice == ResolutionChoice.KeepMerged ? resolution.MergedData: resolution.LocalData;

								var updateModel = MapToUpdateViewModel(dataToSave);

								updateModel.QuestionId = resolution.ServerId;

								// Bypass dirty detection
								// Teacher has explicitly chosen
								// to overwrite server version
								// LastKnownModifiedDate intentionally
								// left null here
								updateModel.LastKnownModifiedDate = null;

								var updateResult = await _questionService.UpdateQuestion(updateModel, userClaims);

								if (updateResult.ResponseCode == ResponseCode.successful)
								{
									resolved.Add(new ResolvedItem
									{
										ClientId =resolution.ClientId,
										ServerId =resolution.ServerId,
										AppliedChoice =resolution.Choice,
										ResolvedAt = now,
										ShouldCleanLocalRecord = true
										// Server now has correct version
										// Frontend cleans full local data
										// Keeps only lightweight reference
									});

									_logger.Information(
										"Conflict resolved - " +"ClientId: {ClientId}, " +"Choice: {Choice}",resolution.ClientId,resolution.Choice);
								}
								else
								{
									failed.Add(resolution.ClientId);
								}

								break;

							case ResolutionChoice.KeepServer:

								resolved.Add(new ResolvedItem
								{
									ClientId = resolution.ClientId,
									ServerId = resolution.ServerId,
									AppliedChoice =ResolutionChoice.KeepServer,
									ResolvedAt = now,
									ShouldCleanLocalRecord = true
									// Frontend replaces local data
									// with server version
									// then cleans to lightweight ref
								});

								_logger.Information("Conflict resolved - " +"ClientId: {ClientId}, " + "Choice: KeepServer", resolution.ClientId);

								break;

							case ResolutionChoice.DiscardLocal:

								// ─────────────────────────────────────
								// Teacher abandons local changes
								// No server update needed
								// Frontend discards local record
								// ─────────────────────────────────────

								resolved.Add(new ResolvedItem
								{
									ClientId = resolution.ClientId,
									ServerId = resolution.ServerId,
									AppliedChoice =ResolutionChoice.DiscardLocal,
									ResolvedAt = now,
									ShouldCleanLocalRecord = true
									// Frontend removes local record
									// entirely — no server version kept
								});

								_logger.Information("Local changes discarded - " +"ClientId: {ClientId}",resolution.ClientId);

								break;

							default:
								_logger.Warning("Unknown resolution choice - " +"ClientId: {ClientId}, " +"Choice: {Choice}",resolution.ClientId,resolution.Choice);

								failed.Add(resolution.ClientId);
								break;
						}
					}
					catch (Exception ex)
					{
						_logger.Error(
							ex,"Error resolving conflict - " +"ClientId: {ClientId}",resolution.ClientId);

						failed.Add(resolution.ClientId);
					}
				}

				_logger.Information("Conflict resolution completed - " +"Resolved: {Resolved}, " + "Failed: {Failed}", resolved.Count,failed.Count);

				return new ConflictResolutionResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage =$"Resolved {resolved.Count} conflicts",
					Status = "successful",
					Resolved = resolved,
					Failed = failed
				};
			}
			catch (Exception ex)
			{
				_logger.Error(
					ex,
					"Error during conflict resolution - " +"DeviceId: {DeviceId}",model.DeviceId);

				return new ConflictResolutionResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage ="An error occurred during conflict resolution",
					Status = "failed",
					Resolved = new List<ResolvedItem>(),
					Failed = model.Resolutions?.Select(r => r.ClientId).ToList() ?? new List<string>()
				};
			}
		}
	}

	#region Private Helpers

	private CreateQuestionViewModel MapToCreateViewModel(SyncQuestionViewModel model)
	{
		return new CreateQuestionViewModel
		{
			ClientId = model.ClientId,
			OriginDevice = model.OriginDevice,
			CreatedAtDevice = model.CreatedAtDevice,
			SubjectId = model.SubjectId,
			TopicId = model.TopicId,
			Topic = model.Topic,
			SubTopic = model.SubTopic,
			Title = model.Title,
			TextContent = model.TextContent,
			QuestionType = model.QuestionType,
			DifficultyLevel = model.DifficultyLevel,
			MarksAllocation = model.MarksAllocation,
			Options = model.Options,
			BoardSessionId = model.BoardSessionId,
			ScanSessionId = model.ScanSessionId,
			IsScanned = model.IsScanned,
			ExtractedQuestionIndex = model.ExtractedQuestionIndex,
			AIConfidenceScore = model.AIConfidenceScore
		};
	}

	private UpdateQuestionViewModel MapToUpdateViewModel(SyncQuestionViewModel model)
	{
		return new UpdateQuestionViewModel
		{
			// ServerId becomes QuestionId
			// Safe — only called when isNewQuestion = false
			QuestionId = model.ServerId!.Value,
			ClientId = model.ClientId,
			OriginDevice = model.OriginDevice,
			EditedAtDevice = model.EditedAtDevice,
			SubjectId = model.SubjectId,
			TopicId = model.TopicId,
			Topic = model.Topic,
			SubTopic = model.SubTopic,
			Title = model.Title,
			TextContent = model.TextContent,
			QuestionType = model.QuestionType,
			DifficultyLevel = model.DifficultyLevel,
			MarksAllocation = model.MarksAllocation,
			Options = model.Options,
			BoardSessionId = model.BoardSessionId,
			LastKnownModifiedDate = model.LastKnownModifiedDate
		};
	}


	/// <summary>
	/// Maps Question entity to lightweight QuestionDto
	/// Used in conflict detail — no options loaded
	/// Full detail fetched separately if needed
	/// </summary>
	private QuestionDto MapQueryResultToDto(Questions question)
	{
		return new QuestionDto
		{
			Id = question.Id,
			ClientId = question.ClientId,
			SubjectId = question.SubjectId,
			TopicId = question.TopicId,
			Topic = question.Topic,
			SubTopic = question.SubTopic,
			Title = question.Title,
			TextContent = question.TextContent,
			QuestionType = (int)question.QuestionType,
			QuestionTypeName = question.QuestionType.ToString(),
			DifficultyLevel = (int)question.DifficultyLevel,
			DifficultyLevelName = question.DifficultyLevel.ToString(),
			MarksAllocation = question.MarksAllocation,
			HasBoardSession = question.HasBoardSession,
			HasMedia = question.HasMedia,
			HasAudio = question.HasAudio,
			IsScanned = question.IsScanned,
			Status = (int)question.Status,
			StatusName = question.Status.ToString(),
			CreationDate = question.CreationDate,
			ModifiedDate = question.ModifiedDate
		};
	}

	#endregion
}

