using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.Entities;
using TechHub.QuestionBank.Core.Enums;
using TechHub.QuestionBank.Core.Response;
using TechHub.QuestionBank.Core.ViewModel;
using TechHub.QuestionBank.Services.interfaces;
using TechHub.Service.Interface;
using TechhubMS.util;

namespace TechHub.QuestionBank.Services;

public class QuestionScanService : IQuestionScanService
{
	private readonly IQueryRepository<ScanToken> _tokenQueryRepo;
	private readonly ICommandRespository<ScanToken> _tokenCommandRepo;
	private readonly IQueryRepository<ScanSession> _sessionQueryRepo;
	private readonly ICommandRespository<ScanSession> _sessionCommandRepo;
	private readonly IQuestionService _questionService;
	private readonly IConfiguration _configuration;
	private readonly ILogger _logger;

	// Daily scan quota per teacher
	// Can be moved to appsettings or
	// school subscription settings later
	private const int DefaultDailyQuota = 20;

	// Token TTL in minutes
	private const int TokenTtlMinutes = 10;

	public QuestionScanService(
		IQueryRepository<ScanToken> tokenQueryRepo,
		ICommandRespository<ScanToken> tokenCommandRepo,
		IQueryRepository<ScanSession> sessionQueryRepo,
		ICommandRespository<ScanSession> sessionCommandRepo,
		IQuestionService questionService,
		IConfiguration configuration,
		ILogger logger)
	{
		_tokenQueryRepo = tokenQueryRepo;
		_tokenCommandRepo = tokenCommandRepo;
		_sessionQueryRepo = sessionQueryRepo;
		_sessionCommandRepo = sessionCommandRepo;
		_questionService = questionService;
		_configuration = configuration;
		_logger = logger;
	}

