using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.DTO;
using TechHub.QuestionBank.Core.Entities;
using TechHub.QuestionBank.Core.Enums;
using TechHub.QuestionBank.Core.Helpers;
using TechHub.QuestionBank.Core.ViewModel;
using TechHub.QuestionBank.Services.interfaces;
using TechHub.Service.Interface;

namespace TechHub.QuestionBank.Services;

public class QuestionBoardService : IQuestionBoardService
{
	private readonly IQueryRepository<Questions> _questionQueryRepo;
	private readonly ICommandRespository<Questions> _questionCommandRepo;
	private readonly ICloudinaryService _cloudinaryService;
	private readonly IQueryRepository<QuestionJob> _jobQueryRepo;
	private readonly IQueryRepository<QuestionOptions> _optionQueryRepo;
	private readonly ILogger _logger;

	public QuestionBoardService(IQueryRepository<Questions> questionQueryRepo, ICommandRespository<Questions> questionCommandRepo,
		 ICloudinaryService cloudinaryService, IQueryRepository<QuestionJob> jobQueryRepo, IQueryRepository<QuestionOptions> optionQueryRepo, ILogger logger)
	{
		_questionQueryRepo = questionQueryRepo;
		_questionCommandRepo = questionCommandRepo;
		_cloudinaryService = cloudinaryService;
		_jobQueryRepo = jobQueryRepo;
		_optionQueryRepo = optionQueryRepo;
		_logger = logger;
	}

	/// <summary>
	/// Attach board session to question
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///    - Parse UserId and SchoolId
	///
	/// 2. VALIDATE INPUT
	///    - QuestionId required
	///    - BoardSessionId required
	///    - SnapshotUrl required
	///    - SnapshotPublicId required
	///      (needed for future CDN deletion)
	///
	/// 3. RETRIEVE QUESTION
	///    - Verify question exists
	///    - Verify not deleted
	///
	/// 4. CROSS-TENANT CHECK
	///    - Question must belong to school
	///
	/// 5. OWNERSHIP CHECK
	///    - Only creator or admin can attach
	///
	/// 6. STATUS CHECK
	///    - Only Draft or PendingReview questions
	///      can have board sessions attached
	///    - Published questions are locked
	///      Must unpublish first
	///
	/// 7. HANDLE EXISTING BOARD SESSION
	///    - If question already has a board session
	///      store old SnapshotPublicId for CDN cleanup
	///      Will be deleted after new one attached
	///
	/// 8. ATTACH BOARD SESSION
	///    - Update question with:
	///      BoardSessionId
	///      SnapshotUrl
	///      SnapshotPublicId
	///      HasBoardSession = true
	///      QuestionType = BoardBased or Mixed
	///
	/// 9. TRIGGER OLD SNAPSHOT CLEANUP
	///    - If old snapshot existed
	///      delete from CDN
	///      Fire and forget — does not block response
	///
	/// 10. RETURN RESPONSE
	///     - Return QuestionId, BoardSessionId, SnapshotUrl
	///     - Frontend updates local record immediately
	/// </summary>
	public async Task<AttachBoardSessionResponse> AttachBoardSession(AttachBoardSessionViewModel model,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("QuestionId", model.QuestionId))
		{
			try
			{
				_logger.Information(
					"Attaching board session - " + "QuestionId: {QuestionId}, " + "BoardSessionId: {SessionId}",
					model.QuestionId, model.BoardSessionId);

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new AttachBoardSessionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new AttachBoardSessionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				var isAdmin = ClaimsHelper.IsAdmin(userClaims.Role);

				if (model.QuestionId == Guid.Empty)
				{
					return new AttachBoardSessionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Question ID is required",
						Status = "failed"
					};
				}

				if (string.IsNullOrWhiteSpace(model.BoardSessionId))
				{
					return new AttachBoardSessionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Board session ID is required",
						Status = "failed"
					};
				}

