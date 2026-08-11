using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Serilog;
using TechHub.Core;
using TechHub.Core.Configuration;
using TechHub.Core.Constant;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Helper;
using TechHub.Core.Interface;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;
using TechHub.Service.Service.ImageGeneration;
using MediaType = TechHub.Core.Enum.MediaType;

namespace TechHub.Service.Service;

/// <summary>
/// AI teaching-material generation pipeline (image first).
///
/// FLOW:
///  1. Feature gate — the school must be privileged (SchoolFeature table).
///  2. Resolve the lesson context (aim + objectives + school/subject/class).
///  3. Build the prompt (teacher override wins) and store it as the latest
///     LessonGenerationPrompt row (always pick the last prompt).
///  4. Call the configured agent (swappable via ImageGeneration:Provider).
///  5. Upload the produced bytes to Cloudinary and attach as LessonMedia.
///  6. Update the prompt row with the image outcome so teachers can review,
///     edit the last prompt and regenerate.
/// </summary>
public class ImageGenerationService : IImageGenerationService
{
	private const string PromptStatusPending = "Pending";
	private const string PromptStatusCompleted = "Completed";
	private const string PromptStatusFailed = "Failed";
	private const string PromptStatusDeclined = "Declined";

	private const string ContextSql = @"
		SELECT
			lc.Id           AS LessonId,
			lc.CreatedBy    AS CreatedBy,
			lc.Aim          AS Aim,
			lc.Description  AS Description,
			s.Subject       AS SubjectName,
			t.Name          AS TopicName,
			c.Name          AS ClassName,
			sch.SchoolName  AS SchoolName
		FROM   LessonContent lc
		LEFT JOIN Subjects   s   ON s.Id  = lc.SubjectId
		LEFT JOIN Topic      t   ON t.Id  = lc.TopicId
		LEFT JOIN Classroom  c   ON c.Id  = lc.ClassroomId
		LEFT JOIN School     sch ON sch.Id = lc.SchoolId
		WHERE  lc.Id = @LessonId AND lc.SchoolId = @SchoolId";

	private readonly IQueryRepository<LessonContent> _lessonQuery;
	private readonly IQueryRepository<LessonGenerationPrompt> _promptQuery;
	private readonly ICommandRespository<LessonGenerationPrompt> _promptCommand;
	private readonly IQueryRepository<LessonMedia> _mediaQuery;
	private readonly ICommandRespository<LessonMedia> _mediaCommand;
	private readonly IImageGenerationAgentFactory _agentFactory;
	private readonly ISchoolFeatureService _featureService;
	private readonly ICloudinaryService _cloudinaryService;
	private readonly IInstructionalPromptRefiner _promptRefiner;
	private readonly ImageGenerationSettings _settings;
	private readonly ILogger _logger;

	public ImageGenerationService(
		IQueryRepository<LessonContent> lessonQuery,
		IQueryRepository<LessonGenerationPrompt> promptQuery,
		ICommandRespository<LessonGenerationPrompt> promptCommand,
		IQueryRepository<LessonMedia> mediaQuery,
		ICommandRespository<LessonMedia> mediaCommand,
		IImageGenerationAgentFactory agentFactory,
		ISchoolFeatureService featureService,
		ICloudinaryService cloudinaryService,
		IInstructionalPromptRefiner promptRefiner,
		IOptions<ImageGenerationSettings> settings,
		ILogger logger)
	{
		_lessonQuery = lessonQuery;
		_promptQuery = promptQuery;
		_promptCommand = promptCommand;
		_mediaQuery = mediaQuery;
		_mediaCommand = mediaCommand;
		_agentFactory = agentFactory;
		_featureService = featureService;
		_cloudinaryService = cloudinaryService;
		_promptRefiner = promptRefiner;
		_settings = settings.Value;
		_logger = logger;
	}

