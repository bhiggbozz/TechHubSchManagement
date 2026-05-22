using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.DTO;
using TechHub.QuestionBank.Core.Entities;
using TechHub.QuestionBank.Core.Enums;
using TechHub.QuestionBank.Core.Helpers;
using TechHub.QuestionBank.Core.Model;
using TechHub.QuestionBank.Core.Response;
using TechHub.QuestionBank.Core.ViewModel;
using TechHub.QuestionBank.Services.interfaces;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;

namespace TechHub.QuestionBank.Services;

public class QuestionService : IQuestionService
{
	private readonly IQueryRepository<Questions> _questionQueryRepo;
	private readonly ICommandRespository<Questions> _questionCommandRepo;
	private readonly IQueryRepository<QuestionOptions> _optionQueryRepo;
	private readonly ICommandRespository<QuestionOptions> _optionCommandRepo;
	private readonly IQueryRepository<ScanSession> _scanSessionQueryRepo;
	private readonly ICommandRespository<ScanSession> _scanSessionCommandRepo;
	private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;

	private readonly ILogger _logger;

	public QuestionService(
		IQueryRepository<Questions> questionQueryRepo,
		ICommandRespository<Questions> questionCommandRepo,
		IQueryRepository<QuestionOptions> optionQueryRepo,
		ICommandRespository<QuestionOptions> optionCommandRepo,
		IQueryRepository<ScanSession> scanSessionQueryRepo,
		ICommandRespository<ScanSession> scanSessionCommandRepo,
		IDbTransactionScopeFactory dbTransactionScopeFactory,
		ILogger logger)
	{
		_questionQueryRepo = questionQueryRepo;
		_questionCommandRepo = questionCommandRepo;
		_optionQueryRepo = optionQueryRepo;
		_optionCommandRepo = optionCommandRepo;
		_scanSessionCommandRepo = scanSessionCommandRepo;
		_scanSessionQueryRepo = scanSessionQueryRepo;
		_dbTransactionScopeFactory = dbTransactionScopeFactory;
		_logger = logger;
	}

	/// <summary>
	/// Create a new question
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///    - Parse and validate UserId and SchoolId
	///
	/// 2. VALIDATE INPUT
	///    - Title is required
	///    - SubjectId is required
	///    - MarksAllocation must be positive
	///    - MCQ must have at least 2 options
	///    - MCQ must have exactly one correct answer
	///
	/// 3. DUPLICATE CLIENTID CHECK
	///    - If ClientId provided, check if already synced
	///    - Prevents duplicate questions on retry sync
	///    - If duplicate found, return existing server Id
	///      so frontend can reconcile without creating again
	///
	/// 4. BUILD QUESTION ENTITY
	///    - Map ViewModel to Question model
	///    - Set school context from JWT claims
	///    - Set audit fields
	///    - Set initial SyncStatus as Synced
	///      (it is on server now)
	///
	/// 5. SAVE QUESTION
	///    - Persist to database
	///
	/// 6. SAVE OPTIONS (MCQ only)
	///    - Save each option linked to question
	///
	/// 7. RETURN RESPONSE
	///    - Return ServerId AND ClientId
	///    - Frontend uses this map to clean local storage
	///
	/// OFFLINE CONSIDERATION:
	/// - ClientId is device-generated before server assigns Id
	/// - Same question may arrive multiple times on retry
	/// - Duplicate ClientId detection prevents double save
	/// - Always return ServerId so frontend can reconcile
	/// </summary>

