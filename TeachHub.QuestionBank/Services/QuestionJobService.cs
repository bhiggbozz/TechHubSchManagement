using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
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
using TechHub.Service.Service.DatabaseService;
using static System.Formats.Asn1.AsnWriter;

namespace TechHub.QuestionBank.Services;

public class QuestionJobService : IQuestionJobService
{
	private readonly IQueryRepository<QuestionJob> _jobQueryRepo;
	private readonly ICommandRespository<QuestionJob> _jobCommandRepo;
	private readonly IQueryRepository<Questions> _questionQueryRepo;
	private readonly ICommandRespository<Questions> _questionCommandRepo;
	private readonly IQueryRepository<QuestionOptions> _optionQueryRepo;
	private readonly ICommandRespository<QuestionOptions> _optionCommandRepo;
	private readonly IQueryRepository<SubTopic> _subTopicQueryRepo;
	private readonly IConnectionStringResolver _resolver;

	private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;
	private readonly ICloudinaryService _cloudinaryService;
	private readonly IConfiguration _configuration;
	private readonly ILogger _logger;

	// Max Claude call attempts per job before
	// marking permanently Failed
	private const int MaxAttempts = 10;

	public QuestionJobService(
		IQueryRepository<QuestionJob> jobQueryRepo,
		ICommandRespository<QuestionJob> jobCommandRepo,
		IQueryRepository<Questions> questionQueryRepo,
		ICommandRespository<Questions> questionCommandRepo,
		IQueryRepository<QuestionOptions> optionQueryRepo,
		ICommandRespository<QuestionOptions> optionCommandRepo,
		IQueryRepository<SubTopic> subTopicQueryRepo,
		IDbTransactionScopeFactory dbTransactionScopeFactory,
		IConnectionStringResolver resolver,
		ICloudinaryService cloudinaryService,
		IConfiguration configuration,
		ILogger logger)
	{
		_jobQueryRepo = jobQueryRepo;
		_jobCommandRepo = jobCommandRepo;
		_questionQueryRepo = questionQueryRepo;
		_questionCommandRepo = questionCommandRepo;
		_optionQueryRepo = optionQueryRepo;
		_optionCommandRepo = optionCommandRepo;
		_subTopicQueryRepo = subTopicQueryRepo;
		_cloudinaryService = cloudinaryService;
		_dbTransactionScopeFactory = dbTransactionScopeFactory;
		_resolver = resolver;
		_configuration = configuration;
		_logger = logger;
	}

	// ═══════════════════════════════════════════════════════════
	// STEP 1: SUBMIT JOB
	// Teacher uploads image → backend saves to temp → logs job
	// Returns JobId in ~200ms — teacher never waits
	// ═══════════════════════════════════════════════════════════

	/// <summary>
	/// WORKFLOW:
	/// 1. Validate user claims
	/// 2. Validate input
	/// 3. Verify SubTopic exists and belongs to school
	/// 4. Upload image to Cloudinary temp folder
	/// 5. Log QuestionJob as Pending
	/// 6. Return JobId immediately
	/// </summary>
	public async Task<SubmitJobResponse> SubmitJob(IFormFile image,SubmitQuestionJobViewModel model,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		{
			try
			{
				_logger.Information(
					"Submitting question job - UserId: {UserId}, SubTopicId: {SubTopicId}",
					userClaims.UserId, model.SubTopicId);

				if (!Guid.TryParse(userClaims.UserId, out var userId))
					return Fail<SubmitJobResponse>("Invalid user identification");

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					return Fail<SubmitJobResponse>("Invalid school identification");

				// ── Validate image ──────────────────────────────────
				if (image == null || image.Length == 0)
					return Fail<SubmitJobResponse>("Image file is required");

				var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
				if (!allowedTypes.Contains(image.ContentType?.ToLower()))
					return Fail<SubmitJobResponse>("Only JPEG, PNG and WebP images are supported");

				var maxSizeMb = 10;
				if (image.Length > maxSizeMb * 1024 * 1024)
					return Fail<SubmitJobResponse>($"Image cannot exceed {maxSizeMb}MB");

				// ── Validate question type ─────────────────────────
				var validTypes = new[] { "Objective", "Theory", "TrueFalse" };
				if (!validTypes.Contains(model.QuestionType))
					return Fail<SubmitJobResponse>("Question type must be Objective, Theory or TrueFalse");

				if (model.MarksAllocation <= 0)
					return Fail<SubmitJobResponse>("Marks allocation must be greater than zero");

				// ── Verify SubTopic exists and belongs to school ───
				var subTopic = await _subTopicQueryRepo.Get(
					model.SubTopicId, DatabaseTarget.QuestionBank);

				if (subTopic == null || subTopic.IsDeleted || subTopic.SchoolId != schoolId)
					return Fail<SubmitJobResponse>("SubTopic not found");

				// ── Upload image to Cloudinary temp folder ─────────
				// Uses TempPending folder — auto-deleted by policy
				// Background worker reads from here
				// No processing — just a raw dump, fast
				var jobId = Guid.NewGuid();
				var mediaKey = $"qjob_{jobId}";

				using var stream = image.OpenReadStream();

				var uploadResult = await _cloudinaryService.UploadMediaAsync(stream,mediaKey,schoolId,MediaType.Image,isTemporary: true);

				if (!uploadResult.Success)
				{
					_logger.Error(
						"Temp image upload failed - JobId: {JobId}, Error: {Error}",
						jobId, uploadResult.ErrorMessage);

					return Fail<SubmitJobResponse>(
						"Failed to save image. Please try again");
				}

				// ── Log QuestionJob as Pending ─────────────────────
				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				var job = new QuestionJob
				{
					Id = jobId,
					SchoolId = schoolId,
					SubTopicId = model.SubTopicId,
					TeacherId = userId,
					QuestionId = subTopic.SchoolId,
					// Null until background worker completes
					QuestionType = model.QuestionType,
					HasImages = model.HasImages,
					TempImagePath = uploadResult.PublicId,
					// Cloudinary public_id — worker reads from here
					Status = "Pending",
					AttemptCount = 0,
					CreatedAt = now,
					CompletedAt = "",
					FailureReason = ""
				};

				await _jobCommandRepo.Create(job, DatabaseTarget.QuestionBank);

				_logger.Information(
					"QuestionJob logged - JobId: {JobId}, Status: Pending", jobId);

				return new SubmitJobResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Job submitted successfully",
					Status = "successful",
					JobId = jobId,
					// "Pending" — teacher polls GetJobStatus
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error submitting question job");
				return Error<SubmitJobResponse>();
			}
		}
	}