	public async Task<BaseResponse> GenerateImageAsync(
		Guid lessonId,
		GenerateImageViewModel model,
		AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.UserId, out var userId))
				return Unauthorized();
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			// ── 1. Feature gate (security) ───────────────────────────────
			if (!await _featureService.IsFeatureEnabledAsync(schoolId, FeatureKeys.ImageGeneration))
				return new BaseResponse
				{
					ResponseCode = ResponseCode.Forbidden,
					ResponseMessage = "AI image generation is not enabled for your school",
					Status = "failed"
				};

			// ── 2. Resolve lesson context + ownership (security) ─────────
			var ctx = (await _lessonQuery.QueryAsync<LessonGenerationContextDto>(
				ContextSql,
				new Dictionary<string, object>
				{
					{ "LessonId", lessonId },
					{ "SchoolId", schoolId }
				})).FirstOrDefault();

			if (ctx is null)
				return NotFound("Lesson not found");

			if (!IsManagerRole(claims.Role) && ctx.CreatedBy != userId)
				return new BaseResponse
				{
					ResponseCode = ResponseCode.Forbidden,
					ResponseMessage = "You can only generate images for your own lessons",
					Status = "failed"
				};

			// ── 3. Build prompt (teacher override wins) ──────────────────
			var maxPromptLength = Math.Max(100, _settings.MaxPromptLength);
			var teacherPrompt = SanitizePrompt(model?.Prompt, maxPromptLength);
			var materialWords = SanitizePrompt(model?.MaterialWords, maxPromptLength);

			string finalPrompt;
			if (!string.IsNullOrWhiteSpace(teacherPrompt))
			{
				finalPrompt = string.IsNullOrWhiteSpace(model?.Style)
					? teacherPrompt
					: $"{teacherPrompt}\n\nStyle: {model.Style.Trim()}";
			}
			else
			{
				var draftPrompt = TeachingPromptBuilder.BuildLessonImagePrompt(
					ctx.SchoolName,
					ctx.SubjectName,
					ctx.TopicName,
					ctx.ClassName,
					ctx.Aim,
					ctx.Description,
					materialWords,
					model?.Style);

				// Claude redefines the draft (aim + objectives + the teacher's
				// material words) into a stronger teaching prompt. It may also
				// DECLINE when the teacher's requested materials do not align
				// with the subject / aim / objectives — in that case no image
				// should be generated and the reason is surfaced to the teacher.
				var refined = await _promptRefiner.RefineLessonImagePromptAsync(draftPrompt, ctx.ClassName);

				if (refined.Declined)
				{
					_logger.Warning(
						"Image prompt declined by Claude - LessonId: {LessonId}, Reason: {Reason}",
						lessonId, refined.DeclineReason);

					await RecordDeclinedPromptAsync(
						lessonId, schoolId, userId, draftPrompt, refined.DeclineReason);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = string.IsNullOrWhiteSpace(refined.DeclineReason)
							? "Image request declined - the requested materials do not align with the lesson's subject, aim or objectives."
							: $"Image request declined: {refined.DeclineReason}",
						Status = "failed",
						Data = new
						{
							Declined = true,
							Reason = refined.DeclineReason
						}
					};
				}

				finalPrompt = refined.Success && !string.IsNullOrWhiteSpace(refined.Prompt)
					? refined.Prompt
					: draftPrompt;
			}

			var agent = _agentFactory.GetAgent();
			var targetCount = ResolveImageCount(model?.ImageCount);

			var generated = new List<object>();
			var failures = new List<string>();

			for (var i = 1; i <= targetCount; i++)
			{
				var single = await GenerateSingleImageAsync(
					lessonId, schoolId, userId, finalPrompt,
					teacherPrompt, materialWords, model, agent);

				if (single.Success)
					generated.Add(single.Data!);
				else
					failures.Add(single.ErrorMessage ?? "Image generation failed. Please try again.");
			}

			if (generated.Count == 0)
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = failures.FirstOrDefault() ?? "Image generation failed. Please try again.",
					Status = "failed"
				};
			}

			// Backward compatible: a single requested image keeps the original
			// flat response shape so existing clients are unaffected.
			if (generated.Count == 1)
			{
				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Image generated and attached to lesson",
					Status = "successful",
					Data = generated[0]
				};
			}

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = failures.Count == 0
					? $"{generated.Count} images generated and attached to lesson"
					: $"{generated.Count} image(s) generated, {failures.Count} failed",
				Status = "successful",
				Data = new
				{
					Count = generated.Count,
					FailedCount = failures.Count,
					Images = generated
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Unexpected error generating image - LessonId: {LessonId}, UserId: {UserId}",
				lessonId, claims?.UserId);
			return ServerError();
		}
	}

	public async Task<BaseResponse> GetLastPromptAsync(Guid lessonId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			var lesson = await _lessonQuery.Get(lessonId);
			if (lesson is null || lesson.SchoolId != schoolId)
				return NotFound("Lesson not found");

			var last = (await _promptQuery.QueryAsync<LessonGenerationPrompt>(
				"SELECT TOP 1 * FROM LessonGenerationPrompt WHERE LessonId = @LessonId AND SchoolId = @SchoolId AND IsActive = 1 ORDER BY CreatedAt DESC, Id DESC",
				new Dictionary<string, object>
				{
					{ "LessonId", lessonId },
					{ "SchoolId", schoolId }
				})).FirstOrDefault();

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = last is null
					? "No generation prompt found for this lesson"
					: "Last generation prompt retrieved",
				Status = "successful",
				Data = last
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error retrieving last prompt - LessonId: {LessonId}", lessonId);
			return ServerError();
		}
	}

	public async Task<BaseResponse> GetGenerationHistoryAsync(Guid lessonId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			var lesson = await _lessonQuery.Get(lessonId);
			if (lesson is null || lesson.SchoolId != schoolId)
				return NotFound("Lesson not found");

			var rows = await _promptQuery.QueryAsync<LessonGenerationPrompt>(
				"SELECT * FROM LessonGenerationPrompt WHERE LessonId = @LessonId AND SchoolId = @SchoolId AND IsActive = 1 ORDER BY CreatedAt DESC, Id DESC",
				new Dictionary<string, object>
				{
					{ "LessonId", lessonId },
					{ "SchoolId", schoolId }
				});

			var history = rows.ToList();

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = history.Any()
					? $"{history.Count} generation(s) found"
					: "No generations found for this lesson",
				Status = "successful",
				Data = new
				{
					LessonId = lessonId,
					Count = history.Count,
					Generations = history
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error retrieving generation history - LessonId: {LessonId}", lessonId);
			return ServerError();
		}
	}

	public async Task<BaseResponse> GetGeneratedImagesAsync(Guid lessonId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			var lesson = await _lessonQuery.Get(lessonId);
			if (lesson is null || lesson.SchoolId != schoolId)
				return NotFound("Lesson not found");

			var rows = await _promptQuery.QueryAsync<LessonGenerationPrompt>(
				"SELECT * FROM LessonGenerationPrompt WHERE LessonId = @LessonId AND SchoolId = @SchoolId AND Status = 'Completed' AND IsActive = 1 ORDER BY CreatedAt DESC, Id DESC",
				new Dictionary<string, object>
				{
					{ "LessonId", lessonId },
					{ "SchoolId", schoolId }
				});

			var images = rows.Select(r => new
			{
				PromptId = r.Id,
				r.ImageUrl,
				r.ImagePublicId,
				r.MediaId,
				r.AgentType,
				r.CreatedAt
			}).ToList();

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = images.Any()
					? $"{images.Count} generated image(s) found"
					: "No generated images found for this lesson",
				Status = "successful",
				Data = new
				{
					LessonId = lessonId,
					Count = images.Count,
					Images = images
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error retrieving generated images - LessonId: {LessonId}", lessonId);
			return ServerError();
		}
	}

	public async Task<BaseResponse> GetGenerationStatusAsync(Guid lessonId, AuthenticatedUserClaims claims)
	{
		try
		{
			if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				return Unauthorized();

			var lesson = await _lessonQuery.Get(lessonId);
			if (lesson is null || lesson.SchoolId != schoolId)
				return NotFound("Lesson not found");

			var rows = (await _promptQuery.QueryAsync<LessonGenerationPrompt>(
				"SELECT * FROM LessonGenerationPrompt WHERE LessonId = @LessonId AND SchoolId = @SchoolId AND IsActive = 1",
				new Dictionary<string, object>
				{
					{ "LessonId", lessonId },
					{ "SchoolId", schoolId }
				})).ToList();

			var completedCount = rows.Count(r => r.Status == PromptStatusCompleted);
			var failedCount = rows.Count(r => r.Status == PromptStatusFailed);
			var pendingCount = rows.Count(r => r.Status == PromptStatusPending);
			var declinedCount = rows.Count(r => r.Status == PromptStatusDeclined);

			var last = rows
				.OrderByDescending(r => r.CreatedAt)
				.ThenByDescending(r => r.Id)
				.FirstOrDefault();

			var targetCount = Math.Max(1, lesson.ImageCount);
			var isApproved = lesson.Status == LessonStatus.Approved
				|| lesson.Status == LessonStatus.Published;

			string generationStatus;
			string message;
			string? imageUrl = null;
			Guid? promptId = null;

			if (!isApproved)
			{
				generationStatus = "NotApproved";
				message = lesson.Status == LessonStatus.Rejected
					? "Lesson was rejected - no image will be generated."
					: "Lesson not approved yet. The image will be generated automatically once the lesson is approved.";
			}
			else if (!lesson.ShouldGenerateImage)
			{
				generationStatus = "Disabled";
				message = "Image generation is disabled for this lesson.";
			}
			else if (completedCount + failedCount + pendingCount == 0)
			{
				generationStatus = "Queued";
				message = "Image generation has been queued for this lesson. Check back shortly.";
			}
			else if (completedCount >= targetCount)
			{
				generationStatus = "Completed";
				promptId = last?.Id;
				imageUrl = last?.ImageUrl;
				message = completedCount == 1
					? "Image generated successfully."
					: $"{completedCount} images generated successfully.";
			}
			else if (pendingCount > 0)
			{
				generationStatus = "InProgress";
				promptId = last?.Id;
				message = $"{completedCount} of {targetCount} image(s) generated - generation is still in progress.";
			}
			else if (completedCount > 0 && (failedCount > 0 || declinedCount > 0))
			{
				generationStatus = "PartiallyCompleted";
				promptId = last?.Id;
				imageUrl = last?.ImageUrl;
				message = $"{completedCount} of {targetCount} image(s) generated; {failedCount} failed, {declinedCount} declined. You can regenerate the missing ones.";
			}
			else if (declinedCount > 0 && completedCount == 0)
			{
				generationStatus = "Declined";
				promptId = last?.Id;
				message = string.IsNullOrWhiteSpace(last?.ErrorMessage)
					? "Image request was declined - the requested materials do not align with the lesson's subject, aim or objectives."
					: $"Image request declined: {last.ErrorMessage}";
			}
			else
			{
				generationStatus = "Failed";
				promptId = last?.Id;
				message = string.IsNullOrWhiteSpace(last?.ErrorMessage)
					? "Image generation failed. Please try again."
					: $"Image generation failed: {last.ErrorMessage}";
			}

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = message,
				Status = "successful",
				Data = new
				{
					LessonId = lessonId,
					LessonStatus = lesson.Status,
					Approved = isApproved,
					ShouldGenerateImage = lesson.ShouldGenerateImage,
					TargetCount = targetCount,
					CompletedCount = completedCount,
					GenerationStatus = generationStatus,
					PromptId = promptId,
					ImageUrl = imageUrl,
					LatestAttempt = last
				}
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Error retrieving generation status - LessonId: {LessonId}", lessonId);
			return ServerError();
		}
	}

	#region Helpers

	private async Task<int> NextDisplayOrderAsync(Guid lessonId)
	{
		var rows = await _mediaQuery.QueryAsync<int?>(
			"SELECT MAX(DisplayOrder) FROM LessonMedia WHERE LessonContentId = @LessonId",
			new Dictionary<string, object> { { "LessonId", lessonId } });

		var max = rows.FirstOrDefault() ?? 0;
		return max + 1;
	}

	private async Task MarkPromptFailedAsync(Guid promptId, string? errorMessage)
	{
		try
		{
			await _promptCommand.UpdateTableColumnById(
				new Dictionary<string, object>
				{
					{ "Status", PromptStatusFailed },
					{ "ErrorMessage", errorMessage }
				},
				new KeyValuePair<string, object>("Id", promptId));
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Failed to update prompt status - PromptId: {PromptId}", promptId);
		}
	}

	/// <summary>
	/// Records a Claude refusal so the teacher can see (via the status/history
	/// endpoints) why no image was generated for the lesson.
	/// </summary>
	private async Task RecordDeclinedPromptAsync(
		Guid lessonId, Guid schoolId, Guid userId, string prompt, string? reason)
	{
		try
		{
			await _promptCommand.Create(new LessonGenerationPrompt
			{
				Id = Guid.NewGuid(),
				SchoolId = schoolId,
				LessonId = lessonId,
				CreatedBy = userId,
				PromptText = prompt,
				TeacherPrompt = null,
				AgentType = "Claude",
				Style = null,
				Status = PromptStatusDeclined,
				ErrorMessage = reason,
				CreatedAt = DateTime.UtcNow,
				IsActive = true
			});
		}
		catch (Exception ex)
		{
			_logger.Error(ex,
				"Failed to record declined prompt - LessonId: {LessonId}", lessonId);
		}
	}

	private async Task<SingleImageGenerationResult> GenerateSingleImageAsync(
		Guid lessonId,
		Guid schoolId,
		Guid userId,
		string finalPrompt,
		string? teacherPrompt,
		string? materialWords,
		GenerateImageViewModel model,
		IImageGenerationAgent agent)
	{
		var promptId = Guid.NewGuid();

		// ── Persist the prompt (always pick the last one) ───────────────
		var promptEntity = new LessonGenerationPrompt
		{
			Id = promptId,
			SchoolId = schoolId,
			LessonId = lessonId,
			CreatedBy = userId,
			PromptText = finalPrompt,
			TeacherPrompt = !string.IsNullOrWhiteSpace(teacherPrompt)
				? teacherPrompt
				: (string.IsNullOrWhiteSpace(materialWords) ? null : materialWords),
			AgentType = agent.Name,
			Style = model?.Style,
			Status = PromptStatusPending,
			CreatedAt = DateTime.UtcNow,
			IsActive = true
		};

		await _promptCommand.Create(promptEntity);

		// ── Call the agent ─────────────────────────────────────────────
		var request = new ImageGenerationRequest
		{
			Prompt = finalPrompt,
			NegativePrompt = model?.NegativePrompt,
			Width = model?.Width,
			Height = model?.Height,
			Style = model?.Style
		};

		var result = await agent.GenerateAsync(request);

		if (!result.Success || result.ImageBytes is null || result.ImageBytes.Length == 0)
		{
			_logger.Warning(
				"Image generation failed - LessonId: {LessonId}, PromptId: {PromptId}, Error: {Error}",
				lessonId, promptId, result?.ErrorMessage);

			await MarkPromptFailedAsync(promptId, result?.ErrorMessage);

			return new SingleImageGenerationResult
			{
				Success = false,
				ErrorMessage = result?.ErrorMessage ?? "Image generation failed. Please try again."
			};
		}

		// ── Upload to Cloudinary (server-side, permanent) ──────────────
		var extension = result.ContentType?.ToLower().Contains("png") == true ? "png" : "jpg";
		var mediaKey = MediaKeyGenerator.GenerateKey(schoolId, $"ai_lesson_{lessonId:N}.{extension}");

		CloudinaryUploadResult upload;
		using (var stream = new MemoryStream(result.ImageBytes))
		{
			upload = await _cloudinaryService.UploadMediaAsync(
				stream,
				mediaKey,
				schoolId,
				MediaType.Image,
				isTemporary: false);
		}

		if (!upload.Success)
		{
			_logger.Error(
				"Cloudinary upload failed after generation - LessonId: {LessonId}, Error: {Error}",
				lessonId, upload.ErrorMessage);

			await MarkPromptFailedAsync(promptId, "Failed to store generated image");

			return new SingleImageGenerationResult
			{
				Success = false,
				ErrorMessage = "Image was generated but could not be stored. Please try again."
			};
		}

		// ── Attach as LessonMedia ──────────────────────────────────────
		var mediaId = Guid.NewGuid();
		var now = DateTime.UtcNow;
		var displayOrder = await NextDisplayOrderAsync(lessonId);

		var mediaEntity = new LessonMedia
		{
			Id = mediaId,
			LessonContentId = lessonId,
			SchoolId = schoolId,
			FileName = mediaKey,
			OriginalFileName = $"ai_lesson_{lessonId:N}.{extension}",
			FileExtension = $".{extension}",
			MediaType = "Image",
			FileSizeBytes = upload.FileSizeBytes > 0 ? upload.FileSizeBytes : result.ImageBytes.Length,
			CloudinaryUrl = upload.SecureUrl,
			PublicId = upload.PublicId,
			Duration = null,
			Status = "Ready",
			DisplayOrder = displayOrder,
			CreatedAt = now,
			IsActive = true,
			MetaData = System.Text.Json.JsonSerializer.Serialize(new
			{
				GeneratedBy = "AI",
				AgentType = agent.Name,
				ModelUsed = result.ModelUsed,
				PromptId = promptId
			})
		};

		await _mediaCommand.Create(mediaEntity);

		// ── Finalize prompt row ────────────────────────────────────────
		await _promptCommand.UpdateTableColumnById(
			new Dictionary<string, object>
			{
				{ "Status", PromptStatusCompleted },
				{ "MediaId", mediaId },
				{ "ImageUrl", upload.SecureUrl },
				{ "ImagePublicId", upload.PublicId }
			},
			new KeyValuePair<string, object>("Id", promptId));

		_logger.Information(
			"Image generated and attached - LessonId: {LessonId}, PromptId: {PromptId}, MediaId: {MediaId}, Agent: {Agent}",
			lessonId, promptId, mediaId, agent.Name);

		return new SingleImageGenerationResult
		{
			Success = true,
			Data = new
			{
				PromptId = promptId,
				MediaId = mediaId,
				ImageUrl = upload.SecureUrl,
				PublicId = upload.PublicId,
				Prompt = finalPrompt,
				AgentType = agent.Name,
				ModelUsed = result.ModelUsed
			}
		};
	}

	private int ResolveImageCount(int? count)
	{
		var max = Math.Max(1, _settings.MaxImagesPerLesson);
		return count.HasValue ? Math.Clamp(count.Value, 1, max) : 1;
	}

	private static string SanitizePrompt(string? value, int maxLength)
	{
		if (string.IsNullOrWhiteSpace(value))
			return string.Empty;

		var sanitized = value.Trim().Replace("\0", string.Empty);
		return sanitized.Length > maxLength ? sanitized[..maxLength] : sanitized;
	}

	private static bool IsManagerRole(string? role)
		=> role is "Administrator" or "SuperAdministrator";

	private static BaseResponse Unauthorized() => new()
	{
		ResponseCode = ResponseCode.Unauthorized,
		ResponseMessage = "Invalid authentication",
		Status = "failed"
	};

	private static BaseResponse NotFound(string message) => new()
	{
		ResponseCode = ResponseCode.NotFound,
		ResponseMessage = message,
		Status = "failed"
	};

	private static BaseResponse ServerError() => new()
	{
		ResponseCode = ResponseCode.ErrorOccured,
		ResponseMessage = "An unexpected error occurred",
		Status = "failed"
	};

	#endregion

	/// <summary>
	/// Outcome of generating one image within a multi-image request.
	/// </summary>
	private sealed class SingleImageGenerationResult
	{
		public bool Success { get; set; }
		public object? Data { get; set; }
		public string? ErrorMessage { get; set; }
	}
}
