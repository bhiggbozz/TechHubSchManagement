//namespace TechHub.BackgroundJobs.Interfaces;

//	/// <summary>
//	/// Service for enqueuing background jobs
//	/// Abstracts Hangfire implementation
//	/// </summary>
//	public interface IBackgroundJobService
//	{
//		/// <summary>
//		/// Enqueue media upload job to background queue
//		/// Returns immediately, processing happens asynchronously
//		/// </summary>
//		/// <param name="mediaId">Media record ID</param>
//		/// <param name="mediaKey">Unique media key</param>
//		/// <param name="fileContent">File bytes to upload</param>
//		/// <param name="originalFileName">Original file name</param>
//		/// <param name="originalFileSize">Original file size in bytes</param>
//		/// <param name="schoolId">School ID</param>
//		/// <param name="mediaType">Media type (Video, Image, Document, Audio)</param>
//		/// <returns>Job ID for tracking</returns>
//		string EnqueueMediaUpload(Guid mediaId,string mediaKey,byte[] fileContent,string originalFileName,long originalFileSize,Guid schoolId,int mediaType);

//		/// <summary>
//		/// Schedule recurring cleanup job for orphaned temp files
//		/// Runs daily at 2 AM
//		/// </summary>
//		void ScheduleMediaCleanup();
//	}