	// ═══════════════════════════════════════════════════════════
	// STEP 2: GET JOB STATUS (Teacher polls this)
	// ═══════════════════════════════════════════════════════════

	public async Task<JobStatusResponse> GetJobStatus(Guid jobId,AuthenticatedUserClaims userClaims)
	{
		try
		{
			if (!Guid.TryParse(userClaims.UserId, out var userId))
				return Fail<JobStatusResponse>("Invalid user identification");

			if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				return Fail<JobStatusResponse>("Invalid school identification");

			var job = await _jobQueryRepo.Get(jobId, DatabaseTarget.QuestionBank);

			if (job == null)
				return Fail<JobStatusResponse>("Job not found");

			// Cross-tenant check
			if (job.SchoolId != schoolId)
				return Fail<JobStatusResponse>("Job not found");

			// Ownership check — teacher can only see their own jobs
			if (job.TeacherId != userId)
				return new JobStatusResponse
				{
					ResponseCode = ResponseCode.Forbidden,
					ResponseMessage = "You do not have permission to view this job",
					Status = "failed"
				};

			return new JobStatusResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Job status retrieved",
				//Status = "successful",
				JobId = job.Id,
				// Pending | Processing | Completed | Failed
				Status = job.Status,
				// Populated when Completed
				QuestionId = job.QuestionId,
				// Populated when Failed
				FailureReason = job.FailureReason,
				AttemptCount = job.AttemptCount,
				CreatedAt = job.CreatedAt,
				CompletedAt = job.CompletedAt
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error getting job status - JobId: {JobId}", jobId);
			return Error<JobStatusResponse>();
		}
	}

	// ═══════════════════════════════════════════════════════════
	// GET MY JOBS (Teacher sees their upload history)
	// ═══════════════════════════════════════════════════════════

	public async Task<JobListResponse> GetMyJobs(AuthenticatedUserClaims userClaims)
	{
		try
		{
			if (!Guid.TryParse(userClaims.UserId, out var userId))
				return new JobListResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = "Invalid user identification",
					Status = "failed"
				};

			if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				return new JobListResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = "Invalid school identification",
					Status = "failed"
				};

			// Fetch all jobs for this teacher
			// Most recent first
			var query = $@"
                SELECT
                    Id, QuestionType, Status,
                    QuestionId, FailureReason,
                    AttemptCount, CreatedAt, CompletedAt
                FROM QuestionJob
                WHERE TeacherId = '{userId}'
                AND   SchoolId  = '{schoolId}'
                ORDER BY CreatedAt DESC";

			var results = await _jobQueryRepo.GetByQuery(
				query, DatabaseTarget.QuestionBank);

			var jobs = results?.Select(j => new JobSummaryDto
			{
				JobId = j.Id,
				QuestionType = j.QuestionType,
				Status = j.Status,
				QuestionId = j.QuestionId,
				FailureReason = j.FailureReason,
				CreatedAt = j.CreatedAt,
				CompletedAt = j.CompletedAt
			}).ToList() ?? new List<JobSummaryDto>();