				if (string.IsNullOrWhiteSpace(model.SnapshotUrl))
				{
					return new AttachBoardSessionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage ="Snapshot URL is required. " + "Generate and upload snapshot before attaching",
						Status = "failed"
					};
				}

				if (string.IsNullOrWhiteSpace(model.SnapshotPublicId))
				{
					return new AttachBoardSessionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage ="Snapshot public ID is required " + "for future cleanup",
						Status = "failed"
					};
				}
				var question = await _questionQueryRepo.Get(model.QuestionId, DatabaseTarget.QuestionBank);

				if (question == null || question.IsDeleted)
				{
					_logger.Warning("Question not found - " + "QuestionId: {QuestionId}",model.QuestionId);

					return new AttachBoardSessionResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				if (question.SchoolId != schoolId)
				{
					_logger.Warning("Cross-tenant access attempt - " +"QuestionId: {QuestionId}, " + "SchoolId: {SchoolId}",model.QuestionId, schoolId);

					return new AttachBoardSessionResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				if (!isAdmin && question.CreatedBy != userId)
				{
					_logger.Warning("Ownership check failed - " +"QuestionId: {QuestionId}, " +"RequestedBy: {UserId}", model.QuestionId,userId);

					return new AttachBoardSessionResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage =
							"You do not have permission to " +
							"attach board content to this question",
						Status = "failed"
					};
				}

				
				// Published questions are locked
				// Board content changes the question substance
				// Teacher must unpublish first
				if (question.Status == QuestionStatus.Published)
				{
					return new AttachBoardSessionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage ="Cannot attach board content to a " +"published question. " +"Unpublish it first",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 7: HANDLE EXISTING BOARD SESSION
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				// Store old snapshot public ID for cleanup
				// Teacher is replacing existing board content
				var oldSnapshotPublicId = question.SnapshotPublicId;

				var hasOldSession = !string.IsNullOrWhiteSpace(question.BoardSessionId.ToString()) && !string.IsNullOrWhiteSpace(oldSnapshotPublicId);

				if (hasOldSession)
				{
					_logger.Information("Replacing existing board session - " + "QuestionId: {QuestionId}, " +"OldSessionId: {OldSessionId}",model.QuestionId,question.BoardSessionId);
				}

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				// Determine updated question type
				// If question already has text content
				// it becomes Mixed (text + board)
				// If question has no text content
				// it becomes BoardBased
				var updatedQuestionType =!string.IsNullOrWhiteSpace(question.TextContent) ? QuestionType.Mixed : QuestionType.BoardBased;

				var updateDict = new Dictionary<string, object>
					{
						{ "BoardSessionId",    model.BoardSessionId },
						{ "SnapshotUrl",       model.SnapshotUrl },
						{ "SnapshotPublicId",  model.SnapshotPublicId },
						{ "HasBoardSession",   true },
						{ "QuestionType",      (int)updatedQuestionType },
						{ "ModifiedDate",      now }
					};

				await _questionCommandRepo.UpdateTableColumnById(updateDict, new KeyValuePair<string, object>("Id", model.QuestionId),DatabaseTarget.QuestionBank);

				_logger.Information("Board session attached - " + "QuestionId: {QuestionId}, " + "SessionId: {SessionId}", model.QuestionId, model.BoardSessionId);

				// Fire and forget
				// Old CDN snapshot deleted in background
				// Does not block response to teacher
				// If cleanup fails CDN cleanup job
				// will handle orphaned assets later
				if (!string.IsNullOrWhiteSpace(oldSnapshotPublicId))
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await DeleteCdnSnapshot(oldSnapshotPublicId);

							_logger.Information("Old snapshot deleted - " + "PublicId: {PublicId}", oldSnapshotPublicId);
						}
						catch (Exception ex)
						{
							_logger.Error(ex,"Old snapshot deletion failed - " + "PublicId: {PublicId}", oldSnapshotPublicId);
							// Cleanup job handles orphaned assets
						}
					});
				}


				return new AttachBoardSessionResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage =
						"Board session attached successfully",
					Status = "successful",
					QuestionId = model.QuestionId,
					BoardSessionId = model.BoardSessionId,
					SnapshotUrl = model.SnapshotUrl
					// Frontend updates local question record
					// with these values immediately
					// No need to refetch question
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,"Error attaching board session - " + "QuestionId: {QuestionId}", model.QuestionId);

				return new AttachBoardSessionResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage ="An error occurred while attaching " + "board session",
					Status = "failed"
				};
			}
		}
	}

	/// <summary>
	/// Detach board session from question
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///
	/// 2. RETRIEVE QUESTION
	///    - Verify exists and not deleted
	///
	/// 3. CROSS-TENANT CHECK
	///
	/// 4. OWNERSHIP CHECK
	///
	/// 5. STATUS CHECK
	///    - Published questions locked
	///
	/// 6. CHECK HAS BOARD SESSION
	///    - If no board session return success
	///      Idempotent — safe to retry
	///
	/// 7. CLEAR BOARD SESSION FROM QUESTION
	///    - Clear BoardSessionId
	///    - Clear SnapshotUrl
	///    - Clear SnapshotPublicId
	///    - HasBoardSession = false
	///    - Revert QuestionType if needed
	///
	/// 8. TRIGGER CDN SNAPSHOT CLEANUP
	///    - Delete old snapshot from CDN
	///    - Fire and forget
	///
	/// 9. RETURN RESPONSE
	/// </summary>
	public async Task<BaseResponse> DetachBoardSession(DetachBoardSessionViewModel model,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("QuestionId", model.QuestionId))
		{
			try
			{
				_logger.Information("Detaching board session - " +"QuestionId: {QuestionId}", model.QuestionId);

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

				var isAdmin = ClaimsHelper.IsAdmin(userClaims.Role);

				var question = await _questionQueryRepo.Get(model.QuestionId, DatabaseTarget.QuestionBank);

				if (question == null || question.IsDeleted)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				if (question.SchoolId != schoolId)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				if (!isAdmin && question.CreatedBy != userId)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage ="You do not have permission to " + "remove board content from this question",
						Status = "failed"
					};
				}

				if (question.Status == QuestionStatus.Published)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage ="Cannot remove board content from a " +"published question. " + "Unpublish it first",
						Status = "failed"
					};
				}

				// Idempotent — no board session means
				// nothing to detach — return success
				if (!question.HasBoardSession|| string.IsNullOrWhiteSpace(question.BoardSessionId?.ToString()))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage ="No board session attached to this question",
						Status = "successful"
					};
				}

				var oldSnapshotPublicId = question.SnapshotPublicId;

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				// Revert question type
				// If was Mixed → revert to text based
				// If was BoardBased → revert to ShortAnswer
				// as a safe default — teacher can change
				var revertedType = question.QuestionType == QuestionType.Mixed ? QuestionType.ShortAnswer : QuestionType.ShortAnswer;

				var updateDict = new Dictionary<string, object>
					{
						{ "BoardSessionId",   string.Empty },
						{ "SnapshotUrl",      string.Empty },
						{ "SnapshotPublicId", string.Empty },
						{ "HasBoardSession",  false },
						{ "QuestionType",     (int)revertedType },
						{ "ModifiedDate",     now }
					};

				await _questionCommandRepo.UpdateTableColumnById(
					updateDict,
					new KeyValuePair<string, object>(
						"Id", model.QuestionId),
					DatabaseTarget.QuestionBank);

				_logger.Information("Board session detached - " +"QuestionId: {QuestionId}",model.QuestionId);


				if (!string.IsNullOrWhiteSpace(oldSnapshotPublicId))
				{
					_ = Task.Run(async () =>
					{
						try
						{
							await DeleteCdnSnapshot(oldSnapshotPublicId);

							_logger.Information("Snapshot deleted from CDN - " + "PublicId: {PublicId}",oldSnapshotPublicId);
						}
						catch (Exception ex)
						{
							_logger.Error(ex,"Snapshot deletion failed - " +"PublicId: {PublicId}",oldSnapshotPublicId);
						}
					});
				}

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage ="Board session detached successfully",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,"Error detaching board session - " +"QuestionId: {QuestionId}", model.QuestionId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage ="An error occurred while detaching " + "board session",
					Status = "failed"
				};
			}
		}
	}


	public async Task<BaseResponse> GetQuestionsByJobId(Guid jobId,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("JobId", jobId))
		{
			try
			{
				if (!Guid.TryParse(userClaims.UserId, out var userId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};

				// Verify job exists and belongs to this school/teacher
				var job = await _jobQueryRepo.Get(jobId, DatabaseTarget.QuestionBank);

				if (job == null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Job not found",
						Status = "failed"
					};

				if (job.SchoolId != schoolId)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Job not found",
						Status = "failed"
					};

				if (job.Status != "Completed")
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"Job is not completed yet. Current status: {job.Status}",
						Status = "failed",
						Data = new { job.Status, job.AttemptCount }
					};

				// Fetch all questions for this job
				var questionQuery = $@"
					SELECT
						q.Id,
						q.QuestionType,
						q.QuestionHtml,
						q.ContentParts,
						q.HasLatex,
						q.HasMedia,
						q.CorrectAnswer,
						q.DifficultyLevel,
						q.MarksAllocation,
						q.Status,
						q.CreationDate,
						st.Name  AS SubTopicName,
						t.Name   AS TopicName
					FROM   Questions q
					LEFT JOIN SubTopic st ON st.Id = q.SubTopicId
					LEFT JOIN Topic    t  ON t.Id  = q.TopicId
					WHERE  q.JobId     = '{jobId}'
					AND    q.SchoolId  = '{schoolId}'
					AND    q.IsDeleted = 0
					AND    q.IsActive  = 1
					ORDER  BY q.CreationDate ASC";

				var questionRows = await _questionQueryRepo.QueryAsync<JobQuestionRow>(
					questionQuery, new Dictionary<string, object>(),
					DatabaseTarget.QuestionBank);

				var questions = questionRows?.ToList() ?? new();

				// Fetch options for each objective question
				var result = new List<JobQuestionDto>();

				foreach (var q in questions)
				{
					var options = new List<JobQuestionOptionDto>();

					if (q.QuestionType == (int)QuestionType.MultipleChoice)
					{
						var optionQuery = $@"
							SELECT
								Id,
								OptionLabel,
								OptionText,
								OptionHtml,
								ContentParts,
								IsCorrect,
								HasLatex,
								HasImages,
								OrderIndex
							FROM   QuestionOptions
							WHERE  QuestionId = '{q.Id}'
							AND    IsDeleted  = 0
							AND    IsActive   = 1
							ORDER  BY OrderIndex ASC";

						var optionRows = await _optionQueryRepo.QueryAsync<JobQuestionOptionDto>(optionQuery, new Dictionary<string, object>(),DatabaseTarget.QuestionBank);

						options = optionRows?.ToList() ?? new();
					}

					result.Add(new JobQuestionDto
					{
						Id = q.Id,
						QuestionType = q.QuestionType,
						QuestionTypeName = ((QuestionType)q.QuestionType).ToString(),
						QuestionHtml = q.QuestionHtml,
						ContentParts = q.ContentParts,
						HasLatex = q.HasLatex,
						HasMedia = q.HasMedia,
						CorrectAnswer = q.CorrectAnswer,
						DifficultyLevel = q.DifficultyLevel,
						MarksAllocation = q.MarksAllocation,
						Status = q.Status,
						StatusName = ((QuestionStatus)q.Status).ToString(),
						SubTopicName = q.SubTopicName,
						TopicName = q.TopicName,
						CreationDate = q.CreationDate,
						Options = options
					});
				}

				_logger.Information(
					"Questions retrieved by JobId - JobId: {JobId}, Count: {Count}",
					jobId, result.Count);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = $"{result.Count} question(s) retrieved",
					Status = "successful",
					Data = new
					{
						JobId = jobId,
						QuestionType = job.QuestionType,
						TotalQuestions = result.Count,
						Questions = result
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Error retrieving questions by JobId - JobId: {JobId}", jobId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving questions",
					Status = "failed"
				};
			}
		}
	}

	/// <summary>
	/// Get board session reference for a question
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///
	/// 2. RETRIEVE QUESTION
	///
	/// 3. CROSS-TENANT CHECK
	///
	/// 4. RETURN BOARD SESSION REFERENCE
	///    - Returns BoardSessionId only
	///    - Frontend uses ID to fetch
	///      strokes from MongoDB directly
	///    - Backend never fetches strokes
	/// </summary>
	public async Task<BoardSessionResponse> GetBoardSession(Guid questionId,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("QuestionId", questionId))
		{
			try
			{
				_logger.Information("Getting board session - QuestionId: {QuestionId}", questionId);

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new BoardSessionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BoardSessionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				var question = await _questionQueryRepo.Get(questionId, DatabaseTarget.QuestionBank);

				if (question == null || question.IsDeleted)
				{
					return new BoardSessionResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				if (question.SchoolId != schoolId)
				{
					return new BoardSessionResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}
				// No ownership check here
				// Any member of the school can view
				// the board session reference
				// Students need this to display
				// the equation or diagram
				// Strokes are fetched by frontend
				// from MongoDB using this ID
				// Backend never fetches or
				// processes strokes directly

				_logger.Information("Board session reference retrieved - " + "QuestionId: {QuestionId}, " + "HasSession: {HasSession}",questionId, question.HasBoardSession);

				return new BoardSessionResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage ="Board session retrieved successfully",
					Status = "successful",
					QuestionId = questionId,
					BoardSessionId = question.BoardSessionId?.ToString(),
					SnapshotUrl = question.SnapshotUrl,
					HasBoardSession = question.HasBoardSession
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,"Error getting board session - QuestionId: {QuestionId}",questionId);

				return new BoardSessionResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage ="An error occurred while retrieving board session",
					Status = "failed"
				};
			}
		}
	}

	#region Private Helpers

	/// <summary>
	/// Deletes snapshot from CDN
	/// Called fire-and-forget when
	/// board session is replaced or detached
	/// Uses existing Cloudinary pattern
	/// </summary>
	private async Task DeleteCdnSnapshot(string publicId)
	{
		// Follows existing CloudinaryService pattern
		// in the platform
		// Deletes asset by public ID
		var success = await _cloudinaryService.DeleteMediaAsync(publicId, MediaType.Image);
		if (!success)
		{
			_logger.Warning("CDN snapshot deletion returned false - " + "PublicId: {PublicId}. " + "Cleanup job will handle orphaned asset", publicId);
		}
	}

	#endregion
}

