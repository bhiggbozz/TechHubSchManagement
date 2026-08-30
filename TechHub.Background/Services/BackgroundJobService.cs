using Hangfire;
using TechHub.Background.Jobs;
using TechHub.BackgroundJobs.Interfaces;
using TechHub.BackgroundJobs.Jobs;

namespace TechHub.BackgroundJobs.Services;

	public class BackgroundJobService : IBackgroundJobService
	{
		private readonly IRecurringJobManager _recurringJobManager;
		private readonly IBackgroundJobClient _backgroundJobClient;

		public BackgroundJobService(IRecurringJobManager recurringJobManager, IBackgroundJobClient backgroundJobClient)
		{
			_recurringJobManager = recurringJobManager;
			_backgroundJobClient = backgroundJobClient;
		}

	//	/// <summary>
	//	/// Enqueue media upload job
	//	/// </summary>
	//	public string EnqueueMediaUpload(Guid mediaId,string mediaKey,byte[] fileContent,string originalFileName,long originalFileSize,Guid schoolId,int mediaType)
	//	{
	//		// Enqueue job to 'default' queue with Hangfire
	//		// Returns immediately with job ID
	//		// Job executes in background worker thread

	//		var jobId = BackgroundJob.Enqueue<MediaUploadJob>(job =>
	//			job.ExecuteAsync(mediaId,mediaKey,fileContent,originalFileName,originalFileSize,schoolId,mediaType));

	//		return jobId;
	//	}

	//	/// <summary>
	//	/// Schedule recurring cleanup job
	//	/// </summary>
	//	public void ScheduleMediaCleanup()
	//	{
	//		// Schedule job to run daily at 2 AM
	//		RecurringJob.AddOrUpdate<MediaCleanupJob>("media-cleanup",
	//			job => job.ExecuteAsync(),
	//			Cron.Daily(2)); // 2 AM daily
	//	}

	/// <summary>
	/// Enqueue media upload job (server-side upload)
	/// </summary>
		public string EnqueueMediaUpload(Guid mediaId,string mediaKey,byte[] fileContent,string originalFileName,long originalFileSize,Guid schoolId,int mediaType)
		{
			var jobId = BackgroundJob.Enqueue<MediaUploadJob>(
				job => job.ExecuteAsync(mediaId,mediaKey,fileContent,originalFileName,originalFileSize,schoolId,mediaType));

			return jobId;
		}

		/// <summary>
		/// Schedule daily media cleanup job
		/// Runs at 2 AM every day
		/// </summary>
		public void ScheduleMediaCleanup()
		{
			_recurringJobManager.AddOrUpdate<MediaCleanupJob>("media-cleanup",
				job => job.ExecuteAsync(),
				Cron.Daily(2));  // 2 AM every day
		}

		/// <summary>
		/// NEW: Enqueue thumbnail generation job
		/// 
		/// JOB DETAILS:
		/// - Queue: "default" (medium priority)
		/// - Retry: 2 attempts (thumbnail generation can fail if video corrupted)
		/// - Delay: Immediate (enqueued right after upload)
		/// 
		/// WHAT IT DOES:
		/// 1. Extract thumbnail from video (frame at 2 seconds)
		/// 2. Generate preview clip (first 30 seconds)
		/// 3. Update database with thumbnail/preview URLs
		/// 
		/// CALLED AFTER:
		/// - ConfirmUpload (direct-to-CDN)
		/// - UploadComplete webhook (server-side)
		/// </summary>
		public string EnqueueThumbnailGeneration(Guid mediaId,string publicId,Guid schoolId)
		{
			var jobId = BackgroundJob.Enqueue<ThumbnailGenerationJob>(
				job => job.Execute(mediaId, publicId, schoolId));

			return jobId;
		}

	/// <summary>
	/// NEW: Enqueue AI content analysis job
	/// 
	/// JOB DETAILS:
	/// - Queue: "low" (low priority, can run later)
	/// - Retry: 1 attempt (AI analysis not critical, expensive to retry)
	/// - Delay: 2 minutes (let video processing finish first)
	/// 
	/// WHAT IT DOES:
	/// 1. Analyze video content for inappropriate material
	/// 2. Assess video/audio quality
	/// 3. Detect key moments (intro, main content, summary)
	/// 4. Extract topics/subjects
	/// 5. Update database with analysis results
	/// 
	/// CALLED AFTER:
	/// - ConfirmUpload (direct-to-CDN)
	/// - UploadComplete webhook (server-side)
	/// 
	/// NOTE: Currently implements basic analysis
	/// Can be enhanced with Azure Video Indexer or Cloudinary AI
	/// </summary>
		public string EnqueueAIContentAnalysis(Guid mediaId,string cdnUrl,decimal? duration)
		{
			// Schedule with 2-minute delay
			// Gives time for video processing to complete
			var jobId = BackgroundJob.Schedule<AIContentAnalysisJob>(
				job => job.Execute(mediaId, cdnUrl, duration),
				TimeSpan.FromMinutes(2));

			return jobId;
		}

		/// <summary>
		/// Enqueue auto image generation for an approved lesson.
		/// Uses the DI-based IBackgroundJobClient (never the static API).
		/// </summary>
		public string EnqueueLessonImageGeneration(Guid lessonId, Guid schoolId, Guid userId)
		{
			var jobId = _backgroundJobClient.Enqueue<LessonImageGenerationJob>(
				job => job.ExecuteAsync(lessonId, schoolId, userId));

			return jobId;
		}

		/// <summary>
		/// Schedule the daily cleanup of old ApplicationLogs rows.
		/// Runs at 3 AM every day. Uses the DI-based IRecurringJobManager.
		/// </summary>
		public void ScheduleApplicationLogsCleanup()
		{
			_recurringJobManager.AddOrUpdate<ApplicationLogsCleanupJob>("cleanup-application-logs",
				job => job.ExecuteAsync(),
				Cron.Daily(3));  // 3 AM every day
		}

		/// <summary>
		/// Schedule the daily sweep that marks stale InProgress quiz/assessment
		/// attempts Abandoned. Runs at 4 AM every day, staggered after the other
		/// two daily jobs (media-cleanup at 2 AM, log cleanup at 3 AM).
		/// </summary>
		public void ScheduleStaleAttemptCleanup()
		{
			_recurringJobManager.AddOrUpdate<StaleAttemptCleanupJob>("stale-attempt-cleanup",
				job => job.ExecuteAsync(),
				Cron.Daily(4));  // 4 AM every day
		}

	}