			return new JobListResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Jobs retrieved successfully",
				Status = "successful",
				Jobs = jobs,
				TotalCount = jobs.Count,
				PendingCount = jobs.Count(j => j.Status == "Pending" || j.Status == "Processing"),
				CompletedCount = jobs.Count(j => j.Status == "Completed"),
				FailedCount = jobs.Count(j => j.Status == "Failed")
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error getting jobs for teacher");
			return new JobListResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "An error occurred while retrieving jobs",
				Status = "failed"
			};
		}
	}

	// ═══════════════════════════════════════════════════════════
	// RETRY FAILED JOB
	// Resets Status to Pending
	// Background worker picks it up again
	// ═══════════════════════════════════════════════════════════

	public async Task<BaseResponse> RetryJob(Guid jobId,AuthenticatedUserClaims userClaims)
	{
		try
		{
			if (!Guid.TryParse(userClaims.UserId, out var userId))
				return Fail<BaseResponse>("Invalid user identification");

			if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				return Fail<BaseResponse>("Invalid school identification");

			var job = await _jobQueryRepo.Get(jobId, DatabaseTarget.QuestionBank);

			if (job == null || job.SchoolId != schoolId)
				return Fail<BaseResponse>("Job not found");

			if (job.TeacherId != userId)
				return new BaseResponse
				{
					ResponseCode = ResponseCode.Forbidden,
					ResponseMessage = "You do not have permission to retry this job",
					Status = "failed"
				};

			if (job.Status != "Failed")
				return Fail<BaseResponse>(
					$"Only failed jobs can be retried. Current status: {job.Status}");

			// Reset for background worker to pick up again
			var retryDict = new Dictionary<string, object>
			{
				{ "Status",        "Pending" },
				{ "FailureReason", string.Empty }
			};

			await _jobCommandRepo.UpdateTableColumnById(retryDict,new KeyValuePair<string, object>("Id", jobId),DatabaseTarget.QuestionBank);

			_logger.Information("Job reset for retry - JobId: {JobId}", jobId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Job queued for retry",
				Status = "successful"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error retrying job - JobId: {JobId}", jobId);
			return Error<BaseResponse>();
		}
	}

	// ═══════════════════════════════════════════════════════════
	// STEP 3: GET QUESTION PREVIEW
	// Teacher fetches processed question when Status = Completed
	// ═══════════════════════════════════════════════════════════

	public async Task<QuestionPreviewResponse> GetQuestionPreview(Guid jobId,AuthenticatedUserClaims userClaims)
	{
		try
		{
			if (!Guid.TryParse(userClaims.UserId, out var userId))
				return Fail<QuestionPreviewResponse>("Invalid user identification");

			if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				return Fail<QuestionPreviewResponse>("Invalid school identification");

			var job = await _jobQueryRepo.Get(jobId, DatabaseTarget.QuestionBank);

			if (job == null || job.SchoolId != schoolId)
				return Fail<QuestionPreviewResponse>("Job not found");

			if (job.TeacherId != userId)
				return new QuestionPreviewResponse
				{
					ResponseCode = ResponseCode.Forbidden,
					ResponseMessage = "You do not have permission to view this job",
					Status = "failed"
				};

			if (job.Status != "Completed" || job.QuestionId == null)
				return Fail<QuestionPreviewResponse>(
					$"Question not ready yet. Current status: {job.Status}");

			// Fetch the processed question
			var question = await _questionQueryRepo.Get(
				job.QuestionId.Value, DatabaseTarget.QuestionBank);

			if (question == null || question.IsDeleted)
				return Fail<QuestionPreviewResponse>("Question not found");

			// Fetch options if Objective question
			var options = new List<OptionPreviewDto>();

			if (job.QuestionType == "Objective")
			{
				var optionsQuery = $@"
                    SELECT *
                    FROM QuestionOptions
                    WHERE QuestionId = '{question.Id}'
                    AND   IsDeleted  = 0
                    AND   IsActive   = 1
                    ORDER BY OrderIndex ASC";

				var optionResults = await _optionQueryRepo.GetByQuery(optionsQuery, DatabaseTarget.QuestionBank);

				options = optionResults?.Select(o => new OptionPreviewDto
				{
					Id = o.Id,
					OptionLabel = o.OptionLabel,
					OptionText = o.OptionText,
					OptionHtml = o.OptionHtml,
					ContentParts = o.ContentParts,
					IsCorrect = o.IsCorrect,
					HasLatex = o.HasLatex,
					HasImages = o.HasImages,
					OrderIndex = o.OrderIndex
				}).ToList() ?? new List<OptionPreviewDto>();
			}

			return new QuestionPreviewResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Question preview retrieved",
				//Status = "successful",
				QuestionId = question.Id,
				JobId = jobId,
				QuestionType = job.QuestionType,
				QuestionHtml = question.QuestionHtml,
				ContentParts = question.ContentParts,
				Options = options,
				HasLatex = question.HasLatex,
				HasImages = question.HasMedia,
				DifficultyLevel = question.DifficultyLevel.ToString(),
				MarksAllocation = question.MarksAllocation,
				Status = question.Status.ToString()
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error getting question preview - JobId: {JobId}", jobId);
			return Error<QuestionPreviewResponse>();
		}
	}

	/// <summary>
	/// Called by QuestionJobWorker every 30 seconds
	/// Picks up ONE pending job at a time
	/// Calls Claude, saves ALL extracted questions, updates job status
	///
	/// WORKFLOW:
	/// 1. Fetch next Pending job (FIFO order)
	/// 2. Mark as Processing — prevents double pickup
	/// 3. Download image from Cloudinary temp
	/// 4. Send image to Claude — extract ALL questions
	/// 5. Loop through extracted questions:
	///    a. Save Question record
	///    b. Save QuestionOptions if Objective
	/// 6. Update job → Completed + ExtractedCount
	/// 7. Delete temp image from Cloudinary (fire and forget)
	///
	/// ON FAILURE:
	/// - Increment AttemptCount
	/// - If AttemptCount < MaxAttempts → reset to Pending (auto retry)
	/// - If AttemptCount >= MaxAttempts → permanently Failed
	/// </summary>
	public async Task ProcessNextPendingJob()
	{
		QuestionJob? job = null;
		IDbTransactionScope? scope = null;
		try
		{
			// ────────────────────────────────────────────────────
			// STEP 1: Fetch next Pending job
			// FIFO order — oldest job processed first
			// TOP 1 — one job per worker cycle
			// AttemptCount < MaxAttempts — skip permanently failed
			// ────────────────────────────────────────────────────
			var connectionString = _resolver.Resolve(DatabaseTarget.QuestionBank);

			scope = _dbTransactionScopeFactory.Create("QuestionBankConnection");
			var pendingQuery = $@"
            SELECT TOP 1 *
            FROM QuestionJob
            WHERE Status  in ('Pending', 'Processing')
            AND   AttemptCount < {MaxAttempts}
            ORDER BY CreatedAt ASC";

			var pending = await _jobQueryRepo.GetByQuery(pendingQuery, DatabaseTarget.QuestionBank);

			job = pending?.FirstOrDefault();

			if (job == null)
			{
				_logger.Debug("QuestionJobWorker — no pending jobs found");
				return;
			}

			_logger.Information("Processing job - JobId: {JobId}, SubTopicId: {SubTopicId}, " + "QuestionType: {Type}, Attempt: {Attempt}",
				job.Id, job.SubTopicId, job.QuestionType, job.AttemptCount + 1);

			var subTopic = await _subTopicQueryRepo.Get(job.SubTopicId, DatabaseTarget.QuestionBank);

			if (subTopic == null)
				throw new Exception($"SubTopic not found - SubTopicId: {job.SubTopicId}");

			// ────────────────────────────────────────────────────
			// STEP 2: Mark as Processing immediately
			// Prevents another worker instance picking up same job
			// AttemptCount incremented here — tracks total tries
			// ────────────────────────────────────────────────────
			var processingDict = new Dictionary<string, object>
			{
				{ "Status",       "Processing" },
				{ "AttemptCount", job.AttemptCount + 1 }
			};

			await _jobCommandRepo.UpdateTableColumnById(scope.Transaction, scope.Connection,processingDict, new KeyValuePair<string, object>("Id", job.Id),DatabaseTarget.QuestionBank);

			_logger.Information("Job marked as Processing - JobId: {JobId}", job.Id);

			// ────────────────────────────────────────────────────
			// STEP 3: Download image from Cloudinary temp folder
			// TempImagePath is the Cloudinary public_id
			// stored at upload time in SubmitJob()
			// ────────────────────────────────────────────────────
			if (string.IsNullOrWhiteSpace(job.TempImagePath))
				throw new Exception("TempImagePath is missing on job record");

			var imageBytes = await DownloadImageFromCloudinary(job.TempImagePath);

			if (imageBytes == null || imageBytes.Length == 0)
				throw new Exception($"Failed to download temp image from Cloudinary. " + $"PublicId: {job.TempImagePath}");

			_logger.Information("Image downloaded - JobId: {JobId}, Size: {Size} bytes",job.Id, imageBytes.Length);

			// ────────────────────────────────────────────────────
			// STEP 4: Call Claude Vision
			// Sends image + structured prompt
			// Returns all questions extracted from the image
			// ────────────────────────────────────────────────────
			var claudeResult = await CallClaude(imageBytes,job.QuestionType,job.HasImages);

			if (!claudeResult.Success)
				throw new Exception($"Claude processing failed: {claudeResult.ErrorMessage}");

			if (claudeResult.Questions == null || !claudeResult.Questions.Any())
				throw new Exception("Claude returned no questions from the image. " + "Image may be too blurry or unclear.");

			_logger.Information(
				"Claude extracted {Count} question(s) - JobId: {JobId}", claudeResult.Questions.Count, job.Id);

			// ────────────────────────────────────────────────────
			// STEP 5: Save all extracted questions
			// Each question gets its own Question record
			// All inherit the same SubTopicId from the job
			// Options saved per question for Objective type
			// ────────────────────────────────────────────────────
			var savedQuestionIds = new List<Guid>();
			var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
			
			foreach (var extracted in claudeResult.Questions)
			{
				// ── 5a: Save Question record ──────────────────
				var questionId = Guid.NewGuid();

				var question = new Questions
				{
					// ── Identity ──────────────────────────────────
					Id = questionId,
					SchoolId = job.SchoolId,
					SubjectId = Guid.Empty,
					TopicId = subTopic.TopicId,
					SubTopicId = job.SubTopicId,
					CreatedBy = job.TeacherId,

					// ── Legacy string fields (kept for compatibility) ──
					Topic = string.Empty,
					SubTopic = string.Empty,

					// ── Content — plain text (not used in AI pipeline) ──
					Title = string.Empty,
					TextContent = string.Empty,

					// ── Content — AI generated ────────────────────
					QuestionType = ParseQuestionType(job.QuestionType),
					QuestionHtml = extracted.QuestionHtml ?? string.Empty,
					ContentParts = extracted.ContentPartsJson ?? string.Empty,
					HasLatex = extracted.HasLatex,
					DifficultyLevel = DifficultyLevel.Medium,
					MarksAllocation = 1,

					// ── Answer (TrueFalse only) ───────────────────
					CorrectAnswer = job.QuestionType == "TrueFalse"? extracted.CorrectAnswer ?? string.Empty: string.Empty,
					// ── Job link ──────────────────────────────────
					JobId = job.Id,

					// ── Board session (not used in job pipeline) ──
					BoardSessionId = null,
					HasBoardSession = false,
					HasMedia = extracted.HasImages,
					HasAudio = false,
					SnapshotUrl = string.Empty,
					SnapshotPublicId = null,

					// ── Scan pipeline (not used in job pipeline) ──
					ScanSessionId = null,
					IsScanned = false,
					ExtractedQuestionIndex = null,
					AIConfidenceScore = null,
					//OriginalFileUrl = string.Empty,
					//OriginalFileName = string.Empty,

					// ── Status ────────────────────────────────────
					Status = QuestionStatus.Draft,
					IsActive = true,
					IsDeleted = false,

					// ── Review / Publish (not yet actioned) ───────
					ReviewedDate = null,
					ReviewedBy = null,
					PublishedDate = null,
					PublishedBy = null,

					// ── Sync (not applicable for AI pipeline) ─────
					ClientId = null,
					OriginDevice = null,
					LastSyncedAt = null,

					// ── Soft delete fields (not deleted yet) ──────
					DeletedDate = null,
					DeletedBy = null,

					// ── Audit ─────────────────────────────────────
					CreationDate = now,
					ModifiedDate = now
				};
				
				await _questionCommandRepo.Create(scope.Transaction, scope.Connection,question, DatabaseTarget.QuestionBank);

				_logger.Information("Question saved - QuestionId: {QuestionId}, " + "JobId: {JobId}, HasLatex: {HasLatex}, HasImages: {HasImages}",questionId, job.Id, extracted.HasLatex, extracted.HasImages);

				// ── 5b: Save options for Objective questions ──
				if (job.QuestionType == "Objective" && extracted.Options?.Any() == true)
				{
					var optionCount = 0;

					foreach (var opt in extracted.Options)
					{
						// Skip empty options — Claude occasionally
						// returns empty option shells
						if (string.IsNullOrWhiteSpace(opt.PlainText) && string.IsNullOrWhiteSpace(opt.Html))
						{
							_logger.Warning("Skipping empty option - QuestionId: {QuestionId}, " + "Label: {Label}", questionId, opt.Label);
							continue;
						}

						var option = new QuestionOptions
						{
							Id = Guid.NewGuid(),
							QuestionId = questionId,
							OptionLabel = opt.Label,
							// A | B | C | D | E etc

							OptionText = opt.PlainText,
							// Plain text version — used for simple display
							// and search indexing

							OptionHtml = opt.Html,
							// Claude-generated HTML — used for rich rendering
							// may contain LaTeX spans or image placeholders

							ContentParts = opt.ContentPartsJson,
							// Structured JSON — used for editing

							IsCorrect = opt.IsCorrect,
							HasLatex = opt.HasLatex,
							HasImages = opt.HasImages,
							OrderIndex = opt.OrderIndex,
							IsActive = true,
							IsDeleted = false,
							CreationDate = now
						};

						await _optionCommandRepo.Create(scope.Transaction, scope.Connection,option, DatabaseTarget.QuestionBank);

						optionCount++;
					}

					_logger.Information("Options saved - QuestionId: {QuestionId}, " + "Count: {Count}", questionId, optionCount);
				}

				savedQuestionIds.Add(questionId);
			}

			_logger.Information(
				"All questions saved - JobId: {JobId}, " + "TotalExtracted: {Total}", job.Id, savedQuestionIds.Count);

			// ────────────────────────────────────────────────────
			// STEP 6: Update job → Completed
			// ExtractedCount tells teacher how many questions
			// were found and saved from this image
			// Note: No single QuestionId stored —
			// teacher fetches questions by JobId
			// ────────────────────────────────────────────────────
			var completedDict = new Dictionary<string, object>
			{
				{ "Status",         "Completed" },
				{ "ExtractedCount", savedQuestionIds.Count },
				{ "CompletedAt",    now }
			};

			await _jobCommandRepo.UpdateTableColumnById(scope.Transaction, scope.Connection,completedDict, new KeyValuePair<string, object>("Id", job.Id),DatabaseTarget.QuestionBank);
			await scope.CommitAsync();
			_logger.Information(
				"Job completed - JobId: {JobId}, " +
				"ExtractedCount: {Count}",
				job.Id, savedQuestionIds.Count);

			// ────────────────────────────────────────────────────
			// STEP 7: Delete temp image from Cloudinary
			// Fire and forget — job already marked Completed
			// Teacher is unaffected if cleanup fails
			// Cloudinary auto-delete policy is the safety net
			// ────────────────────────────────────────────────────
			_ = Task.Run(async () =>
			{
				try
				{
					await _cloudinaryService.DeleteMediaAsync(job.TempImagePath, MediaType.Image);

					_logger.Information("Temp image deleted - PublicId: {PublicId}", job.TempImagePath);
				}
				catch (Exception ex)
				{
					_logger.Warning(ex,"Temp image deletion failed - PublicId: {PublicId}. " + "Cloudinary auto-delete policy will handle cleanup",
						job.TempImagePath);
				}
			});
		}
		catch (Exception ex)
		{
			// ────────────────────────────────────────────────────
			// FAILURE HANDLING
			// Under max attempts → reset to Pending for auto retry
			// At max attempts → permanently Failed
			// Teacher sees FailureReason and can retry manually
			// ────────────────────────────────────────────────────
			//try { scope.RollbackAsync(); } catch { }

			_logger.Error(ex,
				"Job processing failed - JobId: {JobId}",
				job?.Id);

			if (job == null) return;

			var newAttemptCount = job.AttemptCount + 1;
			var permanentlyFailed = newAttemptCount >= MaxAttempts;

			var failDict = new Dictionary<string, object>
			{
				{
					"Status",
					permanentlyFailed ? "Failed" : "Pending"
					// Under max → back to Pending, worker retries next cycle
					// At max → permanently Failed, teacher must retry manually
				},
				{
					"FailureReason",
					permanentlyFailed
						? $"Failed after {MaxAttempts} attempts. " +
						  $"Last error: {ex.Message}"
						: string.Empty
					// Only set reason on permanent failure
					// Transient failures do not surface to teacher
				},
				{ "AttemptCount", newAttemptCount }
			};

			await _jobCommandRepo.UpdateTableColumnById(failDict,new KeyValuePair<string, object>("Id", job.Id),DatabaseTarget.QuestionBank);

			_logger.Warning("Job {Status} - JobId: {JobId}, " +"Attempts: {Attempts}/{Max}",permanentlyFailed ? "permanently failed" : "reset for retry",job.Id,newAttemptCount,MaxAttempts);
		}
		finally
		{
			scope?.Dispose();
		}
	}

	// ═══════════════════════════════════════════════════════════
	// PRIVATE: CALL CLAUDE
	// Sends image + prompt, parses structured response
	// ═══════════════════════════════════════════════════════════

	private async Task<ClaudeProcessingResult> CallClaude(byte[] imageBytes,string questionType,bool hasImages)
	{
		try
		{
			var apiKey = _configuration["Anthropic:ApiKey"];
			var httpClient = new System.Net.Http.HttpClient
			{
				Timeout = TimeSpan.FromMinutes(5)
			};
			var client = new AnthropicClient(apiKey, httpClient);

			var base64Image = Convert.ToBase64String(imageBytes);

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
								MediaType = "image/jpeg",
								Data      = base64Image
							}
						},
						new TextContent
						{
							Text = BuildProcessingPrompt(questionType, hasImages)
						}
					}
				}
			};

			var parameters = new MessageParameters
			{
				Model = AnthropicModels.Claude4Sonnet,
				MaxTokens = 8192,
				Messages = messages,
				System = new List<SystemMessage>
				{
					new SystemMessage(BuildSystemPrompt())
				}
			};

			var response = await client.Messages.GetClaudeMessageAsync(parameters);

			var rawJson = response.Content ?.OfType<TextContent>().FirstOrDefault()?.Text ?? string.Empty;

			// Strip markdown code fences if Claude wrapped in them
			rawJson = rawJson.Replace("```json", "").Replace("```", "").Trim();

			return ParseClaudeResponse(rawJson, questionType);
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Claude API call failed");
			return new ClaudeProcessingResult
			{
				Success = false,
				ErrorMessage = $"Claude API error: {ex.Message}"
			};
		}
	}

	// ═══════════════════════════════════════════════════════════
	// PRIVATE: SYSTEM PROMPT
	// Strict rules Claude must follow for every response
	// ═══════════════════════════════════════════════════════════

	private string BuildSystemPrompt() => @"
		You are an expert educational content processor for an African EdTech platform.
		You process images of exam questions and return structured data.
 
		CRITICAL RULES — follow exactly:
		- Return ONLY valid JSON. No preamble, explanation or markdown.
		- Never guess content — only extract what is clearly visible.
		- For LaTeX: always use standard KaTeX-compatible syntax.
		- For HTML: use ONLY these CSS classes:
			th-question   : question body wrapper
			th-block      : block element (own line, left aligned)
			th-center     : block element (own line, centered)
			th-inline     : inline element (flows with text)
			th-row        : flex row container
			th-col        : flex column inside th-row
			th-img        : image element
			th-math-block : block math equation
			th-math-inline: inline math equation
		- For math: wrap inline equations in \( \), block equations in \[ \]
		- For images: use placeholder tokens {{image:DESCRIPTION}} — never fake URLs
		- CSS classes must be in class attribute, no inline styles allowed.
		- Separate question content from answer content always.";

	// ═══════════════════════════════════════════════════════════
	// PRIVATE: USER PROMPT (varies by question type)
	// ═══════════════════════════════════════════════════════════

	private string BuildProcessingPrompt(string questionType, bool hasImages)
	{
		var typeInstruction = questionType switch
		{
			"Objective" => @"
QUESTION TYPE: Multiple Choice
- Extract the question body and ALL answer options (A B C D E etc)
- Identify correct answer only if clearly marked in image
- If correct answer not marked — set isCorrect false for all options",

			"TrueFalse" => @"
QUESTION TYPE: True/False
- Extract only the question body
- Set correctAnswer to 'True' or 'False' ONLY if clearly visible
- If not visible — set correctAnswer to null",

			"Theory" => @"
QUESTION TYPE: Theory/Essay
- Extract only the question body
- options array must be empty []",

			_ => string.Empty
		};

		// ── Image instruction — strong and at the top ──────────
		var imageInstruction = hasImages
	? @"
!!MANDATORY IMAGE RULE — READ THIS FIRST!!

This question contains diagrams, graphs or figures — in BOTH the question body AND possibly in the answer options.

QUESTION BODY IMAGES:
For every diagram or figure in the question text — place a placeholder token exactly where it appears:
  {{image:snake_case_description}}
Examples:
  {{image:velocity_time_graph}}
  {{image:circuit_diagram}}
  {{image:rectangular_block_water}}

OPTION IMAGES:
Some answer options may themselves BE images (a graph, a diagram, a shape).
For each option that is an image:
- Set the option html to: <div class='th-option'>{{image:option_a_description}}</div>
- Set plainText to a brief description: 'Graph showing increasing velocity'
- Set hasImages to true for that option
- Use a unique description per option

Examples of image options:
  Option A is a velocity-time graph:
    html: <div class='th-option'>{{image:option_a_velocity_graph}}</div>
    plainText: Graph showing constant velocity
    hasImages: true

  Option B is a displacement diagram:
    html: <div class='th-option'>{{image:option_b_displacement_diagram}}</div>
    plainText: Diagram showing displacement
    hasImages: true

NEVER skip a diagram in EITHER the question body or the options.
If unclear — still place the token with your best description.
hasImages MUST be true in your response when images are present."
	: @"
No diagrams in this question — text and math only.
hasImages must be false in your response.
All option hasImages must be false.";

		return $@"
{imageInstruction}

{typeInstruction}

Extract ALL exam questions visible in this image.
Do NOT add titles, headings or question numbers.

Return JSON in exactly this structure — no preamble, no markdown:
{{
  ""questions"": [
    {{
      ""questionHtml"": ""<div class='th-question'>...</div>"",
      ""contentParts"": [
        {{""type"": ""text"",  ""value"": ""..."", ""display"": ""inline""}},
        {{""type"": ""latex"", ""value"": ""..."", ""display"": ""block""}},
        {{""type"": ""image"", ""value"": ""{{{{image:description}}}}"", ""display"": ""block""}}
      ],
      ""hasLatex"": false,
      ""hasImages"": false,
      ""correctAnswer"": null,
      ""options"": [
        {{
          ""label"": ""A"",
          ""html"": ""<div class='th-option'>...</div>"",
          ""contentParts"": [],
          ""plainText"": ""..."",
          ""isCorrect"": false,
          ""hasLatex"": false,
          ""hasImages"": false,
          ""orderIndex"": 0
        }}
      ]
    }}
  ],
  ""totalExtracted"": 1
}}

STRICT RULES:
- Return an array of questions — even if only one question found
- questionHtml uses ONLY these classes: th-question th-block th-center th-inline th-row th-col th-img th-math-block th-math-inline th-option
- Inline math wrapped in \( \) — block math wrapped in \[ \]
- No inline styles. No other CSS classes. No markdown.
- options array is empty [] for Theory and TrueFalse questions.
- hasLatex true if ANY part of that question contains math.
- hasImages true if ANY part contains an image placeholder token.
- correctAnswer null unless answer is explicitly marked in the image.";
	}
	// ═══════════════════════════════════════════════════════════
	// PRIVATE: PARSE CLAUDE RESPONSE
	// ═══════════════════════════════════════════════════════════

	private ClaudeProcessingResult ParseClaudeResponse(string rawJson, string questionType)
	{
		try
		{
			using var doc = JsonDocument.Parse(rawJson);
			var root = doc.RootElement;

			var result = new ClaudeProcessingResult { Success = true };

			if (!root.TryGetProperty("questions", out var questionsEl)
				|| questionsEl.ValueKind != JsonValueKind.Array)
			{
				return new ClaudeProcessingResult
				{
					Success = false,
					ErrorMessage = "Claude response missing questions array"
				};
			}

			foreach (var qEl in questionsEl.EnumerateArray())
			{
				var extracted = new ExtractedQuestion
				{
					QuestionHtml = GetString(qEl, "questionHtml"),
					ContentPartsJson = GetString(qEl, "contentParts"),
					HasLatex = GetBool(qEl, "hasLatex"),
					HasImages = GetBool(qEl, "hasImages"),
					CorrectAnswer = GetString(qEl, "correctAnswer"),
					Options = new List<ParsedOption>()
				};

				if (questionType == "Objective"
					&& qEl.TryGetProperty("options", out var optsEl)
					&& optsEl.ValueKind == JsonValueKind.Array)
				{
					var orderIndex = 0;
					foreach (var opt in optsEl.EnumerateArray())
					{
						extracted.Options.Add(new ParsedOption
						{
							Label = GetString(opt, "label"),
							Html = GetString(opt, "html"),
							ContentPartsJson = GetString(opt, "contentParts"),
							PlainText = GetString(opt, "plainText"),
							IsCorrect = GetBool(opt, "isCorrect"),
							HasLatex = GetBool(opt, "hasLatex"),
							HasImages = GetBool(opt, "hasImages"),
							OrderIndex = orderIndex++
						});
					}
				}

				result.Questions.Add(extracted);
			}

			_logger.Information("Claude extracted {Count} questions", result.Questions.Count);

			return result;
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Failed to parse Claude response");
			return new ClaudeProcessingResult
			{
				Success = false,
				ErrorMessage = $"Failed to parse Claude response: {ex.Message}"
			};
		}
	}
	// ═══════════════════════════════════════════════════════════
	// PRIVATE: DOWNLOAD IMAGE FROM CLOUDINARY
	// ═══════════════════════════════════════════════════════════

	private async Task<byte[]?> DownloadImageFromCloudinary(string? publicId)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(publicId))
				return null;

			// Build URL directly from publicId — no reconstruction
			var url = _cloudinaryService.GetRawUrl(publicId);

			_logger.Information(
				"Downloading temp image - PublicId: {PublicId}, URL: {Url}",
				publicId, url);

			using var httpClient = new System.Net.Http.HttpClient();
			httpClient.Timeout = TimeSpan.FromSeconds(30);

			var response = await httpClient.GetAsync(url);

			if (!response.IsSuccessStatusCode)
			{
				_logger.Error(
					"Image download failed - Status: {Status}, URL: {Url}",
					response.StatusCode, url);
				return null;
			}

			var bytes = await response.Content.ReadAsByteArrayAsync();

			_logger.Information(
				"Image downloaded successfully - Size: {Size} bytes", bytes.Length);

			return bytes;
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Failed to download image - PublicId: {PublicId}", publicId);
			return null;
		}
	}

	// ═══════════════════════════════════════════════════════════
	// PRIVATE: HELPERS
	// ═══════════════════════════════════════════════════════════

	private QuestionType ParseQuestionType(string type) => type switch
	{
		"Objective" => QuestionType.MultipleChoice,
		"TrueFalse" => QuestionType.TrueOrFalse,
		_ => QuestionType.ShortAnswer
	};

	private string GetString(JsonElement el, string key)
	{
		if (el.TryGetProperty(key, out var prop)
			&& prop.ValueKind != JsonValueKind.Null)
			return prop.ValueKind == JsonValueKind.Array
				? prop.GetRawText()
				: prop.GetString() ?? string.Empty;
		return string.Empty;
	}

	private bool GetBool(JsonElement el, string key)
	{
		if (el.TryGetProperty(key, out var prop)
			&& prop.ValueKind == JsonValueKind.True)
			return true;
		return false;
	}

	private T Fail<T>(string message) where T : BaseResponse, new()
		=> new T
		{
			ResponseCode = ResponseCode.BadRequest,
			ResponseMessage = message,
			Status = "failed"
		};

	private T Error<T>() where T : BaseResponse, new()
		=> new T
		{
			ResponseCode = ResponseCode.ErrorOccured,
			ResponseMessage = "An error occurred. Please try again",
			Status = "failed"
		};
}

// ═══════════════════════════════════════════════════════════
// INTERNAL RESULT MODELS
// Used only within QuestionJobService
// Not exposed via API
// ═══════════════════════════════════════════════════════════

internal class ClaudeProcessingResult
{
	public bool Success { get; set; }
	public string ErrorMessage { get; set; } = string.Empty;

	// Now a list — one entry per extracted question
	public List<ExtractedQuestion> Questions { get; set; } = new();
}

internal class ExtractedQuestion
{
	public string QuestionHtml { get; set; } = string.Empty;
	public string ContentPartsJson { get; set; } = string.Empty;
	public bool HasLatex { get; set; }
	public bool HasImages { get; set; }
	public string? CorrectAnswer { get; set; }
	public List<ParsedOption> Options { get; set; } = new();
}

internal class ParsedOption
{
	public string Label { get; set; } = string.Empty;
	public string Html { get; set; } = string.Empty;
	public string ContentPartsJson { get; set; } = string.Empty;
	public string PlainText { get; set; } = string.Empty;
	public bool IsCorrect { get; set; }
	public bool HasLatex { get; set; }
	public bool HasImages { get; set; }
	public int OrderIndex { get; set; }
}

