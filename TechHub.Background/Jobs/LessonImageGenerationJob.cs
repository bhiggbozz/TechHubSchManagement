using Hangfire;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;

namespace TechHub.Background.Jobs;

/// <summary>
/// Auto-generates a teaching image for a lesson once it has been approved.
/// Enqueued from the lesson approval / auto-publish paths. The image is built
/// from the lesson's aim + objectives and attached as LessonMedia — no
/// teacher click required.
/// </summary>
	public class LessonImageGenerationJob
	{
		private readonly IImageGenerationService _generationService;
		private readonly IQueryRepository<LessonContent> _lessonQuery;
		private readonly IQueryRepository<LessonGenerationPrompt> _promptQuery;
		private readonly ILogger<LessonImageGenerationJob> _logger;

		public LessonImageGenerationJob(
			IImageGenerationService generationService,
			IQueryRepository<LessonContent> lessonQuery,
			IQueryRepository<LessonGenerationPrompt> promptQuery,
			ILogger<LessonImageGenerationJob> logger)
		{
			_generationService = generationService;
			_lessonQuery = lessonQuery;
			_promptQuery = promptQuery;
			_logger = logger;
		}

	[Queue("default")]
	[AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60 })]
	public async Task ExecuteAsync(Guid lessonId, Guid schoolId, Guid userId)
	{
		using (LogContext.PushProperty("JobType", "LessonImageGeneration"))
		using (LogContext.PushProperty("LessonId", lessonId))
		using (LogContext.PushProperty("SchoolId", schoolId))
		{
			try
			{
				// Respect the teacher's submit-time intent: never generate for
				// a lesson they opted out of.
				var lesson = await _lessonQuery.Get(lessonId);
				if (lesson is null || lesson.SchoolId != schoolId)
				{
					_logger.LogWarning(
						"Lesson not found for auto image generation - LessonId: {LessonId}, SchoolId: {SchoolId}",
						lessonId, schoolId);
					return;
				}

				if (!lesson.ShouldGenerateImage)
				{
					_logger.LogInformation(
						"Lesson opted out of image generation - skipping auto generation - LessonId: {LessonId}",
						lessonId);
					return;
				}

				// Generate the shortfall vs the requested image count — skip when
				// the target is already met (e.g. the teacher generated some or
				// all images manually while creating the lesson).
				var existingCount = (await _promptQuery.QueryAsync<int?>(
					"SELECT COUNT(1) FROM LessonGenerationPrompt WHERE LessonId = @LessonId AND SchoolId = @SchoolId AND Status = 'Completed' AND IsActive = 1",
					new Dictionary<string, object>
					{
						{ "LessonId", lessonId },
						{ "SchoolId", schoolId }
					})).FirstOrDefault() ?? 0;

				var targetCount = Math.Max(1, lesson.ImageCount);
				var shortfall = targetCount - existingCount;

				if (shortfall <= 0)
				{
					_logger.LogInformation(
						"Lesson image generation target already met - skipping auto generation - LessonId: {LessonId}, ExistingCount: {ExistingCount}, TargetCount: {TargetCount}",
						lessonId, existingCount, targetCount);
					return;
				}

				var claims = new AuthenticatedUserClaims
				{
					UserId = userId.ToString(),
					SchoolId = schoolId.ToString(),
					Role = "Administrator"
				};

				// Teacher's material words are combined with the lesson's
				// aim + objectives when the prompt is built.
				var model = new GenerateImageViewModel { ImageCount = shortfall };
				if (!string.IsNullOrWhiteSpace(lesson.ImageMaterialWords))
					model.MaterialWords = lesson.ImageMaterialWords;

				var result = await _generationService.GenerateImageAsync(
					lessonId, model, claims);

				if (result.ResponseCode == ResponseCode.successful)
				{
					_logger.LogInformation(
						"Auto image generation completed - LessonId: {LessonId}",
						lessonId);
				}
				else
				{
					// Business-level skip (feature disabled, etc.) — not a retryable failure.
					_logger.LogWarning(
						"Auto image generation skipped - LessonId: {LessonId}, Code: {Code}, Message: {Message}",
						lessonId, result.ResponseCode, result.ResponseMessage);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex,
					"Exception during auto image generation - LessonId: {LessonId}",
					lessonId);
				throw;
			}
		}
	}
}
