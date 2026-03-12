using Hangfire;
using TechHub.Background.Jobs;
using TechHub.BackgroundJobs.Interfaces;
using TechHub.BackgroundJobs.Jobs;

namespace TechHub.BackgroundJobs.Services
{
	public class BackgroundJobService : IBackgroundJobService
	{
		/// <summary>
		/// Enqueue media upload job
		/// </summary>
		public string EnqueueMediaUpload(Guid mediaId,string mediaKey,byte[] fileContent,string originalFileName,long originalFileSize,Guid schoolId,int mediaType)
		{
			// Enqueue job to 'default' queue with Hangfire
			// Returns immediately with job ID
			// Job executes in background worker thread

			var jobId = BackgroundJob.Enqueue<MediaUploadJob>(job =>
				job.ExecuteAsync(mediaId,mediaKey,fileContent,originalFileName,originalFileSize,schoolId,mediaType));

			return jobId;
		}

		/// <summary>
		/// Schedule recurring cleanup job
		/// </summary>
		public void ScheduleMediaCleanup()
		{
			// Schedule job to run daily at 2 AM
			RecurringJob.AddOrUpdate<MediaCleanupJob>("media-cleanup",
				job => job.ExecuteAsync(),
				Cron.Daily(2)); // 2 AM daily
		}
	}
}