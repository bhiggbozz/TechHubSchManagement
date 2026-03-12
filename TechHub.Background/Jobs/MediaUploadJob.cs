using Hangfire;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Enums;
using TechHub.Service.Interface;

namespace TechHub.Background.Jobs;

	/// <summary>
	/// Background job for uploading media to Cloudinary
	/// Executes independently of HTTP requests
public class MediaUploadJob
{
	

	private readonly ICloudinaryService _cloudinaryService;
	private readonly ICommandRespository<ClassPreparationMedia> _mediaCommandRepo;
	private readonly ILogger _logger;

	public MediaUploadJob(
		ICloudinaryService cloudinaryService, ICommandRespository<ClassPreparationMedia> mediaCommandRepo, ILogger logger)
	{
		_cloudinaryService = cloudinaryService;
		_mediaCommandRepo = mediaCommandRepo;
		_logger = logger;
	}

	/// <summary>
	/// Execute media upload job
	/// </summary>
	[Queue("default")]
	[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
	public async Task ExecuteAsync(Guid mediaId,string mediaKey,byte[] fileContent,string originalFileName,long originalFileSize,
		Guid schoolId,int mediaType)
	{
		using (LogContext.PushProperty("JobType", "MediaUpload"))
		using (LogContext.PushProperty("MediaId", mediaId))
		using (LogContext.PushProperty("SchoolId", schoolId))
		{
			Stream? stream = null;

			try
			{
				_logger.LogInformation(
					"Starting media upload - MediaId: {MediaId}, FileName: {FileName}, Size: {Size}",
					mediaId,
					originalFileName,
					FormatFileSize(fileContent.Length));

				

				await UpdateUploadStatus(mediaId, UploadStatus.Uploading, null);


				stream = new MemoryStream(fileContent);

				var uploadResult = await _cloudinaryService.UploadMediaAsync(stream, mediaKey,schoolId,(MediaType)mediaType,isTemporary: true);

				if (!uploadResult.Success)
				{
					_logger.LogError(
						"Cloudinary upload failed - MediaId: {MediaId}, Error: {Error}",
						mediaId,
						uploadResult.ErrorMessage);

					await UpdateUploadStatus(mediaId,UploadStatus.Failed,uploadResult.ErrorMessage);

					throw new Exception($"Cloudinary upload failed: {uploadResult.ErrorMessage}");
				}


				var updateDict = new Dictionary<string, object>
					{
						{ "PublicId", uploadResult.PublicId },
						{ "CdnUrl", uploadResult.SecureUrl },
						{ "ThumbnailUrl", uploadResult.ThumbnailUrl ?? string.Empty },
						{ "FileSizeBytes", uploadResult.FileSizeBytes },
						{ "DurationSeconds", uploadResult.Duration > 0 ? (int)uploadResult.Duration : DBNull.Value },
						{ "UploadStatus", (int)UploadStatus.Completed },
						{ "UploadErrorMessage", DBNull.Value },
						{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
					};

				var whereClause = new KeyValuePair<string, object>("Id", mediaId);
				await _mediaCommandRepo.UpdateTableColumnById(updateDict, whereClause);

				var compressionRatio = originalFileSize > 0
					? (1 - ((double)uploadResult.FileSizeBytes / originalFileSize)) * 100 : 0;

				_logger.LogInformation(
					"Media upload completed - MediaId: {MediaId}, " +
					"OriginalSize: {OriginalSize}, CompressedSize: {CompressedSize}, " +
					"Compression: {Ratio:F1}%, Duration: {Duration}s",
					mediaId, FormatFileSize(originalFileSize), FormatFileSize(uploadResult.FileSizeBytes),
					compressionRatio,
					(int)uploadResult.Duration);
			}
			catch (Exception ex)
			{
				_logger.LogError(
					ex,
					"Exception during media upload - MediaId: {MediaId}",
					mediaId);

				await UpdateUploadStatus(mediaId,UploadStatus.Failed,ex.Message);

				throw;
			}
			finally
			{
				stream?.Dispose();

				if (fileContent.Length > 100 * 1024 * 1024)
				{
					_logger.LogDebug("Forcing garbage collection after large file upload");
					GC.Collect(2, GCCollectionMode.Forced, blocking: false);
				}
			}
		}
	}

	private async Task UpdateUploadStatus(Guid mediaId, UploadStatus status, string? errorMessage)
	{
		try
		{
			var updateDict = new Dictionary<string, object>
				{
					{ "UploadStatus", (int)status },
					{ "UploadErrorMessage", errorMessage ?? (object)DBNull.Value },
					{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
				};

			var whereClause = new KeyValuePair<string, object>("Id", mediaId);
			await _mediaCommandRepo.UpdateTableColumnById(updateDict, whereClause);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,"Error updating upload status - MediaId: {MediaId}, Status: {Status}", mediaId, status);
		}
	}

	private string FormatFileSize(long bytes)
	{
		if (bytes == 0) return "0 B";

		string[] sizes = { "B", "KB", "MB", "GB" };
		double len = bytes;
		int order = 0;

		while (len >= 1024 && order < sizes.Length - 1)
		{
			order++;
			len = len / 1024;
		}

		return $"{len:0.##} {sizes[order]}";
	}
}
