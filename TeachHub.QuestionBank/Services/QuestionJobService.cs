using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.DTO;
using TechHub.QuestionBank.Core.Entities;
using TechHub.QuestionBank.Core.Enums;
using TechHub.QuestionBank.Core.Model;
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
	private readonly ICommandRespository<QuestionImage> _questionImageCommandRepo;

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
	    ICommandRespository<QuestionImage> questionImageCommandRepo,
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
		_questionCommandRepo = questionCommandRepo;

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
					ClassroomId = model.ClassroomId,   
					SubjectId = model.SubjectId,     
					SubTopicId = model.SubTopicId,
					TeacherId = userId,
					QuestionId = subTopic.SchoolId,   /// lokt this later ... it should be null , guess nulable ain't working atm
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


	public async Task<BaseResponse> GetJobStatuses(Guid classroomId,Guid subjectId,Guid? topicId,Guid? subTopicId,AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
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
				var whereClause = $@"
					WHERE  j.SchoolId    = '{schoolId}'
					AND    j.ClassroomId = '{classroomId}'
					AND    j.SubjectId   = '{subjectId}'
					AND    j.TeacherId   = '{userId}'";

				if (topicId.HasValue && topicId != Guid.Empty)
					whereClause += $" AND st.TopicId = '{topicId.Value}'";

				if (subTopicId.HasValue && subTopicId != Guid.Empty)
					whereClause += $" AND j.SubTopicId = '{subTopicId.Value}'";

				var query = $@"
					SELECT
						j.Id              AS JobId,
						j.Status,
						j.QuestionType,
						j.ExtractedCount,
						j.FailureReason,
						j.AttemptCount,
						j.CreatedAt,
						j.CompletedAt,
						st.Name           AS SubTopicName,
						st.TopicId        AS TopicId
					FROM   QuestionJob   j
					LEFT JOIN SubTopic   st ON st.Id = j.SubTopicId
					{whereClause}
					ORDER  BY j.CreatedAt DESC";

				var rows = await _jobQueryRepo.QueryAsync<JobStatusRow>(query, new Dictionary<string, object>(),DatabaseTarget.QuestionBank);

				var list = rows?.ToList() ?? new();

				// Summary counts
				var summary = new
				{
					Total = list.Count,
					Pending = list.Count(j => j.Status == "Pending"),
					Processing = list.Count(j => j.Status == "Processing"),
					Completed = list.Count(j => j.Status == "Completed"),
					Failed = list.Count(j => j.Status == "Failed")
				};

				_logger.Information(
					"Job statuses retrieved - ClassroomId: {ClassroomId}, " +
					"SubjectId: {SubjectId}, Count: {Count}",
					classroomId, subjectId, list.Count);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = list.Any()
						? $"{list.Count} job(s) found"
						: "No jobs found",
					Status = "successful",
					Data = new
					{
						Summary = summary,
						Jobs = list
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error retrieving job statuses");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving job statuses",
					Status = "failed"
				};
			}
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
		try
		{
			// ── STEP 1: Fetch next pending job — no transaction needed ───
			var pendingQuery = $@"
				SELECT TOP 1 *
				FROM   QuestionJob
				WHERE  Status IN ('Pending', 'Processing')
				AND    AttemptCount < {MaxAttempts}
				ORDER  BY CreatedAt ASC";

			var pending = await _jobQueryRepo.GetByQuery(pendingQuery, DatabaseTarget.QuestionBank);

			job = pending?.FirstOrDefault();

			if (job == null)
			{
				_logger.Debug("QuestionJobWorker — no pending jobs found");
				return;
			}

			_logger.Information(
				"Processing job - JobId: {JobId}, SubTopicId: {SubTopicId}, " +
				"QuestionType: {Type}, Attempt: {Attempt}",
				job.Id, job.SubTopicId, job.QuestionType, job.AttemptCount + 1);

			// ── STEP 2: Mark as Processing — own transaction ─────────────
			using (var markScope = _dbTransactionScopeFactory.Create("QuestionBankConnection"))
			{
				var processingDict = new Dictionary<string, object>
				{
					{ "Status",       "Processing"       },
					{ "AttemptCount", job.AttemptCount + 1 }
				};

				await _jobCommandRepo.UpdateTableColumnById(markScope.Transaction, markScope.Connection,processingDict,new KeyValuePair<string, object>("Id", job.Id),DatabaseTarget.QuestionBank);

				await markScope.CommitAsync();
			}

			_logger.Information(
				"Job marked as Processing - JobId: {JobId}", job.Id);

			// ── STEP 3: Validate subtopic ────────────────────────────────
			var subTopic = await _subTopicQueryRepo.Get(job.SubTopicId, DatabaseTarget.QuestionBank);

			if (subTopic == null)
				throw new Exception($"SubTopic not found - SubTopicId: {job.SubTopicId}");

			// ── STEP 4: Download image from Cloudinary ───────────────────
			if (string.IsNullOrWhiteSpace(job.TempImagePath))
				throw new Exception("TempImagePath is missing on job record");

			var imageBytes = await DownloadImageFromCloudinary(job.TempImagePath);

			if (imageBytes == null || imageBytes.Length == 0)
				throw new Exception($"Failed to download temp image. PublicId: {job.TempImagePath}");

			_logger.Information("Image downloaded - JobId: {JobId}, Size: {Size} bytes",
				job.Id, imageBytes.Length);

			// ── STEP 5: Call Claude Vision ───────────────────────────────
			var claudeResult = await CallClaude(imageBytes, job.QuestionType, job.HasImages);

			if (!claudeResult.Success)
				throw new Exception($"Claude processing failed: {claudeResult.ErrorMessage}");

			if (claudeResult.Questions == null || !claudeResult.Questions.Any())
				throw new Exception(
					"Claude returned no questions. Image may be too blurry or unclear.");

			_logger.Information(
				"Claude extracted {Count} question(s) - JobId: {JobId}",
				claudeResult.Questions.Count, job.Id);

			// ── STEP 5b: Crop and upload diagram images ──────────────────
			// Build map: { "circuit_diagram" → "https://cloudinary.com/..." }
			var imageUrlMap = new Dictionary<string, string>();

			if (claudeResult.ImageBounds?.Any() == true)
			{
				_logger.Information(
					"Processing {Count} image bounds - JobId: {JobId}",
					claudeResult.ImageBounds.Count, job.Id);

				imageUrlMap = await ProcessImageBounds(imageBytes,claudeResult.ImageBounds,job.Id,job.SchoolId);

				_logger.Information(
					"Image processing complete - JobId: {JobId}, " + "Uploaded: {Uploaded}/{Total}",job.Id, imageUrlMap.Count,claudeResult.ImageBounds.Count);

				// Replace placeholders in all questions before saving
				foreach (var extracted in claudeResult.Questions)
				{
					extracted.QuestionHtml = ReplacePlaceholdersInHtml(extracted.QuestionHtml, imageUrlMap);

					extracted.ContentPartsJson = ReplacePlaceholders(extracted.ContentPartsJson, imageUrlMap);

					if (extracted.Options?.Any() == true)
					{
						foreach (var opt in extracted.Options)
						{
							opt.Html = ReplacePlaceholdersInHtml(opt.Html, imageUrlMap);

							opt.ContentPartsJson = ReplacePlaceholders(opt.ContentPartsJson, imageUrlMap);
						}
					}
				}
			}

			// ── STEP 6: Save questions + options + images + complete ─────
			var savedQuestionIds = new List<Guid>();
			var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

			using (var saveScope = _dbTransactionScopeFactory.Create("QuestionBankConnection"))
			{
				try
				{
					foreach (var extracted in claudeResult.Questions)
					{
						var questionId = Guid.NewGuid();

						// ── 6a: Save question record ──────────────────────
						var question = new Questions
						{
							Id = questionId,
							SchoolId = job.SchoolId,
							ClassroomId = job.ClassroomId,
							SubjectId = job.SubjectId,
							TopicId = subTopic.TopicId,
							SubTopicId = job.SubTopicId,
							CreatedBy = job.TeacherId,
							Topic = string.Empty,
							Title = string.Empty,
							TextContent = string.Empty,
							QuestionType = ParseQuestionType(job.QuestionType),
							QuestionHtml = extracted.QuestionHtml
												?? string.Empty,
							ContentParts = extracted.ContentPartsJson
												?? string.Empty,
							HasLatex = extracted.HasLatex,
							DifficultyLevel = DifficultyLevel.Medium,
							MarksAllocation = 1,
							CorrectAnswer = job.QuestionType == "TrueFalse"
												? extracted.CorrectAnswer
												  ?? string.Empty
												: string.Empty,
							JobId = job.Id,
							HasBoardSession = false,
							HasMedia = extracted.HasImages,
							HasAudio = false,
							SnapshotUrl = string.Empty,
							SnapshotPublicId = null,
							IsScanned = false,
							Status = QuestionStatus.Draft,
							IsActive = true,
							IsDeleted = false,
							CreationDate = now,
							ModifiedDate = now,
							QuestionNumber = extracted.QuestionNumber, 
							IsPartial = extracted.IsPartial
						};

						await _questionCommandRepo.Create(saveScope.Transaction, saveScope.Connection, question, DatabaseTarget.QuestionBank);

						_logger.Information(
							"Question saved - QuestionId: {QuestionId}, " +
							"JobId: {JobId}, HasLatex: {HasLatex}, " +
							"HasImages: {HasImages}",
							questionId, job.Id,
							extracted.HasLatex, extracted.HasImages);

						// ── 6b: Save diagram image records ────────────────
						if (extracted.HasImages && imageUrlMap.Any())
						{
							// Extract placeholder keys from original HTML
							// before replacement happened — so we know which
							// images belong to this specific question
							var placeholderKeys =
								ExtractPlaceholderKeysFromOriginal(
									extracted.QuestionHtml ?? string.Empty,
									extracted.ContentPartsJson ?? string.Empty,
									extracted.Options);

							var displayOrder = 0;

							foreach (var key in placeholderKeys)
							{
								if (!imageUrlMap.TryGetValue(
									key, out var imageUrl))
									continue;

								var questionImage = new QuestionImage
								{
									Id = Guid.NewGuid(),
									QuestionId = questionId,
									JobId = job.Id,
									SchoolId = job.SchoolId,
									Label = key,
									CloudinaryUrl = imageUrl,
									PublicId = $"qimg_{job.Id}_{key}",
									DisplayOrder = displayOrder++,
									CreatedAt = now
								};

								await _questionImageCommandRepo.Create(
									saveScope.Transaction, saveScope.Connection,
									questionImage, DatabaseTarget.QuestionBank);

								_logger.Information(
									"Question image saved - QuestionId: {QuestionId}, " +
									"Key: {Key}, Url: {Url}",
									questionId, key, imageUrl);
							}
						}

						// ── 6c: Save options for Objective questions ───────
						if (job.QuestionType == "Objective" &&
							extracted.Options?.Any() == true)
						{
							var optionCount = 0;

							foreach (var opt in extracted.Options)
							{
								if (string.IsNullOrWhiteSpace(opt.PlainText) &&
									string.IsNullOrWhiteSpace(opt.Html))
								{
									_logger.Warning(
										"Skipping empty option - " +
										"QuestionId: {QuestionId}, Label: {Label}",
										questionId, opt.Label);
									continue;
								}

								var option = new QuestionOptions
								{
									Id = Guid.NewGuid(),
									QuestionId = questionId,
									OptionLabel = opt.Label,
									OptionText = opt.PlainText,
									OptionHtml = opt.Html,
									ContentParts = opt.ContentPartsJson,
									IsCorrect = opt.IsCorrect,
									HasLatex = opt.HasLatex,
									HasImages = opt.HasImages,
									OrderIndex = opt.OrderIndex,
									IsActive = true,
									IsDeleted = false,
									CreationDate = now
								};

								await _optionCommandRepo.Create(
									saveScope.Transaction, saveScope.Connection,
									option, DatabaseTarget.QuestionBank);

								optionCount++;
							}

							_logger.Information(
								"Options saved - QuestionId: {QuestionId}, " +
								"Count: {Count}",
								questionId, optionCount);
						}

						savedQuestionIds.Add(questionId);
					}

					// ── 6d: Mark job completed ────────────────────────────
					var completedDict = new Dictionary<string, object>
					{
						{ "Status",         "Completed"            },
						{ "ExtractedCount", savedQuestionIds.Count },
						{ "CompletedAt",    now                    }
					};

					await _jobCommandRepo.UpdateTableColumnById(
						saveScope.Transaction, saveScope.Connection,
						completedDict,
						new KeyValuePair<string, object>("Id", job.Id),
						DatabaseTarget.QuestionBank);

					await saveScope.CommitAsync();

					_logger.Information(
						"Job completed - JobId: {JobId}, ExtractedCount: {Count}",
						job.Id, savedQuestionIds.Count);
				}
				catch
				{
					try { await saveScope.RollbackAsync(); } catch { }
					throw;
				}
			}

			// ── STEP 7: Delete temp image — fire and forget ───────────────
			_ = Task.Run(async () =>
			{
				try
				{
					await _cloudinaryService.DeleteMediaAsync(
						job.TempImagePath, MediaType.Image);

					_logger.Information(
						"Temp image deleted - PublicId: {PublicId}",
						job.TempImagePath);
				}
				catch (Exception ex)
				{
					_logger.Warning(ex,
						"Temp image deletion failed - PublicId: {PublicId}. " +
						"Cloudinary auto-delete policy will handle cleanup",
						job.TempImagePath);
				}
			});
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Job processing failed - JobId: {JobId}", job?.Id);

			if (job == null) return;

			var newAttemptCount = job.AttemptCount + 1;
			var permanentlyFailed = newAttemptCount >= MaxAttempts;

			var failDict = new Dictionary<string, object>
			{
				{
					"Status",
					permanentlyFailed ? "Failed" : "Pending"
				},
				{
					"FailureReason",
					permanentlyFailed
						? $"Failed after {MaxAttempts} attempts. " +
						  $"Last error: {ex.Message}"
						: string.Empty
				},
				{ "AttemptCount", newAttemptCount }
			};

			// Failure update — no transaction, direct write
			await _jobCommandRepo.UpdateTableColumnById(
				failDict,
				new KeyValuePair<string, object>("Id", job.Id),
				DatabaseTarget.QuestionBank);

			_logger.Warning(
				"Job {Status} - JobId: {JobId}, Attempts: {Attempts}/{Max}",
				permanentlyFailed ? "permanently failed" : "reset for retry",
				job.Id, newAttemptCount, MaxAttempts);
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
- Options may appear on the next line or in the right column
- Identify correct answer only if clearly marked
- If not marked — set isCorrect false for all options",

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

		return $@"
!!IMAGE DETECTION — ALWAYS ACTIVE!!

Always scan the ENTIRE image for diagrams, graphs, figures or tables.
Do not rely on any prior indication of whether images are present.

If you find ANY visual content:
1. Set hasImages = true on that question
2. Place placeholder at exact position: {{{{image:snake_case_description}}}}
3. Record bounding box as % of full image:
   {{ ""key"": ""description"", ""x"": 5, ""y"": 35, ""width"": 40, ""height"": 20 }}

For option images:
- html: <div class='th-option'>{{{{image:option_a_description}}}}</div>
- plainText: brief text description
- hasImages: true
- Record bounding box in imageBounds

BOUNDING BOX RULES:
- All values are PERCENTAGES of the full image (0-100)
- x + width must not exceed 100
- y + height must not exceed 100
- Every {{{{image:key}}}} MUST have a matching imageBounds entry
- imageBounds always present — empty [] if truly no images

!!MATH DETECTION — ALWAYS ACTIVE!!

Scan every question for mathematical content.
Wrap ALL math in LaTeX delimiters — never leave as plain text.

Always wrap in LaTeX:
  10^14           → \(10^{{14}}\)
  ms^-1           → \(\text{{ms}}^{{-1}}\)
  4/3 as ratio    → \(\frac{{4}}{{3}}\)
  5.0 x 10^14     → \(5.0 \times 10^{{14}}\)
  Ω               → \(\Omega\)
  E = V + IR      → \(E = V + IR\)
  √2              → \(\sqrt{{2}}\)
  cm²             → \(\text{{cm}}^2\)
  H₂O             → \(\text{{H}}_2\text{{O}}\)
  θ, π, μ, Δ      → \(\theta\), \(\pi\), \(\mu\), \(\Delta\)

hasLatex MUST be true if ANY math exists — even one superscript.

{typeInstruction}

COLUMN LAYOUT RULES:
- Page may have TWO COLUMNS — process each top to bottom independently
- Never mix left and right column content
- Question numbers mark boundaries (35. 36. 37. etc)
- Options always belong to nearest question above them
- If question is cut off — prefix questionHtml with [PARTIAL]

Extract ALL questions visible in this image.

Return JSON — no preamble, no markdown:
{{
  ""questions"": [
    {{
      ""questionNumber"":  35,
      ""questionHtml"":    ""<div class='th-question'>...</div>"",
      ""contentParts"": [
        {{""type"": ""text"",  ""value"": ""..."",                       ""display"": ""inline""}},
        {{""type"": ""latex"", ""value"": ""5.0 \\times 10^{{14}}"",     ""display"": ""inline""}},
        {{""type"": ""image"", ""value"": ""{{{{image:description}}}}"", ""display"": ""block""}}
      ],
      ""hasLatex"":      false,
      ""hasImages"":     false,
      ""isPartial"":     false,
      ""correctAnswer"": null,
      ""options"": [
        {{
          ""label"":        ""A"",
          ""html"":         ""<div class='th-option'>...</div>"",
          ""contentParts"": [],
          ""plainText"":    ""..."",
          ""isCorrect"":    false,
          ""hasLatex"":     false,
          ""hasImages"":    false,
          ""orderIndex"":   0
        }}
      ]
    }}
  ],
  ""imageBounds"": [
    {{
      ""key"":    ""circuit_diagram"",
      ""x"":      62,
      ""y"":      42,
      ""width"":  35,
      ""height"": 20
    }}
  ],
  ""totalExtracted"": 1
}}

STRICT RULES:
- Return array even if only one question found
- questionHtml uses ONLY: th-question th-block th-center th-inline
  th-row th-col th-img th-math-block th-math-inline th-option
- No inline styles. No other CSS classes. No markdown.
- options [] for Theory and TrueFalse
- hasLatex true if ANY math present anywhere in question
- hasImages true if ANY image placeholder present
- isPartial true if question text is cut off
- Every {{{{image:key}}}} must have matching imageBounds entry
- imageBounds always returned — empty [] if no images";
	}
	// ═══════════════════════════════════════════════════════════
	// PRIVATE: PARSE CLAUDE RESPONSE
	// ═══════════════════════════════════════════════════════════

	private ClaudeProcessingResult ParseClaudeResponse(
	string rawJson, string questionType)
	{
		try
		{
			using var doc = JsonDocument.Parse(rawJson);
			var root = doc.RootElement;

			var result = new ClaudeProcessingResult { Success = true };

			// ── Validate questions array exists ──────────────────────────
			if (!root.TryGetProperty("questions", out var questionsEl)
				|| questionsEl.ValueKind != JsonValueKind.Array)
			{
				return new ClaudeProcessingResult
				{
					Success = false,
					ErrorMessage = "Claude response missing questions array"
				};
			}

			// ── Parse each question ──────────────────────────────────────
			foreach (var qEl in questionsEl.EnumerateArray())
			{
				var extracted = new ExtractedQuestion
				{
					QuestionNumber = qEl.TryGetProperty("questionNumber", out var qn)
										&& qn.ValueKind == JsonValueKind.Number
										? qn.GetInt32()
										: null,
					IsPartial = GetBool(qEl, "isPartial"),
					QuestionHtml = GetString(qEl, "questionHtml"),
					ContentPartsJson = GetRawString(qEl, "contentParts"),
					HasLatex = GetBool(qEl, "hasLatex"),
					HasImages = GetBool(qEl, "hasImages"),
					CorrectAnswer = GetString(qEl, "correctAnswer"),
					Options = new List<ParsedOption>()
				};

				// ── Parse options ────────────────────────────────────────
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
							ContentPartsJson = GetRawString(opt, "contentParts"),
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

			// ── Parse imageBounds array ──────────────────────────────────
			if (!root.TryGetProperty("imageBounds", out var boundsEl)
				|| boundsEl.ValueKind != JsonValueKind.Array)
			{
				// Not an error — question may genuinely have no images
				_logger.Warning(
					"Claude response missing imageBounds array — " +
					"no images will be cropped. " +
					"Questions with HasImages=true may have broken placeholders.");
			}
			else
			{
				foreach (var b in boundsEl.EnumerateArray())
				{
					var key = GetString(b, "key");

					if (string.IsNullOrWhiteSpace(key))
					{
						_logger.Warning("Skipping imageBound with empty key");
						continue;
					}

					var bound = new ImageBoundDto
					{
						Key = key,
						X = GetInt(b, "x"),
						Y = GetInt(b, "y"),
						Width = GetInt(b, "width"),
						Height = GetInt(b, "height")
					};

					// ── Validate bounds are within 0-100 range ───────────
					if (bound.X < 0 || bound.X > 100 ||
						bound.Y < 0 || bound.Y > 100 ||
						bound.Width <= 0 || bound.Width > 100 ||
						bound.Height <= 0 || bound.Height > 100)
					{
						_logger.Warning(
							"Invalid imageBound skipped - Key: {Key}, " +
							"X: {X}, Y: {Y}, W: {W}, H: {H}",
							bound.Key, bound.X, bound.Y,
							bound.Width, bound.Height);
						continue;
					}

					// ── Clamp x+width and y+height to 100 ───────────────
					if (bound.X + bound.Width > 100)
					{
						bound.Width = 100 - bound.X;
						_logger.Warning(
							"ImageBound width clamped - Key: {Key}", bound.Key);
					}

					if (bound.Y + bound.Height > 100)
					{
						bound.Height = 100 - bound.Y;
						_logger.Warning(
							"ImageBound height clamped - Key: {Key}", bound.Key);
					}

					result.ImageBounds.Add(bound);
				}
			}

			// ── Cross-check every {{image:key}} has a matching bound ─────
			if (result.Questions.Any(q => q.HasImages))
			{
				var boundKeys = result.ImageBounds
					.Select(b => b.Key)
					.ToHashSet();

				foreach (var question in result.Questions.Where(q => q.HasImages))
				{
					var placeholders = ExtractPlaceholderKeys(
						question.QuestionHtml ?? string.Empty);

					if (question.Options?.Any() == true)
					{
						foreach (var opt in question.Options.Where(o => o.HasImages))
						{
							placeholders.AddRange(
								ExtractPlaceholderKeys(opt.Html ?? string.Empty));
						}
					}

					foreach (var key in placeholders)
					{
						if (!boundKeys.Contains(key))
							_logger.Warning(
								"Placeholder {{image:{Key}}} has no matching " +
								"imageBound — image cannot be cropped. " +
								"Placeholder will remain unreplaced in saved question.",
								key);
					}
				}
			}

			// ── Final log ────────────────────────────────────────────────
			var partialCount = result.Questions.Count(q => q.IsPartial);
			var latexCount = result.Questions.Count(q => q.HasLatex);
			var imageCount = result.Questions.Count(q => q.HasImages);

			_logger.Information(
				"Claude response parsed - " +
				"Questions: {QCount}, " +
				"WithLatex: {LatexCount}, " +
				"WithImages: {ImageCount}, " +
				"Partial: {PartialCount}, " +
				"ImageBounds: {BCount}",
				result.Questions.Count,
				latexCount,
				imageCount,
				partialCount,
				result.ImageBounds.Count);

			return result;
		}
		catch (JsonException jsonEx)
		{
			_logger.Error(jsonEx, "Failed to parse Claude JSON response");
			return new ClaudeProcessingResult
			{
				Success = false,
				ErrorMessage = $"Invalid JSON from Claude: {jsonEx.Message}"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Unexpected error parsing Claude response");
			return new ClaudeProcessingResult
			{
				Success = false,
				ErrorMessage = $"Failed to parse Claude response: {ex.Message}"
			};
		}
	}

	private async Task<Dictionary<string, string>> ProcessImageBounds(byte[] originalImageBytes,List<ImageBoundDto> imageBounds,Guid jobId,Guid schoolId)
	{
		var imageUrlMap = new Dictionary<string, string>();

		if (imageBounds == null || !imageBounds.Any())
			return imageUrlMap;

		foreach (var bound in imageBounds)
		{
			try
			{
				_logger.Information(
					"Cropping image - Key: {Key}, " +
					"X: {X}%, Y: {Y}%, W: {W}%, H: {H}%",
					bound.Key, bound.X, bound.Y,
					bound.Width, bound.Height);

				// ── Crop the region from the original image ───────────
				var croppedBytes = CropImage(
					originalImageBytes,
					bound.X, bound.Y,
					bound.Width, bound.Height);

				if (croppedBytes == null || croppedBytes.Length == 0)
				{
					_logger.Warning(
						"Crop returned empty bytes - Key: {Key}",
						bound.Key);
					continue;
				}

				_logger.Information(
					"Image cropped - Key: {Key}, Size: {Size} bytes",
					bound.Key, croppedBytes.Length);

				// ── Upload cropped image to Cloudinary ────────────────
				// Use a deterministic key so duplicate jobs
				// do not create duplicate Cloudinary assets
				var imageKey = $"qimg_{jobId}_{bound.Key}";

				using var stream = new MemoryStream(croppedBytes);

				var uploadResult = await _cloudinaryService.UploadMediaAsync(
					stream,
					imageKey,
					schoolId,
					MediaType.Image,
					isTemporary: false);  // permanent — question image

				if (!uploadResult.Success)
				{
					_logger.Warning(
						"Diagram upload failed - Key: {Key}, Error: {Error}",
						bound.Key, uploadResult.ErrorMessage);
					continue;
				}

				imageUrlMap[bound.Key] = uploadResult.SecureUrl;

				_logger.Information(
					"Diagram uploaded - Key: {Key}, Url: {Url}",
					bound.Key, uploadResult.SecureUrl);
			}
			catch (Exception ex)
			{
				// Log and continue — one failed crop should not
				// block the rest of the question from being saved
				_logger.Error(ex,
					"Error processing image bound - Key: {Key}", bound.Key);
			}
		}

		_logger.Information(
			"ProcessImageBounds complete - " +
			"Requested: {Total}, Uploaded: {Uploaded}",
			imageBounds.Count, imageUrlMap.Count);

		return imageUrlMap;
	}

	//private byte[] CropImage(
	//	byte[] sourceBytes,
	//	int xPct, int yPct,
	//	int widthPct, int heightPct)
	//{
	//	using var image = SixLabors.ImageSharp.Image.Load(sourceBytes);

	//	var x = (int)(image.Width * xPct / 100.0);
	//	var y = (int)(image.Height * yPct / 100.0);
	//	var width = (int)(image.Width * widthPct / 100.0);
	//	var height = (int)(image.Height * heightPct / 100.0);

	//	// Clamp to image bounds — prevents out of range exceptions
	//	x = Math.Max(0, Math.Min(x, image.Width - 1));
	//	y = Math.Max(0, Math.Min(y, image.Height - 1));
	//	width = Math.Max(1, Math.Min(width, image.Width - x));
	//	height = Math.Max(1, Math.Min(height, image.Height - y));

	//	_logger.Information(
	//		"Crop pixels - X: {X}, Y: {Y}, W: {W}, H: {H} " +
	//		"(Image: {IW}x{IH})",
	//		x, y, width, height,
	//		image.Width, image.Height);

	//	image.Mutate(ctx => ctx.Crop(
	//		new SixLabors.ImageSharp.Rectangle(x, y, width, height)));

	//	using var ms = new MemoryStream();
	//	image.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
	//	{
	//		Quality = 90
	//	});

	//	return ms.ToArray();
	//}


private byte[] CropImage(byte[] sourceBytes,int xPct, int yPct,int widthPct, int heightPct)
{
		using var bitmap = SKBitmap.Decode(sourceBytes);

		var x = (int)(bitmap.Width * xPct / 100.0);
		var y = (int)(bitmap.Height * yPct / 100.0);
		var width = (int)(bitmap.Width * widthPct / 100.0);
		var height = (int)(bitmap.Height * heightPct / 100.0);

		// Clamp to bitmap bounds
		x = Math.Max(0, Math.Min(x, bitmap.Width - 1));
		y = Math.Max(0, Math.Min(y, bitmap.Height - 1));
		width = Math.Max(1, Math.Min(width, bitmap.Width - x));
		height = Math.Max(1, Math.Min(height, bitmap.Height - y));

		_logger.Information(
			"Crop pixels - X: {X}, Y: {Y}, W: {W}, H: {H} " +
			"(Image: {IW}x{IH})",
			x, y, width, height,
			bitmap.Width, bitmap.Height);

		// Extract the cropped region
		var cropRect = new SKRectI(x, y, x + width, y + height);
		using var cropped = new SKBitmap();
		bitmap.ExtractSubset(cropped, cropRect);

		// Encode to JPEG
		using var image = SKImage.FromBitmap(cropped);
		using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
		using var ms = new MemoryStream();
		data.SaveTo(ms);

		return ms.ToArray();
}
// ── Extract all {{image:key}} keys from an HTML string ───────────────────
private List<string> ExtractPlaceholderKeys(string html)
{
		var keys = new List<string>();
		var start = 0;

		while (true)
		{
			var open = html.IndexOf("{{image:", start, StringComparison.Ordinal);
			if (open < 0) break;

			var close = html.IndexOf("}}", open + 8, StringComparison.Ordinal);
			if (close < 0) break;

			var key = html.Substring(open + 8, close - open - 8).Trim();
			if (!string.IsNullOrWhiteSpace(key))
				keys.Add(key);

			start = close + 2;
		}

		return keys;
	}

	private string ReplacePlaceholders(string? content,Dictionary<string, string> imageUrlMap)
	{
		if (string.IsNullOrWhiteSpace(content) || !imageUrlMap.Any())
			return content ?? string.Empty;

		foreach (var kvp in imageUrlMap)
		{
			var placeholder = $"{{{{image:{kvp.Key}}}}}";
			content = content.Replace(placeholder, kvp.Value);
		}

		return content;
	}

	private string ReplacePlaceholdersInHtml(string? html, Dictionary<string, string> imageUrlMap)
	{
		if (string.IsNullOrWhiteSpace(html) || !imageUrlMap.Any())
			return html ?? string.Empty;

		foreach (var kvp in imageUrlMap)
		{
			var placeholder = $"{{{{image:{kvp.Key}}}}}";
			var imgTag = $"<img class='th-img' src='{kvp.Value}' alt='{kvp.Key}' />";
			html = html.Replace(placeholder, imgTag);
		}

		return html;
	}



	private List<string> ExtractPlaceholderKeysFromOriginal(string questionHtml,string contentPartsJson,List<ParsedOption> options)
	{
		var keys = new List<string>();

		keys.AddRange(ExtractPlaceholderKeys(questionHtml));
		keys.AddRange(ExtractPlaceholderKeys(contentPartsJson));

		if (options?.Any() == true)
		{
			foreach (var opt in options)
			{
				keys.AddRange(ExtractPlaceholderKeys(opt.Html ?? string.Empty));
				keys.AddRange(ExtractPlaceholderKeys(opt.ContentPartsJson ?? string.Empty));
			}
		}

		return keys.Distinct().ToList();
	}

	// ── Helper: get string value from JsonElement ─────────────────────────────
	//private string GetString(JsonElement el, string key)
	//{
	//	if (el.TryGetProperty(key, out var prop)
	//		&& prop.ValueKind != JsonValueKind.Null)
	//		return prop.GetString() ?? string.Empty;

	//	return string.Empty;
	//}

	// ── Helper: get raw JSON string (for arrays like contentParts) ────────────
	private string GetRawString(JsonElement el, string key)
	{
		if (el.TryGetProperty(key, out var prop))
		{
			return prop.ValueKind switch
			{
				JsonValueKind.String => prop.GetString() ?? string.Empty,
				JsonValueKind.Array => prop.GetRawText(),
				JsonValueKind.Null => string.Empty,
				_ => prop.GetRawText()
			};
		}
		return string.Empty;
	}

	// ── Helper: get bool value from JsonElement ───────────────────────────────
	//private bool GetBool(JsonElement el, string key)
	//{
	//	if (el.TryGetProperty(key, out var prop)
	//		&& prop.ValueKind == JsonValueKind.True
	//		|| (el.TryGetProperty(key, out prop)
	//			&& prop.ValueKind == JsonValueKind.False))
	//		return prop.GetBoolean();

	//	return false;
	//}

	// ── Helper: get int value from JsonElement ────────────────────────────────
	private int GetInt(JsonElement el, string key)
	{
		if (el.TryGetProperty(key, out var prop))
		{
			return prop.ValueKind switch
			{
				JsonValueKind.Number => prop.TryGetInt32(out var i) ? i : 0,
				JsonValueKind.String => int.TryParse(prop.GetString(), out var s) ? s : 0,
				_ => 0
			};
		}
		return 0;
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
	public List<ImageBoundDto> ImageBounds { get; set; } = new();

}

internal class ExtractedQuestion
{
	public int? QuestionNumber { get; set; }  // ← add
	public bool IsPartial { get; set; }  // ← add
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