	public async Task<CreateQuestionResponse> CreateQuestion(CreateQuestionViewModel model, AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims?.UserId))
		using (LogContext.PushProperty("ClientId", model?.ClientId))
		{
			try
			{
				_logger.Information(
					"Creating question - ClientId: {ClientId}, UserId: {UserId}",
					model?.ClientId, userClaims?.UserId);

				CreateQuestionResponse Fail(string message) =>
					new CreateQuestionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = message,
						Status = "failed"
					};

				if (model == null)
					return Fail("Invalid request payload");

				if (!Guid.TryParse(userClaims?.UserId, out var userId))
					return Fail("Invalid user identification");

				if (!Guid.TryParse(userClaims?.SchoolId, out var schoolId))
					return Fail("Invalid school identification");

				if (model.SubjectId == Guid.Empty)
					return Fail("Subject is required");

				if (model.ClassroomId == Guid.Empty)
					return Fail("Classroom is required");

				if (model.MarksAllocation <= 0)
					return Fail("Marks allocation must be greater than zero");

				if (model.QuestionType == QuestionType.MultipleChoice)
				{
					if (model.Options == null || model.Options.Count < 2)
						return Fail("Multiple choice questions must have at least 2 options");

					if (model.Options.Count > 6)
						return Fail("Multiple choice questions cannot have more than 6 options");

					if (model.Options.Count(o => o.IsCorrect) != 1)
						return Fail("Multiple choice questions must have exactly one correct answer");

					var emptyOption = model.Options
						.FirstOrDefault(o => string.IsNullOrWhiteSpace(o.OptionText));
					if (emptyOption != null)
						return Fail($"Option {emptyOption.OptionLabel} cannot be empty");
				}

				// Idempotency check — before opening transaction
				if (!string.IsNullOrWhiteSpace(model.ClientId))
				{
					const string existingSql = @"
						SELECT TOP 1 *
						FROM   Questions
						WHERE  ClientId  = @ClientId
						AND    SchoolId  = @SchoolId
						AND    IsDeleted = 0";

					var parameters = new Dictionary<string, object>
					{
						{ "ClientId", model.ClientId },
						{ "SchoolId", schoolId }
					};

					var existing = await _questionQueryRepo
						.QueryAsync<Questions>(existingSql, parameters);

					var existingQuestion = existing?.FirstOrDefault();

					if (existingQuestion != null)
					{
						_logger.Information(
							"Duplicate ClientId - ClientId: {ClientId}, ExistingId: {Id}",
							model.ClientId, existingQuestion.Id);

						return new CreateQuestionResponse
						{
							ResponseCode = ResponseCode.successful,
							ResponseMessage = "Question already exists on server",
							Status = "successful",
							QuestionId = existingQuestion.Id,
							ClientId = model.ClientId,
							IsDuplicate = true
						};
					}
				}

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
				var questionId = Guid.NewGuid();

				var question = new Questions
				{
					Id = questionId,
					SchoolId = schoolId,
					SubjectId = model.SubjectId,
					TopicId = model.TopicId,
					ClassroomId = model.ClassroomId,
					CreatedBy = userId,
					Title = model.Title?.Trim(),
					Topic = model.Topic?.Trim(),
					SubTopicId = model.SubTopic,
					QuestionType = model.QuestionType,
					TextContent = model.TextContent?.Trim(),
					DifficultyLevel = model.DifficultyLevel,
					MarksAllocation = model.MarksAllocation,
					BoardSessionId = model.BoardSessionId,
					HasBoardSession = model.BoardSessionId.HasValue,
					HasMedia = !string.IsNullOrWhiteSpace(model.ImageUrl),
					ImageUrl = model.ImageUrl?.Trim(),
					ImagePublicId = model.ImagePublicId?.Trim(),
					HasAudio = false,
					IsScanned = model.IsScanned,
					Status = model.ScanSessionId != null
										? QuestionStatus.PendingReview
										: QuestionStatus.Published,
					IsActive = true,
					IsDeleted = false,
					ClientId = model.ClientId,
					OriginDevice = model.OriginDevice,
					LastSyncedAt = DateTime.UtcNow,
					CreationDate = now,
					ModifiedDate = now
				};

				using var scope = _dbTransactionScopeFactory.Create("QuestionBankConnection");
				try
				{
					await _questionCommandRepo.Create(scope.Transaction, scope.Connection, question);

					if (model.QuestionType == QuestionType.MultipleChoice && model.Options?.Any() == true)
					{
						foreach (var optionModel in model.Options)
						{
							var option = new QuestionOptions
							{
								Id = Guid.NewGuid(),
								QuestionId = questionId,
								OptionLabel = optionModel.OptionLabel,
								OptionText = optionModel.OptionText.Trim(),
								IsCorrect = optionModel.IsCorrect,
								OrderIndex = optionModel.OrderIndex,
								CreationDate = now
							};

							await _optionCommandRepo.Create(
								scope.Transaction, scope.Connection, option);
						}
					}

					await scope.CommitAsync();  // ← scope not transaction
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Rolling back question creation - ClientId: {ClientId}",
						model.ClientId);

					try { await scope.RollbackAsync(); }
					catch (Exception rbEx)
					{
						_logger.Error(rbEx,
							"Rollback failed - ClientId: {ClientId}", model.ClientId);
					}

					throw;
				}

				_logger.Information(
					"Question created - QuestionId: {QuestionId}, ClientId: {ClientId}",
					questionId, model.ClientId);

				return new CreateQuestionResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Question created successfully",
					Status = "successful",
					QuestionId = questionId,
					ClientId = model.ClientId,
					IsDuplicate = false
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Error creating question - ClientId: {ClientId}", model?.ClientId);

				return new CreateQuestionResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while creating the question",
					Status = "failed"
				};
			}
		}
	}


	/// <summary>
	/// Update an existing question
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///    - Parse and validate UserId and SchoolId
	///
	/// 2. VALIDATE INPUT
	///    - Title is required
	///    - MarksAllocation must be positive
	///    - MCQ specific option validation
	///
	/// 3. RETRIEVE QUESTION
	///    - Fetch from QuestionBank database
	///    - Verify question exists
	///
	/// 4. OWNERSHIP CHECK
	///    - Only creator can update their question
	///    - Admins bypass this check
	///
	/// 5. STATUS CHECK
	///    - Only Draft questions can be updated
	///    - Published questions must be unpublished first
	///
	/// 6. DIRTY STATE DETECTION
	///    - Compare LastKnownModifiedDate from client
	///      against server ModifiedDate
	///    - If they differ, server was updated by
	///      another device since last sync
	///    - Return conflict instead of overwriting
	///
	/// 7. UPDATE QUESTION
	///    - Apply changes to question fields
	///    - Update ModifiedDate
	///
	/// 8. UPDATE OPTIONS (MCQ only)
	///    - Delete existing options
	///    - Insert fresh options
	///    - Simplest approach — avoids partial update bugs
	///
	/// 9. RETURN RESPONSE
	///    - Confirm update with new ModifiedDate
	///    - Frontend uses ModifiedDate for future
	///      dirty detection on next sync
	///
	/// OFFLINE CONSIDERATION:
	/// - LastKnownModifiedDate is what frontend
	///   had when teacher started editing
	/// - If server ModifiedDate differs, conflict
	///   detected before any overwrite happens
	/// - Teacher resolves conflict explicitly
	/// </summary>
	public async Task<UpdateQuestionResponse> UpdateQuestion(UpdateQuestionViewModel model, AuthenticatedUserClaims userClaims)
	{
		#region
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("QuestionId", model.QuestionId))
		{
			try
			{
				_logger.Information("Updating question - QuestionId: {QuestionId}, " + "UserId: {UserId}", model.QuestionId, userClaims.UserId);


				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					_logger.Warning("Invalid UserId - UserId: {UserId}", userClaims.UserId);

					return new UpdateQuestionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					_logger.Warning("Invalid SchoolId - SchoolId: {SchoolId}", userClaims.SchoolId);

					return new UpdateQuestionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				//var userRole = int.Parse(userClaims?.Role ?? "0");
				var userRole = Enum.TryParse<UserRole>(userClaims?.Role?.Replace(" ", ""), ignoreCase: true, out var parsedRole) ? parsedRole : UserRole.SubjectTeacher;



				if (model.QuestionId == Guid.Empty)
				{
					return new UpdateQuestionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Question ID is required",
						Status = "failed"
					};
				}

				if (string.IsNullOrWhiteSpace(model.Title))
				{
					return new UpdateQuestionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Question title is required",
						Status = "failed"
					};
				}

				if (model.Title.Length > 500)
				{
					return new UpdateQuestionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage =
							"Question title cannot exceed 500 characters",
						Status = "failed"
					};
				}

				if (model.MarksAllocation <= 0)
				{
					return new UpdateQuestionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Marks allocation must be greater than zero",
						Status = "failed"
					};
				}

				// MCQ specific validation
				if (model.QuestionType == QuestionType.MultipleChoice)
				{
					if (model.Options == null || model.Options.Count < 2)
					{
						return new UpdateQuestionResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage =
								"Multiple choice questions must have " +
								"at least 2 options",
							Status = "failed"
						};
					}

					if (model.Options.Count > 6)
					{
						return new UpdateQuestionResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage =
								"Multiple choice questions cannot " +
								"have more than 6 options",
							Status = "failed"
						};
					}

					var correctAnswers = model.Options.Count(o => o.IsCorrect);

					if (correctAnswers != 1)
					{
						return new UpdateQuestionResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Multiple choice questions must have " + "exactly one correct answer",
							Status = "failed"
						};
					}

					var emptyOption = model.Options.FirstOrDefault(o => string.IsNullOrWhiteSpace(o.OptionText));

					if (emptyOption != null)
					{
						return new UpdateQuestionResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Option {emptyOption.OptionLabel} " + $"cannot be empty",
							Status = "failed"
						};
					}
				}



				var question = await _questionQueryRepo.Get(model.QuestionId, DatabaseTarget.QuestionBank);

				if (question == null || question.IsDeleted)
				{
					_logger.Warning("Question not found - QuestionId: {QuestionId}", model.QuestionId);

					return new UpdateQuestionResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				// Verify question belongs to this school
				// Prevents cross-tenant data access
				if (question.SchoolId != schoolId)
				{
					_logger.Warning("Cross-tenant access attempt - " + "QuestionId: {QuestionId}, " + "SchoolId: {SchoolId}", model.QuestionId, schoolId);

					return new UpdateQuestionResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "Question not found",
						// Intentionally vague — 
						// do not reveal cross-tenant existence
						Status = "failed"
					};
				}


				var isAdmin = userRole == UserRole.Administrator || userRole == UserRole.SuperAdministrator;

				if (!isAdmin && question.CreatedBy != userId)
				{
					_logger.Warning("Ownership check failed - " + "QuestionId: {QuestionId}, " + "RequestedBy: {UserId}", model.QuestionId, userId);

					return new UpdateQuestionResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You do not have permission to edit this question",
						Status = "failed"
					};
				}


				// Only Draft questions can be freely edited
				// Published questions are potentially in active assessments
				// Teacher must unpublish first — explicit intentional action
				if (question.Status != QuestionStatus.Draft)
				{
					_logger.Warning(
						"Cannot update non-draft question - " + "QuestionId: {QuestionId}, " + "Status: {Status}", model.QuestionId, question.Status);

					return new UpdateQuestionResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage =
							$"Cannot edit a {question.Status} question. " +
							$"Unpublish it first to make changes",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 6: DIRTY STATE DETECTION
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				// Frontend sends the ModifiedDate it had
				// when teacher started editing offline
				// If server ModifiedDate is different,
				// another device updated this question
				// since the teacher started their edit
				// We must not silently overwrite that change

				if (!string.IsNullOrWhiteSpace(model.LastKnownModifiedDate))
				{
					var serverModifiedDate = question.ModifiedDate?.Trim();
					var clientModifiedDate = model.LastKnownModifiedDate.Trim();

					if (serverModifiedDate != clientModifiedDate)
					{
						_logger.Warning(
							"Dirty state conflict detected - " + "QuestionId: {QuestionId}, " + "ClientDate: {ClientDate}, " + "ServerDate: {ServerDate}",
							model.QuestionId,
							clientModifiedDate,
							serverModifiedDate);

						// Return conflict details so frontend
						// can present both versions to teacher
						// Teacher resolves explicitly via
						// ResolveConflict endpoint
						return new UpdateQuestionResponse
						{
							ResponseCode = ResponseCode.Conflict,
							ResponseMessage = "This question was updated on another " + "device since your last sync. " + "Please resolve the conflict before saving",
							Status = "conflict",
							IsConflict = true,
							ConflictDetail = new ConflictDetail
							{
								ClientId = model.ClientId,
								ServerId = question.Id,
								Reason = ConflictReason.BothVersionsEdited,
								ReasonDescription =
									"This question was edited on another " +
									"device after your last sync",
								ServerModifiedAt = serverModifiedDate,
								LocalModifiedAt = clientModifiedDate,
								//ServerVersion = MapToDto(question)
							}
						};
					}
				}


				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				var updateDict = new Dictionary<string, object>
				{
					{ "Title",          model.Title.Trim() },
					{ "SubjectId",      model.SubjectId },
					{ "TopicId",        model.TopicId as object ?? DBNull.Value },
					{ "Topic",          model.Topic?.Trim() ?? string.Empty },
					{ "SubTopic",       model.SubTopic?.Trim() ?? string.Empty },
					{ "TextContent",    model.TextContent?.Trim() ?? string.Empty },
					{ "QuestionType",   (int)model.QuestionType },
					{ "DifficultyLevel",(int)model.DifficultyLevel },
					{ "MarksAllocation",model.MarksAllocation },
					{ "BoardSessionId", model.BoardSessionId as object ?? DBNull.Value },
					{ "HasBoardSession",model.BoardSessionId.HasValue },
					{ "ModifiedDate",   now }
				};

				await _questionCommandRepo.UpdateTableColumnById(updateDict, new KeyValuePair<string, object>("Id", model.QuestionId), DatabaseTarget.QuestionBank);

				_logger.Information("Question updated - QuestionId: {QuestionId}", model.QuestionId);


				// Simplest reliable approach:
				// Delete all existing options for this question
				// Insert fresh set from the update model
				// Avoids partial update bugs and ordering issues

				if (model.QuestionType == QuestionType.MultipleChoice && model.Options != null && model.Options.Any())
				{
					// Delete existing options
					//var deleteQuery = $@"
					//               DELETE FROM QuestionOptions
					//               WHERE QuestionId = '{model.QuestionId}'";

					var softDeleteDict = new Dictionary<string, object>
					{
						{ "IsDeleted",  true },
						{ "IsActive",   false },
						{ "DeletedDate", now }
					};
					await _questionCommandRepo.UpdateTableColumnById(softDeleteDict, new KeyValuePair<string, object>("QuestionId", model.QuestionId), DatabaseTarget.QuestionBank);
					//await _questionCommandRepo.ExecuteRaw(deleteQuery,DatabaseTarget.QuestionBank);

					// Insert fresh options
					foreach (var optionModel in model.Options)
					{
						var option = new QuestionOptions
						{
							Id = Guid.NewGuid(),
							QuestionId = model.QuestionId,
							OptionLabel = optionModel.OptionLabel,
							OptionText = optionModel.OptionText.Trim(),
							IsCorrect = optionModel.IsCorrect,
							OrderIndex = optionModel.OrderIndex,
							CreationDate = now
						};

						await _optionCommandRepo.Create(option, DatabaseTarget.QuestionBank);
					}

					_logger.Information("Options updated - QuestionId: {QuestionId}, " + "OptionCount: {Count}", model.QuestionId, model.Options.Count);
				}


				return new UpdateQuestionResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Question updated successfully",
					Status = "successful",
					QuestionId = model.QuestionId,
					ClientId = model.ClientId,
					NewModifiedDate = now
					// Frontend updates its local record
					// with this ModifiedDate
					// Keeps dirty detection accurate
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error updating question - QuestionId: {QuestionId}", model.QuestionId);

				return new UpdateQuestionResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage =
						"An error occurred while updating the question",
					Status = "failed"
				};
			}
		}
	}
	#endregion

	/// <summary>
	/// Get question by Id
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///    - Parse and validate UserId and SchoolId
	///
	/// 2. RETRIEVE QUESTION
	///    - Fetch from QuestionBank database
	///    - Verify question exists and is not deleted
	///
	/// 3. CROSS-TENANT CHECK
	///    - Verify question belongs to requesting school
	///
	/// 4. FETCH OPTIONS (MCQ only)
	///    - Get active options for this question
	///    - Ordered by OrderIndex
	///
	/// 5. MAP TO DTO
	///    - Map question and options to QuestionDto
	///    - Set permission flags based on role and ownership
	///    - IsCorrect hidden if requester is a student
	///
	/// 6. RETURN RESPONSE
	/// </summary>
	public async Task<QuestionDetailResponse> GetQuestion(Guid questionId, AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("QuestionId", questionId))
		{
			try
			{
				_logger.Information(
					"Getting question - QuestionId: {QuestionId}, " +
					"UserId: {UserId}",
					questionId,
					userClaims.UserId);

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					_logger.Warning(
						"Invalid UserId - UserId: {UserId}",
						userClaims.UserId);

					return new QuestionDetailResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					_logger.Warning(
						"Invalid SchoolId - SchoolId: {SchoolId}",
						userClaims.SchoolId);

					return new QuestionDetailResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				var userRole = ClaimsHelper.ParseRole(userClaims.Role);
				var isAdmin = ClaimsHelper.IsAdmin(userClaims.Role);

				// JOIN with Users table to get creator name
				// and Subject table to get subject name
				// in one query rather than multiple round trips
				var query = $@"
					SELECT
						q.*,
						u.UserName      AS CreatedByName,
						s.SubjectName   AS SubjectName
					FROM Questions q
					LEFT JOIN Users u
						ON q.CreatedBy = u.Id
					LEFT JOIN Subjects s
						ON q.SubjectId = s.Id
					WHERE q.Id          = '{questionId}'
					AND   q.SchoolId    = '{schoolId}'
					AND   q.IsDeleted   = 0
					AND   q.IsActive    = 1";

				var results = await _questionQueryRepo.GetByQuery(query, DatabaseTarget.QuestionBank);

				var question = results?.FirstOrDefault();

				if (question == null)
				{
					_logger.Warning("Question not found - QuestionId: {QuestionId}", questionId);

					return new QuestionDetailResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				// Already filtered by SchoolId in query above
				// This is a defence-in-depth check
				if (question.SchoolId != schoolId)
				{
					_logger.Warning(
						"Cross-tenant access attempt - " +"QuestionId: {QuestionId}, " + "SchoolId: {SchoolId}",
						questionId, schoolId);

					return new QuestionDetailResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						// Intentionally vague
						Status = "failed"
					};
				}


				var options = new List<QuestionOptions>();

				if (question.QuestionType == QuestionType.MultipleChoice)
				{
					var optionsQuery = $@"
						SELECT *
						FROM QuestionOptions
						WHERE QuestionId    = '{questionId}'
						AND   IsDeleted     = 0
						AND   IsActive      = 1
						ORDER BY OrderIndex ASC";

					var optionResults = await _optionQueryRepo.GetByQuery(
							optionsQuery, DatabaseTarget.QuestionBank);

					options = optionResults?.ToList() ?? new List<QuestionOptions>();
				}

				var isOwner = question.CreatedBy == userId;
				var isStudent = userRole == UserRole.Student;

				var dto = MapToDto(question,options,isOwner,isAdmin,isStudent);

				_logger.Information("Question retrieved - QuestionId: {QuestionId}", questionId);


				return new QuestionDetailResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Question retrieved successfully",
					Status = "successful",
					Question = dto
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error getting question - QuestionId: {QuestionId}", questionId);

				return new QuestionDetailResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage ="An error occurred while retrieving the question",
					Status = "failed"
				};
			}
		}
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// GET SUBJECT QUESTIONS
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	/// <summary>
	/// Get all questions for a subject
	/// Returns lightweight summary DTOs for memory efficiency
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///    - Parse and validate UserId and SchoolId
	///
	/// 2. VALIDATE FILTER
	///    - Sanitise pagination values
	///    - Prevent excessive page sizes
	///
	/// 3. BUILD DYNAMIC QUERY
	///    - Base filter: SchoolId + SubjectId + not deleted
	///    - Optional filters: QuestionType, DifficultyLevel,
	///      Status, SearchText
	///    - Pagination applied at DB level
	///      not in memory — critical for performance
	///
	/// 4. FETCH TOTAL COUNT
	///    - Separate count query for pagination metadata
	///    - Frontend knows total pages without
	///      fetching all records
	///
	/// 5. FETCH PAGED RESULTS
	///    - Only fetch what is needed for current page
	///    - Returns QuestionSummaryDto not full QuestionDto
	///    - Full detail fetched only when question opened
	///
	/// 6. RETURN RESPONSE
	///
	/// MEMORY CONSIDERATION:
	/// - PageSize capped at 50
	/// - Summary DTO used not full DTO
	/// - DB level pagination not in-memory
	/// - These three together keep memory controlled
	///   regardless of question bank size
	/// </summary>
	public async Task<QuestionListResponse> GetSubjectQuestions(Guid subjectId,QuestionFilterViewModel filter,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("SubjectId", subjectId))
		{
			try
			{
				_logger.Information("Getting subject questions - " + "SubjectId: {SubjectId}, " + "UserId: {UserId}", subjectId, userClaims.UserId);


				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new QuestionListResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new QuestionListResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				// Enforce sensible pagination bounds
				// Prevents memory issues from huge page requests
				filter.Page = filter.Page < 1 ? 1 : filter.Page;
				filter.PageSize = filter.PageSize < 1 ? 20 : filter.PageSize;
				filter.PageSize = filter.PageSize > 50 ? 50 : filter.PageSize;
				// Hard cap at 50 regardless of what frontend sends

				var offset = (filter.Page - 1) * filter.PageSize;

				// Start with base WHERE conditions
				// that always apply
				var whereClause = $@"
						WHERE q.SchoolId    = '{schoolId}'
						AND   q.SubjectId   = '{subjectId}'
						AND   q.IsDeleted   = 0
						AND   q.IsActive    = 1";

				if (!filter.IncludePendingReview)
				{
					whereClause += $" AND q.Status != {(int)QuestionStatus.PendingReview}";
				}

				// Append optional filters dynamically
				if (filter.QuestionType.HasValue)
				{
					whereClause += $" AND q.QuestionType = {(int)filter.QuestionType.Value}";
				}

				if (filter.DifficultyLevel.HasValue)
				{
					whereClause += $" AND q.DifficultyLevel = {(int)filter.DifficultyLevel.Value}";
				}
				if (filter.ScanSessionId.HasValue)
				{
					whereClause += $" AND q.ScanSessionId = '{filter.ScanSessionId.Value}'";
				}

				if (filter.Status.HasValue)
				{
					whereClause += $" AND q.Status = {(int)filter.Status.Value}";
				}

				if (!string.IsNullOrWhiteSpace(filter.SearchText))
				{
					// Sanitise search text to prevent SQL injection
					var safeSearch = filter.SearchText.Replace("'", "''").Trim();

					whereClause +=
						$@" AND (
                        q.Title         LIKE '%{safeSearch}%'
                        OR q.Topic      LIKE '%{safeSearch}%'
                        OR q.TextContent LIKE '%{safeSearch}%'
                    )";
				}

				var countQuery = $@"SELECT COUNT(*) FROM Questions q {whereClause}";

				var totalCount = await _questionQueryRepo.CountAsync(countQuery,DatabaseTarget.QuestionBank);

				if (totalCount == 0)
				{
					return new QuestionListResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "No questions found",
						Status = "successful",
						Questions = new List<QuestionSummaryDto>(),
						TotalCount = 0,
						Page = filter.Page,
						PageSize = filter.PageSize,
						HasMore = false
					};
				}
				// Select only fields needed for summary
				// Not SELECT * — avoids pulling heavy fields
				// like TextContent, AIData into memory
				// for list view that does not need them
				var dataQuery = $@"
					SELECT
						q.Id,
						q.ClientId,
						q.Title,
						q.Topic,
						q.QuestionType,
						q.DifficultyLevel,
						q.MarksAllocation,
						q.HasBoardSession,
						q.HasMedia,
						q.HasAudio,
						q.IsScanned,
						q.Status,
						q.CreationDate,
						s.SubjectName   AS SubjectName
					FROM Questions q
					LEFT JOIN Subjects s
						ON q.SubjectId = s.Id
					{whereClause}
					ORDER BY q.CreationDate DESC
					OFFSET {offset} ROWS
					FETCH NEXT {filter.PageSize} ROWS ONLY";

				var results = await _questionQueryRepo.GetByQuery(dataQuery,DatabaseTarget.QuestionBank);

				var questions = results?.Select(q => MapToSummaryDto(q)).ToList() ?? new List<QuestionSummaryDto>();

				var hasMore = (filter.Page * filter.PageSize) < totalCount;

				_logger.Information("Questions retrieved - " +"SubjectId: {SubjectId}, " + "Count: {Count}, " + "TotalCount: {Total}",subjectId,
					questions.Count, totalCount);


				return new QuestionListResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Questions retrieved successfully",
					Status = "successful",
					Questions = questions,
					TotalCount = totalCount,
					Page = filter.Page,
					PageSize = filter.PageSize,
					HasMore = hasMore
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,"Error getting subject questions - " + "SubjectId: {SubjectId}", subjectId);

				return new QuestionListResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage ="An error occurred while retrieving questions",
					Status = "failed",
					Questions = new List<QuestionSummaryDto>()
				};
			}
		}
	}



	/// <summary>
	/// Soft delete a question
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///    - Parse and validate UserId and SchoolId
	///
	/// 2. RETRIEVE QUESTION
	///    - Fetch from QuestionBank database
	///    - Verify question exists and is not deleted
	///
	/// 3. CROSS-TENANT CHECK
	///    - Verify question belongs to requesting school
	///
	/// 4. OWNERSHIP CHECK
	///    - Only creator or admin can delete
	///
	/// 5. STATUS CHECK
	///    - Published questions cannot be deleted directly
	///    - Must be unpublished first
	///    - Prevents deleting questions active in assessments
	///
	/// 6. SOFT DELETE QUESTION
	///    - Set IsDeleted = true, IsActive = false
	///    - Record DeletedDate and DeletedBy
	///
	/// 7. SOFT DELETE OPTIONS
	///    - Soft delete all active options for this question
	///    - Consistent with question soft delete
	///
	/// 8. RETURN RESPONSE
	/// </summary>
	public async Task<BaseResponse> DeleteQuestion(Guid questionId,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("QuestionId", questionId))
		{
			try
			{
				_logger.Information("Deleting question - QuestionId: {QuestionId}, " +"UserId: {UserId}",questionId,userClaims.UserId);


				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					_logger.Warning("Invalid UserId - UserId: {UserId}",userClaims.UserId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}


				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					_logger.Warning("Invalid SchoolId - SchoolId: {SchoolId}",userClaims.SchoolId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				var isAdmin = ClaimsHelper.IsAdmin(userClaims.Role);

				var question = await _questionQueryRepo.Get(questionId,DatabaseTarget.QuestionBank);

				if (question == null || question.IsDeleted)
				{
					_logger.Warning("Question not found - QuestionId: {QuestionId}",questionId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				if (question.SchoolId != schoolId)
				{
					_logger.Warning("Cross-tenant access attempt - " + "QuestionId: {QuestionId}, " + "SchoolId: {SchoolId}",questionId,schoolId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						// Intentionally vague
						Status = "failed"
					};
				}
				if (question.Status == QuestionStatus.Published)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage ="Cannot delete a published question. " +"Unpublish it first before deleting",
						Status = "failed"
					};
				}

				if (!isAdmin && question.CreatedBy != userId)
				{
					_logger.Warning( "Ownership check failed - " +"QuestionId: {QuestionId}, " + "RequestedBy: {UserId}",questionId, userId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage =
							"You do not have permission " +
							"to delete this question",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 5: STATUS CHECK
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				// Published questions may be actively used
				// in assessments — cannot delete directly
				// Teacher must unpublish first
				// This is an intentional friction point
				// to prevent accidental deletion of
				// questions mid-assessment
				if (question.Status == QuestionStatus.Published)
				{
					_logger.Warning("Cannot delete published question - " +"QuestionId: {QuestionId}",questionId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage =
							"Cannot delete a published question. " +
							"Unpublish it first before deleting",
						Status = "failed"
					};
				}


				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				var deleteDict = new Dictionary<string, object>
				{
					{ "IsDeleted",   true },
					{ "IsActive",    false },
					{ "DeletedDate", now },
					{ "DeletedBy",   userId },
					{ "ModifiedDate",now }
				};

				await _questionCommandRepo.UpdateTableColumnById(deleteDict, new KeyValuePair<string, object>("Id", questionId),DatabaseTarget.QuestionBank);

				_logger.Information(
					"Question soft deleted - QuestionId: {QuestionId}",
					questionId);

				
				// No harm running on non-MCQ questions
				// UpdateTableColumnByCondition handles
				// the case where no options exist gracefully
				if (question.QuestionType == QuestionType.MultipleChoice)
				{
					var optionDeleteDict = new Dictionary<string, object>
				{
					{ "IsDeleted",   true },
					{ "IsActive",    false },
					{ "DeletedDate", now },
					{ "ModifiedDate",now }
				};

					await _optionCommandRepo.UpdateTableColumnById(optionDeleteDict,new KeyValuePair<string, object>("QuestionId", questionId),DatabaseTarget.QuestionBank);

					_logger.Information(
						"Question options soft deleted - " +
						"QuestionId: {QuestionId}",
						questionId);
				}

				
				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Question deleted successfully",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,"Error deleting question - " +"QuestionId: {QuestionId}",questionId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage ="An error occurred while deleting the question",
					Status = "failed"
				};
			}
		}
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// PUBLISH QUESTION
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	/// <summary>
	/// Publish a draft question
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///    - Parse and validate UserId and SchoolId
	///
	/// 2. RETRIEVE QUESTION
	///    - Fetch from QuestionBank database
	///    - Verify question exists and is not deleted
	///
	/// 3. CROSS-TENANT CHECK
	///    - Verify question belongs to requesting school
	///
	/// 4. OWNERSHIP CHECK
	///    - Only creator can publish their own question
	///    - Admins can publish any question in school
	///
	/// 5. READINESS CHECK
	///    - Question must be in Draft status
	///    - MCQ must have at least 2 options
	///    - MCQ must have exactly one correct answer
	///    - Question must have title and content
	///    - Prevents publishing incomplete questions
	///
	/// 6. PUBLISH QUESTION
	///    - Set Status = Published
	///    - Record PublishedDate
	///
	/// 7. RETURN RESPONSE
	/// </summary>
	public async Task<BaseResponse> PublishQuestion(Guid questionId,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("QuestionId", questionId))
		{
			try
			{
				_logger.Information("Publishing question - QuestionId: {QuestionId}, " + "UserId: {UserId}",questionId,                                    userClaims.UserId);


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

				var question = await _questionQueryRepo.Get(questionId,DatabaseTarget.QuestionBank);

				if (question == null || question.IsDeleted)
				{
					_logger.Warning(
						"Question not found - QuestionId: {QuestionId}",
						questionId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				if (question.SchoolId != schoolId)
				{
					_logger.Warning("Cross-tenant access attempt - " +"QuestionId: {QuestionId}, " + "SchoolId: {SchoolId}", questionId, schoolId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}
				if (question.Status == QuestionStatus.PendingReview)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage ="This question must be verified before " +"it can be published. " +"Please confirm it from your review queue first",
						Status = "failed"
					};
				}

				if (!isAdmin && question.CreatedBy != userId)
				{
					_logger.Warning("Ownership check failed - " +"QuestionId: {QuestionId}, " +"RequestedBy: {UserId}",questionId,userId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage ="You do not have permission " + "to publish this question",
						Status = "failed"
					};
				}

				if (question.Status == QuestionStatus.PendingReview)
				{
					return new BaseResponse
					{
						ResponseMessage ="This question must be verified " +"before it can be published"
					};
				}
				if (question.Status != QuestionStatus.Draft)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage =$"Cannot publish a question " + $"with status: {question.Status}",
						Status = "failed"
					};
				}

				if (string.IsNullOrWhiteSpace(question.Title))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage ="Question must have a title before publishing",
						Status = "failed"
					};
				}

				// For non-image and non-board questions
				var requiresTextContent = question.QuestionType != QuestionType.ImageBased && question.QuestionType != QuestionType.BoardBased;

				if (requiresTextContent && string.IsNullOrWhiteSpace(question.TextContent))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Question must have content before publishing",
						Status = "failed"
					};
				}

				// MCQ readiness check
				// Fetch options to validate before publishing
				if (question.QuestionType == QuestionType.MultipleChoice)
				{
					var optionsQuery = $@"
						SELECT *
						FROM QuestionOptions
						WHERE QuestionId    = '{questionId}'
						AND   IsDeleted     = 0
						AND   IsActive      = 1";

					var optionResults = await _optionQueryRepo.GetByQuery(optionsQuery,DatabaseTarget.QuestionBank);

					var options = optionResults?.ToList() ?? new List<QuestionOptions>();

					if (options.Count < 2)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage ="Multiple choice questions must have " +"at least 2 options before publishing",
							Status = "failed"
						};
					}

					var correctCount = options.Count(o => o.IsCorrect);

					if (correctCount != 1)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage ="Multiple choice questions must have " +"exactly one correct answer before publishing",
							Status = "failed"
						};
					}
				}

				

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				var publishDict = new Dictionary<string, object>
				{
					{ "Status",        (int)QuestionStatus.Published },
					{ "PublishedDate", now },
					{ "PublishedBy",   userId },
					{ "ModifiedDate",  now }
				};

				await _questionCommandRepo.UpdateTableColumnById(publishDict,new KeyValuePair<string, object>("Id", questionId),DatabaseTarget.QuestionBank);

				_logger.Information("Question published - QuestionId: {QuestionId}", questionId);


				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Question published successfully",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,"Error publishing question - " +"QuestionId: {QuestionId}", questionId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage ="An error occurred while publishing the question",
					Status = "failed"
				};
			}
		}
	}


	/// <summary>
	/// Maps full Question entity to QuestionDto
	/// IsCorrect hidden from students
	/// Permission flags set based on role and ownership
	/// </summary>
	private QuestionDto MapToDto(Questions question,List<QuestionOptions> options,bool isOwner,bool isAdmin,bool isStudent)
	{
		#region
		return new QuestionDto
		{
			Id = question.Id,
			ClientId = question.ClientId,

			// Classification
			SubjectId = question.SubjectId,
			//SubjectName = question.SubjectName,
			TopicId = question.TopicId,
			Topic = question.Topic,
			SubTopic = question.SubTopic,

			// Content
			Title = question.Title,
			TextContent = question.TextContent,
			QuestionType = (int)question.QuestionType,
			QuestionTypeName = question.QuestionType.ToString(),
			DifficultyLevel = (int)question.DifficultyLevel,
			DifficultyLevelName = question.DifficultyLevel.ToString(),
			MarksAllocation = question.MarksAllocation,

			// Options
			// IsCorrect hidden from students
			// Never expose answers during assessment
			Options = options.Select(o => new QuestionOptionDto
			{
				Id = o.Id,
				OptionLabel = o.OptionLabel,
				OptionText = o.OptionText,
				IsCorrect = isStudent ? false : o.IsCorrect,
				OrderIndex = o.OrderIndex
			}).ToList(),

			// Board & Media
			BoardSessionId = question.BoardSessionId,
			HasBoardSession = question.HasBoardSession,
			HasMedia = question.HasMedia,
			HasAudio = question.HasAudio,

			// Source
			IsScanned = question.IsScanned,

			// Status
			Status = (int)question.Status,
			StatusName = question.Status.ToString(),

			// Sync
			LastSyncedAt = question.LastSyncedAt?
				.ToString("yyyy-MM-dd HH:mm:ss"),

			// Permissions
			// Owner or admin can edit and delete
			// Only owner can publish their own question
			CanEdit = isOwner || isAdmin,
			CanDelete = isOwner || isAdmin,
			CanPublish = isOwner
				&& question.Status == QuestionStatus.Draft,

			// Audit
			//CreatedByName = question.CreatedByName,
			CreationDate = question.CreationDate,
			ModifiedDate = question.ModifiedDate
		};
		#endregion
	}

	/// <summary>
	/// Maps Question entity to lightweight summary
	/// Only fields needed for list display
	/// Keeps list response memory efficient
	/// </summary>
	private QuestionSummaryDto MapToSummaryDto(Questions question)
	{
		#region
		return new QuestionSummaryDto
		{
			Id = question.Id,
			ClientId = question.ClientId,
			Title = question.Title,
			SubjectName = question.SubTopic,
			Topic = question.Topic,
			QuestionType = (int)question.QuestionType,
			QuestionTypeName = question.QuestionType.ToString(),
			DifficultyLevel = (int)question.DifficultyLevel,
			DifficultyLevelName = question.DifficultyLevel.ToString(),
			MarksAllocation = question.MarksAllocation,
			HasBoardSession = question.HasBoardSession,
			HasMedia = question.HasMedia,
			//HasAudio = question.HasAudio,
			IsScanned = question.IsScanned,
			Status = (int)question.Status,
			StatusName = question.Status.ToString(),
			CreationDate = question.CreationDate
		};
		#endregion
	}

	/// <summary>
	/// Confirm an AI extracted question after teacher review
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///    - Parse and validate UserId and SchoolId
	///
	/// 2. RETRIEVE QUESTION
	///    - Fetch from QuestionBank database
	///    - Verify question exists and is not deleted
	///
	/// 3. CROSS-TENANT CHECK
	///    - Verify question belongs to requesting school
	///
	/// 4. OWNERSHIP CHECK
	///    - Only the teacher who owns the scan session
	///      can confirm its questions
	///    - Admins can confirm any question
	///
	/// 5. STATUS CHECK
	///    - Only PendingReview questions can be confirmed
	///    - Already confirmed questions return success
	///      idempotent behaviour — safe to retry
	///
	/// 6. CONFIRM QUESTION
	///    - Status changes PendingReview → Draft
	///    - Record ReviewedDate and ReviewedBy
	///    - Question now visible in question bank
	///    - Teacher can edit and publish from here
	///
	/// 7. UPDATE SCAN SESSION COUNTS
	///    - Increment TotalConfirmed
	///    - Decrement TotalPending
	///    - Check if all questions reviewed
	///    - If yes mark session ReviewComplete
	///
	/// 8. RETURN RESPONSE
	/// </summary>
	public async Task<BaseResponse> ConfirmQuestion(
		Guid questionId,
		AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("QuestionId", questionId))
		{
			try
			{
				_logger.Information(
					"✅ Confirming question - " +
					"QuestionId: {QuestionId}, " +
					"UserId: {UserId}",
					questionId,
					userClaims.UserId);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 1: VALIDATE USER CLAIMS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					_logger.Warning(
						"❌ Invalid UserId - UserId: {UserId}",
						userClaims.UserId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					_logger.Warning(
						"❌ Invalid SchoolId - SchoolId: {SchoolId}",
						userClaims.SchoolId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				var isAdmin = ClaimsHelper.IsAdmin(userClaims.Role);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 2: RETRIEVE QUESTION
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var question = await _questionQueryRepo.Get(
					questionId,
					DatabaseTarget.QuestionBank);

				if (question == null || question.IsDeleted)
				{
					_logger.Warning(
						"❌ Question not found - " +
						"QuestionId: {QuestionId}",
						questionId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 3: CROSS-TENANT CHECK
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (question.SchoolId != schoolId)
				{
					_logger.Warning(
						"🚫 Cross-tenant access attempt - " +
						"QuestionId: {QuestionId}, " +
						"SchoolId: {SchoolId}",
						questionId,
						schoolId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 4: OWNERSHIP CHECK
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (!isAdmin && question.CreatedBy != userId)
				{
					_logger.Warning(
						"🚫 Ownership check failed - " +
						"QuestionId: {QuestionId}, " +
						"RequestedBy: {UserId}",
						questionId,
						userId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage =
							"You do not have permission " +
							"to confirm this question",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 5: STATUS CHECK
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				// Idempotent behaviour
				// If already confirmed return success
				// Safe for frontend to retry without side effects
				if (question.Status == QuestionStatus.Draft
				 || question.Status == QuestionStatus.Published)
				{
					_logger.Information(
						"⚠️ Question already confirmed - " +
						"QuestionId: {QuestionId}, " +
						"Status: {Status}",
						questionId,
						question.Status);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Question already confirmed",
						Status = "successful"
					};
				}

				if (question.Status != QuestionStatus.PendingReview)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage =
							$"Cannot confirm a question " +
							$"with status: {question.Status}",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 6: CONFIRM QUESTION
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var now = DateTime.UtcNow
					.ToString("yyyy-MM-dd HH:mm:ss");

				var confirmDict = new Dictionary<string, object>
			{
				{ "Status",       (int)QuestionStatus.Draft },
                // PendingReview → Draft
                // Now visible in question bank
                // Teacher can edit and publish

                { "ReviewedDate", now },
				{ "ReviewedBy",   userId },
				{ "ModifiedDate", now }
			};

				await _questionCommandRepo.UpdateTableColumnById(
					confirmDict,
					new KeyValuePair<string, object>("Id", questionId),
					DatabaseTarget.QuestionBank);

				_logger.Information(
					"✅ Question confirmed - " +
					"QuestionId: {QuestionId}",
					questionId);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 7: UPDATE SCAN SESSION COUNTS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (question.ScanSessionId.HasValue)
				{
					await UpdateScanSessionCounts(
						question.ScanSessionId.Value,
						confirmed: 1,
						rejected: 0);
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 8: RETURN RESPONSE
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Question confirmed successfully",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(
					ex,
					"💥 Error confirming question - " +
					"QuestionId: {QuestionId}",
					questionId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage =
						"An error occurred while confirming the question",
					Status = "failed"
				};
			}
		}
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// REJECT QUESTION
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	/// <summary>
	/// Reject an AI extracted question after teacher review
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///
	/// 2. RETRIEVE QUESTION
	///    - Verify question exists and is not deleted
	///
	/// 3. CROSS-TENANT CHECK
	///
	/// 4. OWNERSHIP CHECK
	///    - Only creator or admin can reject
	///
	/// 5. STATUS CHECK
	///    - Only PendingReview questions can be rejected
	///      via this endpoint
	///    - Already rejected (deleted) return success
	///      idempotent behaviour
	///
	/// 6. SOFT DELETE QUESTION
	///    - Teacher decided AI got it wrong entirely
	///    - No need to unpublish — never was published
	///    - Soft delete directly
	///
	/// 7. SOFT DELETE OPTIONS
	///    - Delete associated options if MCQ
	///
	/// 8. UPDATE SCAN SESSION COUNTS
	///    - Increment TotalRejected
	///    - Decrement TotalPending
	///    - Check if all questions reviewed
	///
	/// 9. RETURN RESPONSE
	/// </summary>
	public async Task<BaseResponse> RejectQuestion(
		Guid questionId,
		AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("QuestionId", questionId))
		{
			try
			{
				_logger.Information(
					"❌ Rejecting question - " +
					"QuestionId: {QuestionId}, " +
					"UserId: {UserId}",
					questionId,
					userClaims.UserId);

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

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 2: RETRIEVE QUESTION
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var question = await _questionQueryRepo.Get(
					questionId,
					DatabaseTarget.QuestionBank);

				// Idempotent — already rejected (deleted)
				// return success, safe to retry
				if (question == null || question.IsDeleted)
				{
					_logger.Information(
						"⚠️ Question already rejected - " +
						"QuestionId: {QuestionId}",
						questionId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Question already rejected",
						Status = "successful"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 3: CROSS-TENANT CHECK
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (question.SchoolId != schoolId)
				{
					_logger.Warning(
						"🚫 Cross-tenant access attempt - " +
						"QuestionId: {QuestionId}, " +
						"SchoolId: {SchoolId}",
						questionId,
						schoolId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Question not found",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 4: OWNERSHIP CHECK
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (!isAdmin && question.CreatedBy != userId)
				{
					_logger.Warning(
						"🚫 Ownership check failed - " +
						"QuestionId: {QuestionId}, " +
						"RequestedBy: {UserId}",
						questionId,
						userId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage =
							"You do not have permission " +
							"to reject this question",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 5: STATUS CHECK
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				// Reject endpoint is specifically
				// for PendingReview questions only
				// For Draft or Published questions
				// teacher uses DeleteQuestion instead
				if (question.Status != QuestionStatus.PendingReview)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage =
							"Only questions pending review can be rejected. " +
							"Use delete for draft or published questions",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 6: SOFT DELETE QUESTION
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var now = DateTime.UtcNow
					.ToString("yyyy-MM-dd HH:mm:ss");

				var deleteDict = new Dictionary<string, object>
				{
					{ "IsDeleted",   true },
					{ "IsActive",    false },
					{ "DeletedDate", now },
					{ "DeletedBy",   userId },
					{ "ModifiedDate",now }
				};

				await _questionCommandRepo.UpdateTableColumnById(deleteDict,new KeyValuePair<string, object>("Id", questionId),DatabaseTarget.QuestionBank);

				_logger.Information(
					"Question rejected and soft deleted - " +
					"QuestionId: {QuestionId}",
					questionId);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 7: SOFT DELETE OPTIONS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (question.QuestionType == QuestionType.MultipleChoice)
				{
					var optionDeleteDict = new Dictionary<string, object>
				{
					{ "IsDeleted",   true },
					{ "IsActive",    false },
					{ "DeletedDate", now },
					{ "ModifiedDate",now }
				};

					await _optionCommandRepo.UpdateTableColumnById(optionDeleteDict,new KeyValuePair<string, object>("QuestionId", questionId),DatabaseTarget.QuestionBank);

					_logger.Information(
						"Question options soft deleted - " +
						"QuestionId: {QuestionId}",
						questionId);
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 8: UPDATE SCAN SESSION COUNTS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (question.ScanSessionId.HasValue)
				{
					await UpdateScanSessionCounts(
						question.ScanSessionId.Value,
						confirmed: 0,
						rejected: 1);
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 9: RETURN RESPONSE
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Question rejected successfully",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(
					ex,
					"💥 Error rejecting question - " +
					"QuestionId: {QuestionId}",
					questionId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage =
						"An error occurred while rejecting the question",
					Status = "failed"
				};
			}
		}
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// GET PENDING REVIEW QUESTIONS
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	/// <summary>
	/// Get all PendingReview questions for a scan session
	/// Returns questions alongside original file url
	/// Teacher uses this to review side by side
	///
	/// COMPLETE WORKFLOW:
	///
	/// 1. VALIDATE USER CLAIMS
	///
	/// 2. RETRIEVE SCAN SESSION
	///    - Verify session exists
	///    - Verify teacher owns this session
	///
	/// 3. FETCH PENDING QUESTIONS
	///    - Only PendingReview questions for this session
	///    - Ordered by ExtractedQuestionIndex
	///    - Preserves original document order
	///
	/// 4. BUILD REVIEW RESPONSE
	///    - Include original file url for side by side view
	///    - Include session progress counts
	///    - Include questions with AI confidence scores
	///
	/// 5. UPDATE SESSION STATUS TO InReview
	///    - First time teacher opens session
	///    - Marks that review has started
	///
	/// 6. RETURN RESPONSE
	/// </summary>
	public async Task<PendingReviewResponse> GetPendingReviewQuestions(Guid scanSessionId,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("ScanSessionId", scanSessionId))
		{
			try
			{
				_logger.Information(
					"📋 Getting pending review questions - " +
					"ScanSessionId: {SessionId}, " +
					"UserId: {UserId}",
					scanSessionId,
					userClaims.UserId);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 1: VALIDATE USER CLAIMS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new PendingReviewResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new PendingReviewResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				var isAdmin = ClaimsHelper.IsAdmin(userClaims.Role);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 2: RETRIEVE SCAN SESSION
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var session = await _scanSessionQueryRepo.Get(
					scanSessionId,
					DatabaseTarget.QuestionBank);

				if (session == null || session.IsDeleted)
				{
					_logger.Warning(
						"❌ Scan session not found - " +
						"SessionId: {SessionId}",
						scanSessionId);

					return new PendingReviewResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Scan session not found",
						Status = "failed"
					};
				}

				// Cross-tenant check
				if (session.SchoolId != schoolId)
				{
					return new PendingReviewResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Scan session not found",
						Status = "failed"
					};
				}

				// Ownership check
				// Teacher can only review their own sessions
				if (!isAdmin && session.TeacherId != userId)
				{
					return new PendingReviewResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage =
							"You do not have permission " +
							"to access this scan session",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 3: FETCH PENDING QUESTIONS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				// Fetch all PendingReview questions
				// for this session in original document order
				var query = $@"
                SELECT
                    q.Id,
                    q.ClientId,
                    q.Title,
                    q.Topic,
                    q.SubTopic,
                    q.QuestionType,
                    q.TextContent,
                    q.DifficultyLevel,
                    q.MarksAllocation,
                    q.HasBoardSession,
                    q.HasMedia,
                    q.IsScanned,
                    q.ScanSessionId,
                    q.ExtractedQuestionIndex,
                    q.AIConfidenceScore,
                    q.Status,
                    q.CreationDate,
                    s.SubjectName AS SubjectName
                FROM Questions q
                LEFT JOIN Subjects s
                    ON q.SubjectId = s.Id
                WHERE q.ScanSessionId   = '{scanSessionId}'
                AND   q.Status          = {(int)QuestionStatus.PendingReview}
                AND   q.IsDeleted       = 0
                AND   q.IsActive        = 1
                AND   q.SchoolId        = '{schoolId}'
                ORDER BY q.ExtractedQuestionIndex ASC";
				// Ordered by index to match original document

				var results = await _questionQueryRepo.GetByQueryForQuestion(query,DatabaseTarget.QuestionBank);

				var pendingQuestions = results.ToList().Count < 1 ? new List<QuestionQueryResult>() : results.ToList();

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 4: BUILD REVIEW RESPONSE
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var reviewItems = new List<PendingReviewItem>();

				foreach (var q in pendingQuestions)
				{
					// Fetch options for MCQ questions
					var options = new List<QuestionOptions>();

					if (q.QuestionType == (int)QuestionType.MultipleChoice)
					{
						var optionsQuery = $@"
                        SELECT *
                        FROM QuestionOptions
                        WHERE QuestionId    = '{q.Id}'
                        AND   IsDeleted     = 0
                        AND   IsActive      = 1
                        ORDER BY OrderIndex ASC";

						var optionResults = await _optionQueryRepo
							.GetByQuery(
								optionsQuery,
								DatabaseTarget.QuestionBank);

						options = optionResults?.ToList()
							?? new List<QuestionOptions>();
					}

					// Determine confidence level label
					// for UI display
					var confidenceLabel = GetConfidenceLabel(
						q.AIConfidenceScore);

					reviewItems.Add(new PendingReviewItem
					{
						QuestionId = q.Id,
						Title = q.Title,
						TextContent = q.TextContent,
						SubjectName = q.SubjectName,
						Topic = q.Topic,
						SubTopic = q.SubTopic,
						QuestionType = q.QuestionType,
						QuestionTypeName =
							((QuestionType)q.QuestionType).ToString(),
						DifficultyLevel = q.DifficultyLevel,
						DifficultyLevelName =
							((DifficultyLevel)q.DifficultyLevel).ToString(),
						MarksAllocation = q.MarksAllocation,
						ExtractedQuestionIndex = q.ExtractedQuestionIndex,
						AIConfidenceScore = q.AIConfidenceScore,
						AIConfidenceLabel = confidenceLabel,
						// e.g "High", "Medium", "Low"
						NeedsCloseReview =
							confidenceLabel == "Low",
						// Flag low confidence for teacher attention
						Options = options.Select(o =>
							new QuestionOptionDto
							{
								Id = o.Id,
								OptionLabel = o.OptionLabel,
								OptionText = o.OptionText,
								IsCorrect = o.IsCorrect,
								OrderIndex = o.OrderIndex
							}).ToList()
					});
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 5: UPDATE SESSION STATUS TO InReview
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				// Only update if session is still PendingReview
				// Do not overwrite InReview or ReviewComplete
				if (session.Status == ScanSessionStatus.PendingReview)
				{
					var now = DateTime.UtcNow
						.ToString("yyyy-MM-dd HH:mm:ss");

					var sessionUpdateDict = new Dictionary<string, object>
				{
					{ "Status",      (int)ScanSessionStatus.InReview },
					{ "ModifiedDate", now }
				};

					await _scanSessionCommandRepo.UpdateTableColumnById(
						sessionUpdateDict,
						new KeyValuePair<string, object>(
							"Id", scanSessionId),
						DatabaseTarget.QuestionBank);

					_logger.Information(
						"✅ Session status updated to InReview - " +
						"SessionId: {SessionId}",
						scanSessionId);
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 6: RETURN RESPONSE
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				_logger.Information(
					"✅ Pending review questions retrieved - " +
					"SessionId: {SessionId}, " +
					"Count: {Count}",
					scanSessionId,
					reviewItems.Count);

				return new PendingReviewResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Review questions retrieved",
					Status = "successful",

					// Original file for side by side view
					OriginalFileUrl = session.OriginalFileUrl,
					OriginalFileName = session.OriginalFileName,
					FileType = session.FileType,

					// Session progress
					TotalExtracted = session.TotalExtracted,
					TotalConfirmed = session.TotalConfirmed,
					TotalRejected = session.TotalRejected,
					TotalPending = pendingQuestions.Count,

					// Questions to review
					Questions = reviewItems,

					SessionStatus = (int)session.Status,
					SessionStatusName = session.Status.ToString()
				};
			}
			catch (Exception ex)
			{
				_logger.Error(
					ex,
					"💥 Error getting pending review questions - " +
					"ScanSessionId: {SessionId}",
					scanSessionId);

				return new PendingReviewResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage =
						"An error occurred while retrieving review questions",
					Status = "failed"
				};
			}
		}
	}

	/// <summary>
	/// Updates scan session confirmed and rejected counts
	/// Checks if all questions reviewed
	/// Marks session complete if so
	/// </summary>
	private async Task UpdateScanSessionCounts(
		Guid scanSessionId,
		int confirmed,
		int rejected)
	{
		try
		{
			var session = await _scanSessionQueryRepo.Get(
				scanSessionId,
				DatabaseTarget.QuestionBank);

			if (session == null || session.IsDeleted)
				return;

			var now = DateTime.UtcNow
				.ToString("yyyy-MM-dd HH:mm:ss");

			var newConfirmed = session.TotalConfirmed + confirmed;
			var newRejected = session.TotalRejected + rejected;
			var newPending = session.TotalExtracted
							 - newConfirmed
							 - newRejected;

			// Check if all questions have been reviewed
			var allReviewed = newPending <= 0;

			var updateDict = new Dictionary<string, object>
		{
			{ "TotalConfirmed", newConfirmed },
			{ "TotalRejected",  newRejected },
			{ "TotalPending",   newPending < 0 ? 0 : newPending },
			{ "ModifiedDate",   now }
		};

			// If all reviewed mark session complete
			// CDN cleanup job will pick this up
			if (allReviewed)
			{
				updateDict.Add(
					"Status",
					(int)ScanSessionStatus.ReviewComplete);

				updateDict.Add("CompletedDate", now);

				_logger.Information(
					"✅ Scan session review complete - " +
					"SessionId: {SessionId}, " +
					"Confirmed: {Confirmed}, " +
					"Rejected: {Rejected}",
					scanSessionId,
					newConfirmed,
					newRejected);
			}

			await _scanSessionCommandRepo.UpdateTableColumnById(
				updateDict,
				new KeyValuePair<string, object>(
					"Id", scanSessionId),
				DatabaseTarget.QuestionBank);
		}
		catch (Exception ex)
		{
			// Log but do not block question confirmation
			// Count update failure should not prevent
			// teacher from completing their review
			_logger.Error(
				ex,
				"Error updating scan session counts - " +
				"SessionId: {SessionId}",
				scanSessionId);
		}
	}

	public async Task<QuestionListResponse> GetQuestionsByClassroom(Guid classroomId,QuestionFilterViewModelV2 filter,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("ClassroomId", classroomId))
		{
			try
			{
				_logger.Information(
					"Getting questions by classroom - ClassroomId: {ClassroomId}, UserId: {UserId}",
					classroomId, userClaims.UserId);

				if (!Guid.TryParse(userClaims.UserId, out var userId))
					return new QuestionListResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					return new QuestionListResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};

				filter.Page = filter.Page < 1 ? 1 : filter.Page;
				filter.PageSize = filter.PageSize < 1 ? 20 : filter.PageSize;
				filter.PageSize = filter.PageSize > 50 ? 50 : filter.PageSize;

				var offset = (filter.Page - 1) * filter.PageSize;

				var whereClause = $@"
					WHERE  q.SchoolId    = '{schoolId}'
					AND    q.ClassroomId = '{classroomId}'
					AND    q.IsDeleted   = 0
					AND    q.IsActive    = 1";

				// ── Optional filters ─────────────────────────────────────
				if (filter.SubjectId.HasValue && filter.SubjectId != Guid.Empty)
					whereClause += $" AND q.SubjectId = '{filter.SubjectId.Value}'";

				if (filter.TopicId.HasValue && filter.TopicId != Guid.Empty)
					whereClause += $" AND q.TopicId = '{filter.TopicId.Value}'";

				// Multiple subtopics — IN clause
				if (filter.SubTopicIds?.Any() == true)
				{
					var ids = string.Join(",",
						filter.SubTopicIds.Select(id => $"'{id}'"));
					whereClause += $" AND q.SubTopicId IN ({ids})";
				}

				if (filter.QuestionType.HasValue)
					whereClause += $" AND q.QuestionType = {(int)filter.QuestionType.Value}";

				if (filter.DifficultyLevel.HasValue)
					whereClause += $" AND q.DifficultyLevel = {(int)filter.DifficultyLevel.Value}";

				if (filter.Status.HasValue)
					whereClause += $" AND q.Status = {(int)filter.Status.Value}";
				else if (!filter.IncludePendingReview)
					whereClause += $" AND q.Status != {(int)QuestionStatus.Published}";

				if (!string.IsNullOrWhiteSpace(filter.SearchText))
				{
						var safeSearch = filter.SearchText.Replace("'", "''").Trim();
						whereClause += $@" AND (
						q.Title          LIKE '%{safeSearch}%'
						OR q.Topic       LIKE '%{safeSearch}%'
						OR q.TextContent LIKE '%{safeSearch}%'
					)";
				}

				// ── Total count ──────────────────────────────────────────
				var countQuery = $"SELECT COUNT(*) FROM Questions q {whereClause}";
				var totalCount = await _questionQueryRepo.CountAsync(
					countQuery, DatabaseTarget.QuestionBank);

				if (totalCount == 0)
					return new QuestionListResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "No questions found",
						Status = "successful",
						Questions = new List<QuestionSummaryDto>(),
						TotalCount = 0,
						Page = filter.Page,
						PageSize = filter.PageSize,
						HasMore = false
					};

				// ── Paginated data — names resolved via joins ────────────
				var dataQuery = $@"
					SELECT
						q.Id,
						q.ClientId,
						q.Title,
						q.Topic,
						q.SubTopic,
						q.QuestionType,
						q.DifficultyLevel,
						q.MarksAllocation,
						q.HasBoardSession,
						q.HasMedia,
						q.HasAudio,
						q.IsScanned,
						q.Status,
						q.CreationDate,

						s.Subject  AS SubjectName,
						t.Name     AS TopicName,
						st.Name    AS SubTopicName,
						c.ClassName

					FROM   Questions q
					LEFT JOIN Subjects  s  ON s.Id  = q.SubjectId
					LEFT JOIN Topic     t  ON t.Id  = q.TopicId
					LEFT JOIN SubTopic  st ON st.Id = q.SubTopicId
					LEFT JOIN Classroom c  ON c.Id  = q.ClassroomId
					{whereClause}
					ORDER  BY q.CreationDate DESC
					OFFSET {offset} ROWS
					FETCH NEXT {filter.PageSize} ROWS ONLY";

				var results = await _questionQueryRepo.GetByQuery(
					dataQuery, DatabaseTarget.QuestionBank);

				var questions = results?
					.Select(q => MapToClassroomSummaryDto(q))
					.ToList() ?? new List<QuestionSummaryDto>();

				var hasMore = (filter.Page * filter.PageSize) < totalCount;

				_logger.Information(
					"Questions retrieved - ClassroomId: {ClassroomId}, " +
					"Count: {Count}, Total: {Total}, SubTopicFilter: {SubTopicCount}",
					classroomId, questions.Count, totalCount,
					filter.SubTopicIds?.Count ?? 0);

				return new QuestionListResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Questions retrieved successfully",
					Status = "successful",
					Questions = questions,
					TotalCount = totalCount,
					Page = filter.Page,
					PageSize = filter.PageSize,
					HasMore = hasMore
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Error getting questions by classroom - ClassroomId: {ClassroomId}",
					classroomId);

				return new QuestionListResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving questions",
					Status = "failed",
					Questions = new List<QuestionSummaryDto>()
				};
			}
		}
	}


	public async Task<BaseResponse> GetSubjectQuestionSummary(Guid classroomId,Guid subjectId,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("ClassroomId", classroomId))
		using (LogContext.PushProperty("SubjectId", subjectId))
		{
			try
			{
				_logger.Information(
					"Getting subject question summary - " +
					"ClassroomId: {ClassroomId}, SubjectId: {SubjectId}, UserId: {UserId}",
					classroomId, subjectId, userClaims.UserId);

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

				// ── Base filter shared across all queries ────────────────
				var baseFilter = $@"
					WHERE  q.SchoolId    = '{schoolId}'
					AND    q.ClassroomId = '{classroomId}'
					AND    q.SubjectId   = '{subjectId}'
					AND    q.IsDeleted   = 0
					AND    q.IsActive    = 1";

				// ── Total count for this subject in this classroom ───────
				var totalCount = await _questionQueryRepo.CountAsync($"SELECT COUNT(*) FROM Questions q {baseFilter}",DatabaseTarget.QuestionBank);

				// ── Status breakdown ─────────────────────────────────────
				var statusQuery = $@"
					SELECT
						q.Status,
						COUNT(q.Id) AS QuestionCount
					FROM   Questions q
					{baseFilter}
					GROUP  BY q.Status";

				var statusRows = await _questionQueryRepo.QueryAsync<StatusCountRow>(statusQuery, new Dictionary<string, object>(),DatabaseTarget.QuestionBank);

				// ── Difficulty breakdown ─────────────────────────────────
				var difficultyQuery = $@"
					SELECT
						q.DifficultyLevel,
						COUNT(q.Id) AS QuestionCount
					FROM   Questions q
					{baseFilter}
					GROUP  BY q.DifficultyLevel
					ORDER  BY q.DifficultyLevel ASC";

				var difficultyRows = await _questionQueryRepo.QueryAsync<DifficultyCountRow>(difficultyQuery, new Dictionary<string, object>(),DatabaseTarget.QuestionBank);

				// ── Question type breakdown ──────────────────────────────
				var typeQuery = $@"
					SELECT
						q.QuestionType,
						COUNT(q.Id) AS QuestionCount
					FROM   Questions q
					{baseFilter}
					GROUP  BY q.QuestionType
					ORDER  BY q.QuestionType ASC";

				var typeRows = await _questionQueryRepo.QueryAsync<QuestionTypeCountRow>(typeQuery, new Dictionary<string, object>(),
					DatabaseTarget.QuestionBank);

				// ── Per subtopic counts grouped under topics ─────────────
				var subTopicQuery = $@"
					SELECT
						st.Id          AS SubTopicId,
						st.Name        AS SubTopicName,
						t.Id           AS TopicId,
						t.Name         AS TopicName,
						COUNT(q.Id)    AS QuestionCount,
						SUM(CASE WHEN q.Status = {(int)QuestionStatus.Draft}
								 THEN 1 ELSE 0 END) AS DraftCount,
						SUM(CASE WHEN q.Status = {(int)QuestionStatus.Published}
								 THEN 1 ELSE 0 END) AS PublishedCount,
						SUM(CASE WHEN q.Status = {(int)QuestionStatus.PendingReview}
								 THEN 1 ELSE 0 END) AS PendingReviewCount
					FROM   Questions q
					JOIN   SubTopic  st ON st.Id = q.SubTopicId
					JOIN   Topic     t  ON t.Id  = q.TopicId
					{baseFilter}
					GROUP  BY st.Id, st.Name, t.Id, t.Name
					ORDER  BY t.Name ASC, COUNT(q.Id) DESC";

				var subTopicRows = await _questionQueryRepo.QueryAsync<SubTopicDetailCountRow>(
					subTopicQuery, new Dictionary<string, object>(),
					DatabaseTarget.QuestionBank);

				// ── Group subtopics under their topics ───────────────────
				var subTopicList = subTopicRows?.ToList() ?? new();
				var statusList = statusRows?.ToList() ?? new();

				var topics = subTopicList
					.GroupBy(st => new { st.TopicId, st.TopicName })
					.Select(g => new TopicSummary
					{
						TopicId = g.Key.TopicId,
						TopicName = g.Key.TopicName,
						QuestionCount = g.Sum(st => st.QuestionCount),
						SubTopics = g.Select(st => new SubTopicDetailSummary
						{
							SubTopicId = st.SubTopicId,
							SubTopicName = st.SubTopicName,
							QuestionCount = st.QuestionCount,
							DraftCount = st.DraftCount,
							PublishedCount = st.PublishedCount,
							PendingReviewCount = st.PendingReviewCount
						})
						.OrderByDescending(st => st.QuestionCount)
						.ToList()
					})
					.OrderBy(t => t.TopicName)
					.ToList();

				_logger.Information(
					"Subject question summary retrieved - " +
					"ClassroomId: {ClassroomId}, SubjectId: {SubjectId}, " +
					"Total: {Total}, Topics: {TopicCount}, SubTopics: {SubTopicCount}",
					classroomId, subjectId,
					totalCount, topics.Count, subTopicList.Count);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Question summary retrieved successfully",
					Status = "successful",
					Data = new
					{
						ClassroomId = classroomId,
						SubjectId = subjectId,
						TotalQuestions = totalCount,

						StatusSummary = new
						{
							Draft = statusList
								.FirstOrDefault(s => s.Status == (int)QuestionStatus.Draft)
								?.QuestionCount ?? 0,
							Published = statusList
								.FirstOrDefault(s => s.Status == (int)QuestionStatus.Published)
								?.QuestionCount ?? 0,
							PendingReview = statusList
								.FirstOrDefault(s => s.Status == (int)QuestionStatus.PendingReview)
								?.QuestionCount ?? 0
						},

						DifficultyBreakdown = difficultyRows?
							.Select(d => new
							{
								DifficultyLevel = d.DifficultyLevel,
								DifficultyLevelName = ((DifficultyLevel)d.DifficultyLevel).ToString(),
								QuestionCount = d.QuestionCount
							}).ToList(),

						TypeBreakdown = typeRows?
							.Select(t => new
							{
								QuestionType = t.QuestionType,
								QuestionTypeName = ((QuestionType)t.QuestionType).ToString(),
								QuestionCount = t.QuestionCount
							}).ToList(),

						// Topics → SubTopics hierarchy
						Topics = topics
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Error getting subject question summary - " +
					"ClassroomId: {ClassroomId}, SubjectId: {SubjectId}",
					classroomId, subjectId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving the summary",
					Status = "failed"
				};
			}
		}
	}

	private async Task<int> GetStatusCount(Guid schoolId, Guid classroomId, QuestionStatus status)
	{
		var query = $@"
        SELECT COUNT(*) FROM Questions
        WHERE  SchoolId    = '{schoolId}'
        AND    ClassroomId = '{classroomId}'
        AND    Status      = {(int)status}
        AND    IsDeleted   = 0
        AND    IsActive    = 1";

		return await _questionQueryRepo.CountAsync(
			query, DatabaseTarget.QuestionBank);
	}

	private QuestionSummaryDto MapToClassroomSummaryDto(Questions question)
	{
		return new QuestionSummaryDto
		{
			Id = question.Id,
			ClientId = question.ClientId,
			Title = question.Title,
			Topic = question.Topic,
			SubjectName = question.SubjectName,
			TopicName = question.TopicName,
			SubTopicName = question.SubTopicName,
			ClassName = question.ClassName,
			QuestionType = (int)question.QuestionType,
			QuestionTypeName = question.QuestionType.ToString(),
			DifficultyLevel = (int)question.DifficultyLevel,
			DifficultyLevelName = question.DifficultyLevel.ToString(),
			MarksAllocation = question.MarksAllocation,
			HasBoardSession = question.HasBoardSession,
			HasMedia = question.HasMedia,
			IsScanned = question.IsScanned,
			Status = (int)question.Status,
			StatusName = question.Status.ToString(),
			CreationDate = question.CreationDate
		};
	}

	/// <summary>
	/// Converts AI confidence score string to
	/// human readable label for UI display
	/// </summary>
	private string GetConfidenceLabel(string confidenceScore)
	{
		if (string.IsNullOrWhiteSpace(confidenceScore))
			return "Unknown";

		if (!decimal.TryParse(confidenceScore, out var score))
			return "Unknown";

		return score switch
		{
			>= 0.90m => "High",
			>= 0.70m => "Medium",
			_ => "Low"
		};
	}

	//public Task<CreateQuestionResponse> CreateQuestion(CreateQuestionViewModel model, TechHub.Core.Model.AuthenticatedUserClaims userClaims)
	//{
	//	throw new NotImplementedException();
	//}

	//public Task<UpdateQuestionResponse> UpdateQuestion(UpdateQuestionViewModel model, TechHub.Core.Model.AuthenticatedUserClaims userClaims)
	//{
	//	throw new NotImplementedException();
	//}

	//public Task<QuestionDetailResponse> GetQuestion(Guid questionId, TechHub.Core.Model.AuthenticatedUserClaims userClaims)
	//{
	//	throw new NotImplementedException();
	//}

	//public Task<QuestionListResponse> GetSubjectQuestions(Guid subjectId, QuestionFilterViewModel filter, TechHub.Core.Model.AuthenticatedUserClaims userClaims)
	//{
	//	throw new NotImplementedException();
	//}

	//public Task<BaseResponse> DeleteQuestion(Guid questionId, TechHub.Core.Model.AuthenticatedUserClaims userClaims)
	//{
	//	throw new NotImplementedException();
	//}

	//public Task<BaseResponse> PublishQuestion(Guid questionId, TechHub.Core.Model.AuthenticatedUserClaims userClaims)
	//{
	//	throw new NotImplementedException();
	//}

	//public Task<BaseResponse> ConfirmQuestion(Guid questionId, TechHub.Core.Model.AuthenticatedUserClaims userClaims)
	//{
	//	throw new NotImplementedException();
	//}

	//public Task<BaseResponse> RejectQuestion(Guid questionId, TechHub.Core.Model.AuthenticatedUserClaims userClaims)
	//{
	//	throw new NotImplementedException();
	//}

	//public Task<PendingReviewResponse> GetPendingReviewQuestions(Guid scanSessionId, TechHub.Core.Model.AuthenticatedUserClaims userClaims)
	//{
	//	throw new NotImplementedException();
	//}

	//public Task<QuestionListResponse> GetQuestionsByClassroom(Guid classroomId, QuestionFilterViewModelV2 filter, TechHub.Core.Model.AuthenticatedUserClaims userClaims)
	//{
	//	throw new NotImplementedException();
	//}

	//public Task<BaseResponse> GetSubjectQuestionSummary(Guid classroomId, Guid subjectId, TechHub.Core.Model.AuthenticatedUserClaims userClaims)
	//{
	//	throw new NotImplementedException();
	//}
}