	/// <summary>
	/// Request scan token
	///
	/// WORKFLOW:
	/// 1. VALIDATE USER CLAIMS
	/// 2. CHECK DAILY QUOTA
	///    - Count tokens used today by this teacher
	///    - Reject if at or above limit
	/// 3. CHECK SCHOOL SUBSCRIPTION
	///    - Verify school allows scan feature
	///    - Check monthly school limit
	/// 4. CREATE TOKEN
	///    - Generate token record
	///    - Set 10 minute expiry
	/// 5. RETURN TOKEN
	///    - Return TokenId to frontend
	///    - Frontend stores for proxy call
	/// </summary>
	public async Task<RequestScanTokenResponse> RequestScanToken(RequestScanTokenViewModel model, AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		{
			try
			{
				_logger.Information(
					"Requesting scan token UserId: {UserId}, ScanType: {ScanType}", userClaims.UserId, model.ScanType);

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new RequestScanTokenResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new RequestScanTokenResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				var todayStart = DateTime.UtcNow.Date.ToString("yyyy-MM-dd 00:00:00");
				var todayEnd = DateTime.UtcNow.Date.ToString("yyyy-MM-dd 23:59:59");

				var quotaQuery = $@"
                        SELECT COUNT(*)
                        FROM ScanTokens
                        WHERE TeacherId     = '{userId}'
                        AND   SchoolId      = '{schoolId}'
                        AND   Status        != {(int)ScanTokenStatus.Revoked}
                        AND   CreationDate  >= '{todayStart}'
                        AND   CreationDate  <= '{todayEnd}'";

				var usedToday = await _tokenQueryRepo.CountAsync(quotaQuery, DatabaseTarget.QuestionBank);

				var remaining = DefaultDailyQuota - usedToday;

				if (remaining <= 0)
				{
					_logger.Warning("Daily quota exceeded - UserId: {UserId}, Used: {Used}, Limit: {Limit}", userId, usedToday, DefaultDailyQuota);

					return new RequestScanTokenResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"Daily scan limit of {DefaultDailyQuota} " + $"reached. Resets at midnight UTC.",
						Status = "failed"
					};
				}


				var allowedFileTypes = new[] { "image", "pdf" };
				if (!allowedFileTypes.Contains(model.FileType?.ToLower()))
				{
					return new RequestScanTokenResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "File type must be 'image' or 'pdf'",
						Status = "failed"
					};
				}

				var now = DateTime.UtcNow;
				var nowStr = now.ToString("yyyy-MM-dd HH:mm:ss");
				var expiresAt = now.AddMinutes(TokenTtlMinutes).ToString("yyyy-MM-dd HH:mm:ss");

				var token = new ScanToken
				{
					Id = Guid.NewGuid(),
					TeacherId = userId,
					SchoolId = schoolId,
					Purpose = "question_scan",
					ScanType = (int)model.ScanType,
					FileType = model.FileType.ToLower(),
					Status = ScanTokenStatus.Active,
					IssuedAt = nowStr,
					ExpiresAt = expiresAt,
					LocalSessionId = model.LocalSessionId,
					DeviceId = model.DeviceId,
					CreationDate = nowStr
				};

				await _tokenCommandRepo.Create(token, DatabaseTarget.QuestionBank);

				_logger.Information("Scan token created - " + "TokenId: {TokenId}, " + "ExpiresAt: {ExpiresAt}", token.Id, expiresAt);

				return new RequestScanTokenResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Scan token issued",
					Status = "successful",
					TokenId = token.Id,
					ExpiresAt = expiresAt,
					RemainingDailyQuota = remaining - 1,
					// Subtract 1 — this token counts
					DailyLimit = DefaultDailyQuota
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error requesting scan token - UserId: {UserId}", userClaims.UserId);

				return new RequestScanTokenResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage =
						"An error occurred while requesting scan token",
					Status = "failed"
				};
			}
		}
	}

	/// <summary>
	/// Thin proxy to Claude API
	///
	/// WORKFLOW:
	/// 1. VALIDATE TOKEN
	///    - Exists in DB
	///    - Not expired
	///    - Not already used
	///    - Not revoked
	///    - Belongs to requesting teacher
	///
	/// 2. MARK TOKEN AS USED
	///    - Immediately — before Claude call
	///    - Prevents replay even if call fails
	///
	/// 3. BUILD PROMPT
	///    - Base prompt always included
	///    - Scan type specific additions appended
	///
	/// 4. FORWARD TO CLAUDE WITH STREAMING
	///    - Convert image to base64
	///    - Send to Claude vision API
	///    - Stream response chunks back via SSE
	///
	/// 5. LOG USAGE
	///    - Record tokens consumed
	///    - Fire and forget
	/// </summary>
	public async Task ProcessScan(Guid tokenId, IFormFile image, HttpContext httpContext)
	{
		var userId = httpContext.User.FindFirst("UserId")?.Value;
		var schoolId = httpContext.User.FindFirst("SchoolId")?.Value;

		using (LogContext.PushProperty("TokenId", tokenId))
		using (LogContext.PushProperty("UserId", userId))
		{
			// Set up SSE response headers
			// Before any validation so frontend
			// knows this is a streaming endpoint
			httpContext.Response.Headers.Add("Content-Type", "text/event-stream");
			httpContext.Response.Headers.Add("Cache-Control", "no-cache");
			httpContext.Response.Headers.Add("X-Accel-Buffering", "no");
			// Prevents nginx from buffering SSE

			try
			{

				_logger.Information("Processing scan - TokenId: {TokenId}", tokenId);

				var token = await _tokenQueryRepo.Get(tokenId, DatabaseTarget.QuestionBank);

				if (token == null)
				{
					await SendSseError(httpContext, "Invalid scan token");
					return;
				}

				// Verify token belongs to requesting teacher
				if (!Guid.TryParse(userId, out var userGuid) || token.TeacherId != userGuid)
				{
					await SendSseError(httpContext, "Token does not belong to this user");
					return;
				}

				// Check token not already used
				if (token.Status == ScanTokenStatus.Used)
				{
					await SendSseError(httpContext, "Token already used. " + "Request a new token to scan again.");
					return;
				}

				// Check token not revoked
				if (token.Status == ScanTokenStatus.Revoked)
				{
					await SendSseError(httpContext, "Token has been revoked");
					return;
				}

				// Check token not expired
				var expiresAt = DateTime.Parse(token.ExpiresAt);
				if (DateTime.UtcNow > expiresAt)
				{
					// Mark as expired
					await MarkTokenExpired(token.Id);

					await SendSseError(httpContext, "Token expired. " + "Request a new token to scan again.");
					return;
				}

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				var markUsedDict = new Dictionary<string, object>
				{
					{ "Status", (int)ScanTokenStatus.Used },
					{ "UsedAt", now }
				};

				await _tokenCommandRepo.UpdateTableColumnById(markUsedDict, new KeyValuePair<string, object>("Id", token.Id), DatabaseTarget.QuestionBank);

				_logger.Information("Token marked as used - TokenId: {TokenId}", tokenId);

				var scanType = (ScanType)token.ScanType;
				var prompt = BuildPrompt(scanType);

				string imageBase64;
				string mediaType;

				using (var memoryStream = new MemoryStream())
				{
					await image.CopyToAsync(memoryStream);
					imageBase64 = Convert.ToBase64String(memoryStream.ToArray());
				}

				// Determine media type for Claude
				mediaType = image.ContentType switch
				{
					"image/jpeg" => "image/jpeg",
					"image/jpg" => "image/jpeg",
					"image/png" => "image/png",
					"image/webp" => "image/webp",
					"image/gif" => "image/gif",
					_ => "image/jpeg"
					// Default to jpeg for unknown types
				};

				// Send progress event to frontend
				// Teacher sees "Processing..." immediately
				await SendSseEvent(httpContext, "progress",
					JsonSerializer.Serialize(new
					{
						message = "Image received. Extracting questions...",
						stage = "processing"
					}));

				var apiKey = _configuration["Anthropic:ApiKey"];
				var client = new AnthropicClient(apiKey);

				var messages = new List<Message>
					{
						new Message
						{
							Role    = RoleType.User,
							Content = new List<ContentBase>
							{
								new ImageContent
								{
									Source = new ImageSource
									{
										Type      = SourceType.base64,
										MediaType = mediaType,
										Data      = imageBase64
									}
								},
								new TextContent
								{
									Text = prompt
								}
							}
						}
					};

				var parameters = new MessageParameters
				{
					Model = Anthropic.SDK.Constants.AnthropicModels.Claude4Sonnet,
					MaxTokens = 4096,
					Messages = messages,
					System = new List<SystemMessage>
					{
						new SystemMessage(BuildSystemPrompt())
					},
					Stream = true
					// Streaming enabled
					// Response comes back in chunks
				};

				// Accumulate full response for logging
				var fullResponse = new StringBuilder();
				var tokensConsumed = 0;

				// Stream response chunks to frontend via SSE
				await foreach (var res in client.Messages.StreamClaudeMessageAsync(parameters,httpContext.RequestAborted))
				{
					foreach(var block in res.Content ?? new List<ContentBase>())
					{
						if (block is TextContent textBlock && !string.IsNullOrEmpty(textBlock.Text))
						{
							//var text = streamEvent.Delta.Text ?? "";
							fullResponse.Append(textBlock.Text);

							// Pipe each chunk immediately
							// to Web Worker via SSE
							await SendSseEvent(httpContext,"chunk",
								JsonSerializer.Serialize(new
								{
									text = textBlock.Text
								}));
						}

						// Capture token usage when available
						//if (streamEvent.Usage != null)
						//{
						//	tokensConsumed =
						//		streamEvent.Usage.InputTokens +
						//		streamEvent.Usage.OutputTokens;
						//}
					}
				}

				// Send completion event
				// Web Worker knows stream is done
				// Parses accumulated response
				await SendSseEvent(httpContext,"complete",
					JsonSerializer.Serialize(new
					{
						message = "Extraction complete",
						stage = "complete"
					}));

				_logger.Information("Scan streaming complete - TokenId: {TokenId}, TokensConsumed: {Tokens}", tokenId, tokensConsumed);

				// Fire and forget — does not block response
				_ = Task.Run(async () =>
				{
					try
					{
						var usageDict = new Dictionary<string, object>
						{
							{ "TokensConsumed", tokensConsumed }
						};

						await _tokenCommandRepo.UpdateTableColumnById(usageDict, new KeyValuePair<string, object>("Id", token.Id), DatabaseTarget.QuestionBank);
					}
					catch (Exception ex)
					{
						_logger.Error(ex,"Failed to log token usage - TokenId: {TokenId}",tokenId);
					}
				});
			}
			catch (OperationCanceledException)
			{
				// Teacher closed tab or navigated away
				// Stream connection dropped
				// IndexedDB entry preserves any
				// partial response that arrived
				_logger.Warning("Scan stream cancelled - TokenId: {TokenId}", tokenId);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error processing scan - TokenId: {TokenId}", tokenId);

				await SendSseError(httpContext, "An error occurred during extraction. Please try again.");
			}
		}
	}

	/// <summary>
	/// Save confirmed questions after teacher review
	///
	/// WORKFLOW:
	/// 1. VALIDATE USER CLAIMS
	/// 2. IDEMPOTENCY CHECK
	///    - If LocalSessionId already processed
	///      return existing result
	///    - Prevents duplicate saves on retry
	/// 3. CREATE SCAN SESSION
	/// 4. SAVE EACH QUESTION AS PENDINGREVIEW
	/// 5. UPDATE SESSION COUNTS
	/// 6. RETURN RESPONSE
	///    - Frontend cleans IndexedDB on success
	/// </summary>
	public async Task<SaveScanResultsResponse> SaveScanResults(SaveScanResultsViewModel model, AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("LocalSessionId", model.LocalSessionId))
		{
			try
			{
				_logger.Information("Saving scan results - " + "LocalSessionId: {SessionId}, " + "QuestionCount: {Count}", model.LocalSessionId, model.Questions?.Count ?? 0);

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new SaveScanResultsResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new SaveScanResultsResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				if (string.IsNullOrWhiteSpace(model.LocalSessionId))
				{
					return new SaveScanResultsResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "LocalSessionId is required",
						Status = "failed"
					};
				}

				if (model.Questions == null || !model.Questions.Any())
				{
					return new SaveScanResultsResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage =
							"No questions provided to save",
						Status = "failed"
					};
				}

				var existingSessionQuery = $@"
                        SELECT TOP 1 *
                        FROM ScanSessions
                        WHERE LocalSessionId    = '{model.LocalSessionId}'
                        AND   TeacherId         = '{userId}'
                        AND   SchoolId          = '{schoolId}'
                        AND   IsDeleted         = 0";

				var existingSessions = await _sessionQueryRepo.GetByQuery(existingSessionQuery, DatabaseTarget.QuestionBank);

				var existingSession = existingSessions?.FirstOrDefault();

				if (existingSession != null)
				{
					_logger.Information("Duplicate save detected - " + "LocalSessionId: {SessionId}, " + "ExistingSessionId: {ExistingId}", model.LocalSessionId, existingSession.Id);

					// Fetch existing saved questions
					// and return them as if just saved
					var existingQuestionsQuery = $@"
                            SELECT Id, ExtractedQuestionIndex, ClientId
                            FROM Questions
                            WHERE ScanSessionId = '{existingSession.Id}'
                            AND   IsDeleted     = 0";

					var existingQuestions = await _sessionQueryRepo.GetByQueryForQuestion(existingQuestionsQuery, DatabaseTarget.QuestionBank);

					return new SaveScanResultsResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Questions already saved",
						Status = "successful",
						ScanSessionId = existingSession.Id,
						LocalSessionId = model.LocalSessionId,
						SavedQuestions = existingQuestions?
							.Select(q => new SavedQuestionMap
							{
								ExtractedQuestionIndex =
									q.ExtractedQuestionIndex ?? 0,
								QuestionId = q.Id,
								ClientId = q.ClientId
							}).ToList()
							?? new List<SavedQuestionMap>(),
						TotalSaved = existingQuestions?.Count() ?? 0,
						TotalFailed = 0
					};
				}

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				var scanSession = new ScanSession
				{
					Id = Guid.NewGuid(),
					SchoolId = schoolId,
					TeacherId = userId,
					OriginalFileName = model.OriginalFileName,
					FileType = model.FileType,
					TotalExtracted = model.Questions.Count,
					TotalConfirmed = 0,
					TotalRejected = 0,
					TotalPending = model.Questions.Count,
					Status = ScanSessionStatus.PendingReview,
					AIModel = "claude-3-sonnet",
					ExtractionCompletedDate = now,
					IsActive = true,
					IsDeleted = false,
					CreationDate = now,
					ModifiedDate = now
				};

				await _sessionCommandRepo.Create(scanSession, DatabaseTarget.QuestionBank);

				_logger.Information("Scan session created - " + "SessionId: {SessionId}", scanSession.Id);


				var savedQuestions = new List<SavedQuestionMap>();
				var failedCount = 0;

				foreach (var questionModel in model.Questions)
				{
					try
					{
						// Ensure scan session fields are set
						questionModel.ScanSessionId = scanSession.Id;
						questionModel.IsScanned = true;
						var createResult = await _questionService.CreateQuestion(questionModel, userClaims);

						if (createResult.ResponseCode == ResponseCode.successful)
						{
							savedQuestions.Add(new SavedQuestionMap
							{
								ExtractedQuestionIndex =questionModel.ExtractedQuestionIndex ?? 0,
								QuestionId = createResult.QuestionId,
								ClientId = questionModel.ClientId
							});
						}
						else
						{
							failedCount++;

							_logger.Warning("Question save failed - Index: {Index}, Reason: {Reason}", questionModel.ExtractedQuestionIndex, createResult.ResponseMessage);
						}
					}
					catch (Exception ex)
					{
						failedCount++;

						_logger.Error(ex, "Error saving question - Index: {Index}", questionModel.ExtractedQuestionIndex);
					}
				}

				_logger.Information("Questions saved - SessionId: {SessionId}, Saved: {Saved}, Failed: {Failed}",scanSession.Id, savedQuestions.Count, failedCount);
				// Frontend receives ScanSessionId and
				// question IDs then cleans IndexedDB entry
				// Questions now in question bank
				// as PendingReview
				// Teacher reviews via
				// GetPendingReviewQuestions endpoint
				// from Stage 1

				return new SaveScanResultsResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = $"Saved {savedQuestions.Count} questions " + $"for review",
					Status = "successful",
					ScanSessionId = scanSession.Id,
					LocalSessionId = model.LocalSessionId,
					SavedQuestions = savedQuestions,
					TotalSaved = savedQuestions.Count,
					TotalFailed = failedCount
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error saving scan results - LocalSessionId: {SessionId}", model.LocalSessionId);

				return new SaveScanResultsResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage =
						"An error occurred while saving questions",
					Status = "failed"
				};
			}
		}
	}

	/// <summary>
	/// Get teacher scan quota
	/// </summary>
	public async Task<ScanQuotaResponse> GetScanQuota(AuthenticatedUserClaims userClaims)
	{
		try
		{
			if (!Guid.TryParse(userClaims.UserId, out var userId) || !Guid.TryParse(userClaims.SchoolId, out var schoolId))
			{
				return new ScanQuotaResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = "Invalid user identification",
					Status = "failed"
				};
			}

			var todayStart = DateTime.UtcNow.Date.ToString("yyyy-MM-dd 00:00:00");
			var todayEnd = DateTime.UtcNow.Date.ToString("yyyy-MM-dd 23:59:59");

			var quotaQuery = $@"
                    SELECT COUNT(*)
                    FROM ScanTokens
                    WHERE TeacherId     = '{userId}'
                    AND   SchoolId      = '{schoolId}'
                    AND   Status        != {(int)ScanTokenStatus.Revoked}
                    AND   CreationDate  >= '{todayStart}'
                    AND   CreationDate  <= '{todayEnd}'";

			var usedToday = await _tokenQueryRepo.CountAsync(quotaQuery, DatabaseTarget.QuestionBank);

			var remaining = Math.Max(0, DefaultDailyQuota - usedToday);
			var resetsAt = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd 00:00:00");

			return new ScanQuotaResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Quota retrieved",
				Status = "successful",
				DailyLimit = DefaultDailyQuota,
				UsedToday = usedToday,
				RemainingToday = remaining,
				CanScan = remaining > 0,
				ResetsAt = resetsAt
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error getting scan quota");

			return new ScanQuotaResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An error occurred",
				Status = "failed"
			};
		}
	}


