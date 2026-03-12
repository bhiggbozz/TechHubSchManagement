using Hangfire;
using Serilog;
using Serilog.Context;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Enums;
using TechHub.Service.Interface;

namespace TechHub.BackgroundJobs.Jobs
{
	/// <summary>
	/// Recurring job to clean up temporary media files
	/// Runs daily at 2 AM
	/// </summary>
	public class MediaCleanupJob
	{
		private readonly IQueryRepository<ClassPreparationMedia> _mediaQueryRepo;
		private readonly ICommandRespository<ClassPreparationMedia> _mediaCommandRepo;
		private readonly ICloudinaryService _cloudinaryService;
		private readonly ILogger _logger;

		public MediaCleanupJob(
			IQueryRepository<ClassPreparationMedia> mediaQueryRepo,ICommandRespository<ClassPreparationMedia> mediaCommandRepo,ICloudinaryService cloudinaryService,
			ILogger logger)
		{
			_mediaQueryRepo = mediaQueryRepo;
			_mediaCommandRepo = mediaCommandRepo;
			_cloudinaryService = cloudinaryService;
			_logger = logger;
		}

		/// <summary>
		/// Execute cleanup job
		/// Deletes temp files older than 7 days
		/// </summary>
		[Queue("low")]
		[AutomaticRetry(Attempts = 2)]
		public async Task ExecuteAsync()
		{
			using (LogContext.PushProperty("JobType", "MediaCleanup"))
			{
				try
				{
					_logger.Information("Starting media cleanup job");

					
					// Criteria:
					// - IsTemporary = true
					// - Created more than 7 days ago
					// - UploadStatus = Completed (successfully uploaded)
					// - ClassPreparationId is NULL (not linked to any class)

					var cutoffDate = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss");

					var query = $@"
                        SELECT * FROM ClassPreparationMedia
                        WHERE IsTemporary = 1
                        AND ClassPreparationId IS NULL
                        AND UploadStatus = {(int)UploadStatus.Completed}
                        AND CreationDate < '{cutoffDate}'
                        AND IsDeleted = 0";

					var orphanedFiles = await _mediaQueryRepo.GetByQuery(query);

					if (!orphanedFiles.Any())
					{
						_logger.Information("No orphaned files found");
						return;
					}

					_logger.Information(
						"Found {Count} orphaned temp files to clean up",
						orphanedFiles.Count());


					int successCount = 0;
					int failCount = 0;

					foreach (var media in orphanedFiles)
					{
						try
						{
							// Delete from Cloudinary
							var deleted = await _cloudinaryService.DeleteMediaAsync(media.PublicId, (MediaType)media.MediaType);

							if (deleted)
							{
								var updateDict = new Dictionary<string, object>
								{
									{ "IsDeleted", true },
									{ "DeletedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
									{ "DeletionReason", "Automatic cleanup - orphaned temp file" },
									{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
								};

								var whereClause = new KeyValuePair<string, object>("Id", media.Id);
								await _mediaCommandRepo.UpdateTableColumnById(updateDict, whereClause);

								successCount++;

								_logger.Debug(
									"Deleted orphaned file - MediaId: {MediaId}, PublicId: {PublicId}",
									media.Id,
									media.PublicId);
							}
							else
							{
								failCount++;
								_logger.Warning(
									"Failed to delete from Cloudinary - MediaId: {MediaId}",
									media.Id);
							}
						}
						catch (Exception ex)
						{
							failCount++;
							_logger.Error(
								ex,
								"Error deleting orphaned file - MediaId: {MediaId}",
								media.Id);
						}
					}

					_logger.Information(
						"Media cleanup completed - Success: {Success}, Failed: {Failed}",
						successCount,
						failCount);
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Exception during media cleanup job");
					throw;
				}
			}
		}
	}
}