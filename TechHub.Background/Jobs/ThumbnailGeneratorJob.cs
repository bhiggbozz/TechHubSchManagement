//namespace TechHub.Background.Jobs
//{
//	internal class ThumbnailGeneratorJob
//	{
//	}
//}

// TechHub.Background/Jobs/ThumbnailGenerationJob.cs

using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Service.Interface;

namespace TechHub.Background.Jobs
{
	/// <summary>
	/// Background job for generating video thumbnails and preview clips
	/// 
	/// PURPOSE:
	/// - Generate thumbnail image (frame at 2 seconds)
	/// - Generate preview URL (first 30 seconds of video)
	/// - Update database with thumbnail/preview URLs
	/// 
	/// WHEN TRIGGERED:
	/// - After direct-to-CDN upload confirmed (ConfirmUpload)
	/// - After server-side upload completed (webhook)
	/// - Only for video files (not images/documents)
	/// 
	/// CLOUDINARY TRANSFORMATIONS:
	/// - Thumbnail: Extract frame at 2 seconds, resize to 320x180
	/// - Preview: First 30 seconds, compressed quality
	/// 
	/// ERROR HANDLING:
	/// - Retry 2 times with delays (30s, 60s)
	/// - If all retries fail, logs error but doesn't block
	/// - Thumbnails are nice-to-have, not critical
	/// </summary>
	public class ThumbnailGenerationJob
	{
		private readonly ICloudinaryService _cloudinaryService;
		private readonly ICommandRespository<ClassPreparationMedia> _mediaCommandRepo;
		private readonly ILogger<ThumbnailGenerationJob> _logger;

		public ThumbnailGenerationJob(ICloudinaryService cloudinaryService,ICommandRespository<ClassPreparationMedia> mediaCommandRepo,ILogger<ThumbnailGenerationJob> logger)
		{
			_cloudinaryService = cloudinaryService;
			_mediaCommandRepo = mediaCommandRepo;
			_logger = logger;
		}

		/// <summary>
		/// Execute thumbnail generation job
		/// 
		/// WORKFLOW:
		/// 1. Generate thumbnail URL (Cloudinary transformation)
		/// 2. Generate preview URL (first 30 seconds)
		/// 3. Update database with URLs
		/// 
		/// CLOUDINARY URLS:
		/// - Thumbnail: https://res.cloudinary.com/{cloud}/video/upload/so_2.0,w_320,h_180/{publicId}.jpg
		/// - Preview: https://res.cloudinary.com/{cloud}/video/upload/du_30,q_auto:low/{publicId}.mp4
		/// 
		/// NOTE:
		/// - These are transformation URLs (Cloudinary generates on-demand)
		/// - No actual API call needed, just build the URL
		/// - First request generates thumbnail, subsequent requests served from cache
		/// </summary>
		/// <param name="mediaId">Database record ID</param>
		/// <param name="publicId">Cloudinary public_id</param>
		/// <param name="schoolId">School ID (for logging context)</param>
		[Queue("default")]
		[AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 30, 60 })]
		public async Task Execute(Guid mediaId, string publicId, Guid schoolId)
		{
			try
			{
				_logger.LogInformation(
					"Starting thumbnail generation - MediaId: {MediaId}, PublicId: {PublicId}",
					mediaId,
					publicId);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 1: GENERATE THUMBNAIL URL
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// Use Cloudinary service to build transformation URL
				// This doesn't upload anything, just creates the URL pattern
				// Cloudinary generates thumbnail on first access

				var thumbnailUrl = _cloudinaryService.GenerateThumbnailUrl(
					publicId: publicId,
					width: 320,
					height: 180);

				_logger.LogDebug("Generated thumbnail URL: {URL}", thumbnailUrl);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 2: GENERATE PREVIEW URL (FIRST 30 SECONDS)
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// Preview URL for admin quick review
				// Format: https://res.cloudinary.com/{cloud}/video/upload/du_30,q_auto:low/{publicId}.mp4
				// du_30 = duration 30 seconds (first 30 seconds of video)
				// q_auto:low = low quality for faster streaming

				var previewUrl = GeneratePreviewUrl(publicId);

				_logger.LogDebug("Generated preview URL: {URL}", previewUrl);

				var updateDict = new Dictionary<string, object>
				{
					{ "ThumbnailUrl", thumbnailUrl },
					{ "PreviewUrl", previewUrl },
					{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
				};

				await _mediaCommandRepo.UpdateTableColumnById(
					updateDict,
					new KeyValuePair<string, object>("Id", mediaId));

				_logger.LogInformation(
					"Thumbnail generation completed - MediaId: {MediaId}",
					mediaId);
			}
			catch (Exception ex)
			{
				// Log error but don't throw (thumbnails are nice-to-have, not critical)
				_logger.LogError(
					ex,
					"Error generating thumbnails - MediaId: {MediaId}, PublicId: {PublicId}",
					mediaId,
					publicId);

				throw; // Rethrow to trigger Hangfire retry
			}
		}

		/// <summary>
		/// Generate preview URL for first 30 seconds of video
		/// 
		/// CLOUDINARY TRANSFORMATION:
		/// - du_30: Duration 30 seconds (takes first 30 seconds)
		/// - q_auto:low: Low quality for faster streaming
		/// - Returns as MP4
		/// 
		/// USED FOR:
		/// - Admin quick preview (don't need to download full file)
		/// - Teacher preview before submitting
		/// </summary>
		private string GeneratePreviewUrl(string publicId)
		{
			// Build Cloudinary transformation URL
			// This is a pattern, Cloudinary generates video on first request
			// Format: https://res.cloudinary.com/{cloud}/video/upload/du_30,q_auto:low/{publicId}.mp4

			// Note: You'll need cloudName from CloudinaryService
			// For now, returning a placeholder pattern
			// In real implementation, inject CloudinarySettings or pass cloudName

			return $"https://res.cloudinary.com/{{cloud}}/video/upload/du_30,q_auto:low/{publicId}.mp4";

			// TODO: Replace {{cloud}} with actual cloud name
			// Option 1: Inject IOptions<CloudinarySettings>
			// Option 2: Add GetCloudName() method to ICloudinaryService
			// Option 3: Pass cloudName as parameter to this job
		}
	}
}