/// <summary>
        /// Builds the base system prompt
        /// Always included regardless of scan type
        /// </summary>
    private string BuildSystemPrompt()
	{
		return @"
			You are an expert at extracting exam questions
			from images of textbooks and past papers.

			CRITICAL RULES:
			- Return ONLY valid JSON. No preamble, explanation
			  or markdown code blocks.
			- Separate question content from answer content.
			- questionText must contain ONLY what a student
			  would see in an exam — never include the answer.
			- If an answer is visible extract it separately
			  into isCorrect (MCQ) or modelAnswer (essay).
			- Never guess answers — only extract what is
			  clearly visible in the image.
			- If you cannot read text clearly set
			  confidenceScore below 0.7 and note it
			  in extractionNotes.";
	}

	/// <summary>
	/// Builds user prompt based on scan type
	/// Base instruction always included
	/// Type-specific additions appended
	/// </summary>
	private string BuildPrompt(ScanType scanType)
	{
		var basePrompt = @"
		Extract all exam questions from this image.
		Return a JSON object in exactly this structure:
		{
		  ""questions"": [
			{
			  ""index"": 1,
			  ""questionText"": ""..."",
			  ""questionType"": ""MCQ|ShortAnswer|Essay|TrueOrFalse|FillInTheBlank"",
			  ""options"": [
				{ ""label"": ""A"", ""text"": ""..."", ""isCorrect"": false }
			  ],
			  ""marksAllocation"": null,
			  ""modelAnswer"": null,
			  ""answerVisible"": false,
			  ""answerConfidence"": null,
			  ""requiresBoardContent"": false,
			  ""hasdiagram"": false,
			  ""confidenceScore"": 0.95,
			  ""extractionNotes"": """"
			}
		  ],
		  ""totalFound"": 1,
		  ""pageNotes"": """"
		}";

		// Type-specific additions
		var typeAddition = scanType switch
		{
					ScanType.QuestionsOnly => @"
			This page contains questions only.
			Do not look for or extract answers.
			Set answerVisible to false for all questions.
			Set modelAnswer to null for all questions.",

			ScanType.QuestionsWithAnswers => @"
			This page contains questions with answers marked.
			Extract both question text and visible answers.
			For MCQ set isCorrect true on the correct option.
			Set answerVisible true when answer is found.
			Set answerConfidence based on how clearly
			the answer is marked.",

			ScanType.EssayQuestions => @"
			This page contains essay or long-form questions.
			Extract mark allocations carefully — they are critical.
			questionType should be Essay for all questions.
			options array should be empty for essay questions.
			If model answers are visible extract into modelAnswer.",

			ScanType.MarkingScheme => @"
			This is a marking scheme or answer booklet.
			Extract both questions and their model answers.
			modelAnswer should contain the expected response.
			Include mark breakdowns in extractionNotes.
			answerVisible should be true for all questions.",

			ScanType.MixedPaper => @"
			This page contains multiple question types.
			Determine the correct questionType per question.
			Handle MCQ, Essay, ShortAnswer independently.
			Extract answers only where clearly visible.",

			_ => string.Empty
		};

		return basePrompt + typeAddition;
	}

	/// <summary>
	/// Sends an SSE event to the frontend
	/// </summary>
	private async Task SendSseEvent(HttpContext context,string eventName,string data)
	{
		var sseMessage =
			$"event: {eventName}\n" +
			$"data: {data}\n\n";

		await context.Response.WriteAsync(sseMessage);
		await context.Response.Body.FlushAsync();
	}

	/// <summary>
	/// Sends an SSE error event to the frontend
	/// Web Worker receives this and
	/// reports error to main thread
	/// </summary>
	private async Task SendSseError(HttpContext context,string message)
	{
		await SendSseEvent(context,"error",
			JsonSerializer.Serialize(new
			{
				message = message,
				stage = "error"
			}));
	}

	/// <summary>
	/// Marks token as expired in DB
	/// </summary>
	private async Task MarkTokenExpired(Guid tokenId)
	{
		try
		{
			var expiredDict = new Dictionary<string, object>
			{
				{ "Status", (int)ScanTokenStatus.Expired }
			};

			await _tokenCommandRepo.UpdateTableColumnById(expiredDict,new KeyValuePair<string, object>("Id", tokenId),DatabaseTarget.QuestionBank);
		}
		catch (Exception ex)
		{
			_logger.Error(ex,"Failed to mark token expired - TokenId: {TokenId}", tokenId);
		}
	}

}
