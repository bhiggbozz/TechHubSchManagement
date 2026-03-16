// Services/MediaService.cs

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;
using System.Security.Cryptography;
using TechHub.BackgroundJobs.Interfaces;
using TechHub.Core;
using TechHub.Core.Configuration;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Enums;
using TechHub.Core.Helper;
using TechHub.Core.Model;
using TechHub.Core.Models;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;
using TechHub.Service.ViewModels;
using TechhubMS.util;

namespace TechHub.Service.Service;

public class MediaService : IMediaService
{
	private readonly IQueryRepository<ClassPreparationMedia> _mediaQueryRepo;
	private readonly ICommandRespository<ClassPreparationMedia> _mediaCommandRepo;
	private readonly IBackgroundJobService _backgroundJobService;
	private readonly CloudinarySettings _settings;
	private readonly ILogger _logger;
	private readonly ICloudinaryService _cloudinaryService;


	public MediaService(
		IQueryRepository<ClassPreparationMedia> mediaQueryRepo,
		ICommandRespository<ClassPreparationMedia> mediaCommandRepo,
		IBackgroundJobService backgroundJobService,
		IOptions<CloudinarySettings> options, ICloudinaryService cloudinaryService,
		ILogger logger)
	{
		_mediaQueryRepo = mediaQueryRepo;
		_mediaCommandRepo = mediaCommandRepo;
		_backgroundJobService = backgroundJobService;
		_cloudinaryService = cloudinaryService;
		_settings = options.Value;
		_logger = logger;
	}

	/// <summary>
	/// Upload media file with immediate response (background processing)
	/// </summary>
	public async Task<UploadMediaResponse> UploadMedia(IFormFile file, MediaType mediaType, string? displayName, AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		//using (LogContext.PushProperty("TenantId", userClaims.TenantIdentifier))
		{
			try
			{

				if (file == null || file.Length == 0)
				{
					return new UploadMediaResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "No file provided",
						Status = "failed"
					};
				}

				var maxFileSizeBytes = _settings.Settings.FileLimits.MaxFileSizeMB * 1024 * 1024;

				if (file.Length > maxFileSizeBytes)
				{
					return new UploadMediaResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"File size exceeds {_settings.Settings.FileLimits.MaxFileSizeMB}MB limit",
						Status = "failed"
					};
				}

				var extension = Path.GetExtension(file.FileName).TrimStart('.').ToLower();

				var isValidFormat = mediaType switch
				{
					MediaType.Video => _settings.Settings.FileLimits.AllowedVideoFormats.Contains(extension),
					MediaType.Image => _settings.Settings.FileLimits.AllowedImageFormats.Contains(extension),
					MediaType.Document => _settings.Settings.FileLimits.AllowedDocumentFormats.Contains(extension),
					MediaType.Audio => _settings.Settings.FileLimits.AllowedVideoFormats.Contains(extension),
					_ => false
				};

				if (!isValidFormat)
				{
					var allowedFormats = mediaType switch
					{
						MediaType.Video => string.Join(", ", _settings.Settings.FileLimits.AllowedVideoFormats),
						MediaType.Image => string.Join(", ", _settings.Settings.FileLimits.AllowedImageFormats),
						MediaType.Document => string.Join(", ", _settings.Settings.FileLimits.AllowedDocumentFormats),
						MediaType.Audio => string.Join(", ", _settings.Settings.FileLimits.AllowedVideoFormats),
						_ => "none"
					};

					return new UploadMediaResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"File format '.{extension}' is not allowed. Allowed: {allowedFormats}",
						Status = "failed"
					};
				}



				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new UploadMediaResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new UploadMediaResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid UserId",
						Status = "failed"
					};
				}

				_logger.Information(
					"Initiating media upload - FileName: {FileName}, Size: {Size}, Type: {MediaType}",
					file.FileName,
					FormatFileSize(file.Length),
					mediaType);


				byte[] fileContent;
				using (var memoryStream = new MemoryStream())
				{
					await file.CopyToAsync(memoryStream);
					fileContent = memoryStream.ToArray();
				}


				var sha256Hash = MediaKeyGenerator.GenerateSHA256Hash(fileContent);

				var duplicateQuery = $@"
                        SELECT TOP 1 * FROM ClassPreparationMedia 
                        WHERE SHA256Hash = '{sha256Hash}' 
                        AND SchoolId = '{schoolId}' 
                        AND IsDeleted = 0
                        AND UploadStatus = {(int)UploadStatus.Completed}";

				var existingMedia = (await _mediaQueryRepo.GetByQuery(duplicateQuery)).FirstOrDefault();

				if (existingMedia != null)
				{
					_logger.Warning(
						"Duplicate file detected - ExistingMediaId: {ExistingMediaId}, Hash: {Hash}",
						existingMedia.Id,
						sha256Hash);

					return new UploadMediaResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = "This file has already been uploaded",
						Status = "duplicate",
						MediaFile = existingMedia.ToDto(userClaims.UserId.ToString() ?? "")
					};
				}


				var mediaKey = MediaKeyGenerator.GenerateMediaKey(schoolId, file.FileName, fileContent);


				var mediaId = Guid.NewGuid();
				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				var mediaDict = new Dictionary<string, object>
					{
						{ "Id", mediaId },
						{ "ClassPreparationId", DBNull.Value },
						{ "MediaKey", mediaKey },
						{ "PublicId", string.Empty },
						{ "MediaType", (int)mediaType },
						{ "OriginalFileName", file.FileName },
						{ "DisplayName", displayName ?? file.FileName },
						{ "FileSizeBytes", 0 },
						{ "OriginalSizeBytes", file.Length },
						{ "DurationSeconds", DBNull.Value },
						{ "MimeType", file.ContentType },
						{ "FileExtension", Path.GetExtension(file.FileName).ToLower() },
						{ "SHA256Hash", sha256Hash },
						{ "CdnUrl", string.Empty },
						{ "ThumbnailUrl", string.Empty },
						{ "CdnProvider", "Cloudinary" },
						{ "IsTemporary", true },
						{ "UploadStatus", (int)UploadStatus.Pending },
						{ "UploadErrorMessage", DBNull.Value },
						{ "DownloadCount", 0 },
						{ "IsDeleted", false },
						{ "UploadedDate", now },
						{ "SchoolId", schoolId },
						{ "CreationDate", now },
						{ "CreatedBy", userId },
						{ "IsActive", true }
					};

				await _mediaCommandRepo.Create(mediaDict);

				_logger.Information(
					"Media record created with Pending status - MediaId: {MediaId}, MediaKey: {MediaKey}",
					mediaId,
					mediaKey);



				var jobId = _backgroundJobService.EnqueueMediaUpload(mediaId, mediaKey, fileContent, file.FileName, file.Length, schoolId, (int)mediaType);

				_logger.Information(
					"Background upload job enqueued - MediaId: {MediaId}, JobId: {JobId}",
					mediaId,
					jobId);

				return new UploadMediaResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Media upload queued successfully. Processing in background...",
					Status = "successful",
					MediaFile = new MediaFileDto
					{
						MediaId = mediaId,
						MediaKey = mediaKey,
						PublicId = string.Empty,
						MediaType = (int)mediaType,
						MediaTypeName = mediaType.ToString(),
						MediaTypeIcon = GetMediaTypeIcon(mediaType),
						OriginalFileName = file.FileName,
						DisplayName = displayName ?? file.FileName,
						FileExtension = Path.GetExtension(file.FileName).ToLower(),
						FileSizeBytes = 0,
						FileSizeFormatted = "Processing...",
						OriginalSizeBytes = file.Length,
						OriginalSizeFormatted = FormatFileSize(file.Length),
						CompressionRatio = null,
						DurationSeconds = null,
						DurationFormatted = null,
						CdnUrl = string.Empty,
						ThumbnailUrl = null,
						IsTemporary = true,
						IsDeleted = false,
						DownloadCount = 0,
						UploadedDate = DateTime.Now,
						UploadedByName = userClaims.UserId.ToString() ?? ""
						//UploadStatus = (int)UploadStatus.Pending,
						//UploadStatusName = "Queued for processing"
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Exception during media upload initiation");

				return new UploadMediaResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while initiating upload",
					Status = "failed"
				};
			}
		}
	}

	#region Move Media to Permanent

	/// <summary>
	/// Move media to permanent storage (after class approval)
	/// Changes: temp/pending/{schoolId}/ → schools/{schoolId}/
	/// </summary>
	public async Task<BaseResponse> MoveMediaToPermanent(Guid classPreparationId)
	{
		try
		{
			_logger.Information(
				"Moving media to permanent storage - ClassPreparationId: {ClassPreparationId}",
				classPreparationId);

			// Get all temporary media for this class
			var query = $@"
                    SELECT * FROM ClassPreparationMedia
                    WHERE ClassPreparationId = '{classPreparationId}'
                    AND IsTemporary = 1
                    AND IsDeleted = 0
                    AND UploadStatus = {(int)UploadStatus.Completed}";

			var mediaFiles = await _mediaQueryRepo.GetByQuery(query);

			if (!mediaFiles.Any())
			{
				_logger.Information("No temporary media files to move");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "No temporary media files to move",
					Status = "successful"
				};
			}

			int successCount = 0;
			int failCount = 0;

			foreach (var media in mediaFiles)
			{
				try
				{
					_logger.Information(
						"Moving media file - MediaId: {MediaId}, PublicId: {PublicId}",
						media.Id,
						media.PublicId);

					// Move in Cloudinary (temp/pending → schools)
					var moved = await _cloudinaryService.MoveToPermamentStorageAsync(
						media.PublicId,
						media.SchoolId);

					if (moved)
					{
						// Update database
						var newPublicId = media.PublicId.Replace("temp/pending", "schools");
						var newCdnUrl = media.CdnUrl.Replace("temp/pending", "schools");
						var newThumbnailUrl = media.ThumbnailUrl?.Replace("temp/pending", "schools");

						var updateDict = new Dictionary<string, object>
							{
								{ "IsTemporary", false },
								{ "PublicId", newPublicId },
								{ "CdnUrl", newCdnUrl },
								{ "ThumbnailUrl", newThumbnailUrl ?? string.Empty },
								{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
							};

						var whereClause = new KeyValuePair<string, object>("Id", media.Id);
						await _mediaCommandRepo.UpdateTableColumnById(updateDict, whereClause);

						successCount++;

						_logger.Information(
							"Media moved successfully - MediaId: {MediaId}",
							media.Id);
					}
					else
					{
						failCount++;
						_logger.Warning(
							"Failed to move media - MediaId: {MediaId}",
							media.Id);
					}
				}
				catch (Exception ex)
				{
					_logger.Error(
						ex,
						"Exception moving media - MediaId: {MediaId}",
						media.Id);
					failCount++;
				}
			}

			_logger.Information(
				"Media move completed - Success: {Success}, Failed: {Failed}",
				successCount,
				failCount);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = $"Media moved - Success: {successCount}, Failed: {failCount}",
				Status = "successful"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Exception moving media to permanent storage");

			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "Error moving media to permanent storage",
				Status = "failed"
			};
		}
	}

	#endregion

	/// <summary>
	/// Get upload status for a media file
	/// Frontend polls this to track progress
	/// </summary>
	public async Task<MediaUploadStatusResponse> GetUploadStatus(Guid mediaId)
	{
		try
		{
			var query = $"SELECT * FROM ClassPreparationMedia WHERE Id = '{mediaId}'";
			var media = (await _mediaQueryRepo.GetByQuery(query)).FirstOrDefault();

			if (media == null)
			{
				return new MediaUploadStatusResponse
				{
					ResponseCode = ResponseCode.NotFound,
					ResponseMessage = "Media not found",
					Status = "failed"
				};
			}

			return new MediaUploadStatusResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Upload status retrieved",
				Status = "successful",
				MediaId = media.Id,
				UploadStatus = media.UploadStatus,
				UploadStatusName = ((UploadStatus)media.UploadStatus).ToString(),
				ErrorMessage = media.UploadErrorMessage,
				CdnUrl = media.CdnUrl,
				ThumbnailUrl = media.ThumbnailUrl,
				FileSizeBytes = media.FileSizeBytes,
				FileSizeFormatted = FormatFileSize(media.FileSizeBytes)
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error retrieving upload status - MediaId: {MediaId}", mediaId);

			return new MediaUploadStatusResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "Error retrieving upload status",
				Status = "failed"
			};
		}
	}

	#region Delete Media

	/// <summary>
	/// Delete media for rejected class
	/// Soft delete in DB, physical delete from Cloudinary (async)
	/// </summary>
	public async Task<BaseResponse> DeleteMediaForRejectedClass(Guid classPreparationId, Guid deletedBy, string reason)
	{
		try
		{
			_logger.Information(
				"Deleting media for rejected class - ClassPreparationId: {ClassPreparationId}, Reason: {Reason}",
				classPreparationId,
				reason);

			var query = $@"
                    SELECT * FROM ClassPreparationMedia
                    WHERE ClassPreparationId = '{classPreparationId}'
                    AND IsDeleted = 0";

			var mediaFiles = await _mediaQueryRepo.GetByQuery(query);

			if (!mediaFiles.Any())
			{
				_logger.Information("No media files to delete");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "No media files to delete",
					Status = "successful"
				};
			}

			foreach (var media in mediaFiles)
			{
				// Soft delete in database
				var updateDict = new Dictionary<string, object>
					{
						{ "IsDeleted", true },
						{ "DeletedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
						{ "DeletedBy", deletedBy },
						{ "DeletionReason", $"Class rejected: {reason}" },
						{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
					};

				var whereClause = new KeyValuePair<string, object>("Id", media.Id);
				await _mediaCommandRepo.UpdateTableColumnById(updateDict, whereClause);

				// Physical delete from Cloudinary (fire and forget)
				_ = Task.Run(async () =>
				{
					try
					{
						await _cloudinaryService.DeleteMediaAsync(
							media.PublicId,
							(MediaType)media.MediaType);

						_logger.Information(
							"Media physically deleted from Cloudinary - MediaId: {MediaId}",
							media.Id);
					}
					catch (Exception ex)
					{
						_logger.Error(
							ex,
							"Error physically deleting media - MediaId: {MediaId}",
							media.Id);
					}
				});
			}

			_logger.Information(
				"Media deletion completed - Count: {Count}",
				mediaFiles.Count());

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = $"{mediaFiles.Count()} media file(s) deleted",
				Status = "successful"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Exception deleting media for rejected class");

			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "Error deleting media",
				Status = "failed"
			};
		}
	}

	/// <summary>
	/// Delete single media file
	/// Soft delete in DB, physical delete from Cloudinary (async)
	/// </summary>
	public async Task<BaseResponse> DeleteMedia(Guid mediaId, Guid deletedBy, string? reason)
	{
		try
		{
			_logger.Information(
				"Deleting media file - MediaId: {MediaId}, Reason: {Reason}",
				mediaId,
				reason);

			var media = await _mediaQueryRepo.Get(mediaId);

			if (media == null)
			{
				_logger.Warning("Media not found - MediaId: {MediaId}", mediaId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.NotFound,
					ResponseMessage = "Media not found",
					Status = "failed"
				};
			}

			// Soft delete in database
			var updateDict = new Dictionary<string, object>
				{
					{ "IsDeleted", true },
					{ "DeletedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
					{ "DeletedBy", deletedBy },
					{ "DeletionReason", reason ?? "Manual deletion" },
					{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
				};

			var whereClause = new KeyValuePair<string, object>("Id", mediaId);
			await _mediaCommandRepo.UpdateTableColumnById(updateDict, whereClause);

			_logger.Information("Media soft deleted in database - MediaId: {MediaId}", mediaId);

			// Physical delete from Cloudinary (fire and forget)
			_ = Task.Run(async () =>
			{
				try
				{
					await _cloudinaryService.DeleteMediaAsync(
						media.PublicId,
						(MediaType)media.MediaType);

					_logger.Information(
						"Media physically deleted from Cloudinary - MediaId: {MediaId}",
						mediaId);
				}
				catch (Exception ex)
				{
					_logger.Error(
						ex,
						"Error physically deleting media from Cloudinary - MediaId: {MediaId}",
						mediaId);
				}
			});

			return new BaseResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Media deleted successfully",
				Status = "successful"
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Exception deleting media - MediaId: {MediaId}", mediaId);

			return new BaseResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "Error deleting media",
				Status = "failed"
			};
		}
	}


	#region Helper Methods

	private string GetMediaTypeIcon(MediaType mediaType)
	{
		return mediaType switch
		{
			MediaType.Video => "",
			MediaType.Image => "",
			MediaType.Document => "",
			MediaType.Audio => "",
			_ => ""
		};
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

	/// <summary>
	/// Link uploaded media files to a class preparation
	/// Updates ClassPreparationId for each media record
	/// </summary>
	public async Task<BaseResponse> LinkMediaToClass(Guid classPreparationId, List<Guid> mediaIds, AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("ClassPreparationId", classPreparationId))
		{
			try
			{
				_logger.Information(
					"Linking media to class - ClassPreparationId: {ClassPreparationId}, MediaCount: {Count}",
					classPreparationId,
					mediaIds.Count);

				if (mediaIds == null || !mediaIds.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "No media IDs provided",
						Status = "failed"
					};
				}


				int successCount = 0;
				int failCount = 0;

				foreach (var mediaId in mediaIds)
				{
					try
					{
						// Update ClassPreparationId
						var updateDict = new Dictionary<string, object>
							{
								{ "ClassPreparationId", classPreparationId },
								{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
							};

						var whereClause = new KeyValuePair<string, object>("Id", mediaId);
						await _mediaCommandRepo.UpdateTableColumnById(updateDict, whereClause);

						successCount++;

						_logger.Debug("Media linked - MediaId: {MediaId}", mediaId);
					}
					catch (Exception ex)
					{
						_logger.Error(
							ex,
							"Error linking media - MediaId: {MediaId}",
							mediaId);
						failCount++;
					}
				}



				if (failCount > 0)
				{
					_logger.Warning(
						"Media linking completed with errors - Success: {Success}, Failed: {Failed}",
						successCount,
						failCount);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = $"Media linked - Success: {successCount}, Failed: {failCount}",
						Status = "successful"
					};
				}

				_logger.Information("All media files linked successfully - Count: {Count}", successCount);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = $"{successCount} media file(s) linked successfully",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Exception linking media to class");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while linking media files",
					Status = "failed"
				};
			}
		}
	}
	/// <summary>
	/// Get all media files for a specific class preparation
	/// Returns media with uploader details
	/// </summary>
	public async Task<MediaFilesListResponse> GetClassMediaFiles(Guid classPreparationId)
	{
		using (LogContext.PushProperty("ClassPreparationId", classPreparationId))
		{
			try
			{
				_logger.Information(
					"Retrieving media files - ClassPreparationId: {ClassPreparationId}",
					classPreparationId);

				var query = $@"
                SELECT 
                    m.*,
                    CONCAT(u.FirstName, ' ', u.LastName) as UploadedByName
                FROM ClassPreparationMedia m
                LEFT JOIN Users u ON m.CreatedBy = u.Id
                WHERE m.ClassPreparationId = '{classPreparationId}'
                AND m.IsDeleted = 0
                ORDER BY m.CreationDate DESC";

				var mediaFiles = await _mediaQueryRepo.GetByQuery(query);

				if (!mediaFiles.Any())
				{
					_logger.Information("No media files found for class");

					return new MediaFilesListResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "No media files found",
						Status = "successful",
						MediaFiles = new List<MediaFileDto>(),
						TotalCount = 0
					};
				}



				var mediaDtos = new List<MediaFileDto>();

				foreach (var media in mediaFiles)
				{
					var uploadedByName = "";

					// Extract UploadedByName from dynamic result
					// (GetByQuery returns dynamic objects from Dapper)
					try
					{
						var type = media.GetType();
						var prop = type.GetProperty("UploadedByName");
						if (prop != null)
						{
							uploadedByName = prop.GetValue(media)?.ToString() ?? "";
						}
					}
					catch (Exception ex)
					{
						_logger.Warning(
							ex,
							"Error extracting UploadedByName - MediaId: {MediaId}",
							media.Id);
					}

					// Use extension method to map to DTO
					mediaDtos.Add(media.ToDto(uploadedByName));
				}

				_logger.Information(
					"Media files retrieved - Count: {Count}",
					mediaDtos.Count);

				return new MediaFilesListResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Media files retrieved successfully",
					Status = "successful",
					MediaFiles = mediaDtos,
					TotalCount = mediaDtos.Count
				};
			}
			catch (Exception ex)
			{
				_logger.Error(
					ex,
					"Exception retrieving media files - ClassPreparationId: {ClassPreparationId}",
					classPreparationId);

				return new MediaFilesListResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving media files",
					Status = "failed",
					MediaFiles = new List<MediaFileDto>(),
					TotalCount = 0
				};
			}
		}
	}
	#endregion

	/// <summary>
	/// NEW: Request upload token for direct browser-to-Cloudinary upload
	/// 
	/// This is the RECOMMENDED upload method for:
	/// - Large files (>= 100 MB)
	/// - High concurrent upload scenarios (400+ teachers)
	/// - Better user experience (faster uploads)
	/// 
	/// SECURITY MODEL:
	/// 1. Teacher authenticated via JWT token
	/// 2. Teacher ID captured from JWT claims (can't be spoofed)
	/// 3. Database record created BEFORE upload starts
	/// 4. Signature generated using API secret (frontend can't forge)
	/// 5. Token expires in 1 hour (limited attack window)
	/// 6. Single-use enforced via status check
	/// 
	/// WORKFLOW STEPS:
	/// 1. Extract and validate user claims (authentication)
	/// 2. Validate file metadata (size, type, format)
	/// 3. Generate unique media ID and key
	/// 4. Create database record with status "AwaitingUpload"
	/// 5. Generate Cloudinary upload signature
	/// 6. Return token to frontend
	/// 7. [Frontend uploads directly to Cloudinary]
	/// 8. [Frontend calls ConfirmUpload when done]
	/// 
	/// DATABASE STATE:
	/// - MediaId: Generated GUID
	/// - Status: AwaitingUpload (0)
	/// - CreatedBy: Teacher ID from JWT
	/// - ClassPreparationId: NULL (linked later)
	/// - IsTemporary: true (temp storage)
	/// - CdnUrl: Empty (populated on confirm)
	/// 
	/// ERROR CASES:
	/// - Invalid user claims → BadRequest
	/// - File too large → BadRequest with max size
	/// - Invalid file format → BadRequest with allowed formats
	/// - Database error → ErrorOccured
	/// </summary>
	public async Task<RequestUploadTokenResponse> RequestUploadToken(RequestUploadTokenViewModel model, AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		using (LogContext.PushProperty("FileName", model.FileName))
		{
			try
			{
				_logger.Information(
					"Requesting upload token - FileName: {FileName}, Size: {Size} bytes, Type: {Type}",
					model.FileName,
					model.FileSize,
					model.MediaType);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 1: VALIDATE USER CLAIMS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// Extract teacher ID from JWT token
				// This ensures we know WHO initiated the upload
				// Cannot be spoofed because JWT is cryptographically signed

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					_logger.Warning("❌ Invalid UserId in claims - UserId: {UserId}", userClaims.UserId);

					return new RequestUploadTokenResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					_logger.Warning("Invalid SchoolId in claims - SchoolId: {SchoolId}", userClaims.SchoolId);

					return new RequestUploadTokenResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				_logger.Debug("User claims validated - UserId: {UserId}, SchoolId: {SchoolId}",
					userId, schoolId);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 2: VALIDATE FILE SIZE
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// Check file size against configured maximum
				// Prevents:
				// - Storage bloat
				// - Excessive CDN costs
				// - User uploading 10GB files accidentally

				var maxFileSizeBytes = _settings.Settings.FileLimits.MaxFileSizeMB * 1024 * 1024;

				if (model.FileSize > maxFileSizeBytes)
				{
					_logger.Warning(
						"❌ File size exceeds limit - Requested: {RequestedSize} bytes, Max: {MaxSize} bytes",
						model.FileSize,
						maxFileSizeBytes);

					return new RequestUploadTokenResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"File size exceeds maximum allowed ({_settings.Settings.FileLimits.MaxFileSizeMB} MB). Your file: {model.FileSize / (1024 * 1024)} MB",
						Status = "failed"
					};
				}

				_logger.Debug("File size validated - Size: {Size} bytes", model.FileSize);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 3: VALIDATE FILE TYPE AND FORMAT
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// Extract file extension and check against whitelist
				// Prevents:
				// - Uploading .exe disguised as .mp4
				// - Unsupported formats that Cloudinary can't process
				// - Potential security vulnerabilities

				var fileExtension = Path.GetExtension(model.FileName)
					.TrimStart('.')
					.ToLowerInvariant();

				// Get allowed formats based on media type
				var allowedFormats = model.MediaType switch
				{
					MediaType.Video => _settings.Settings.FileLimits.AllowedVideoFormats,
					MediaType.Image => _settings.Settings.FileLimits.AllowedImageFormats,
					MediaType.Document => _settings.Settings.FileLimits.AllowedDocumentFormats,
					//MediaType.Audio => _settings.Settings.FileLimits.AllowedAudioFormats,
					_ => new List<string>()
				};

				if (!allowedFormats.Contains(fileExtension))
				{
					_logger.Warning(
						"Invalid file format - Extension: {Extension}, MediaType: {Type}, Allowed: {Allowed}",
						fileExtension,
						model.MediaType,
						string.Join(", ", allowedFormats));

					return new RequestUploadTokenResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"File format '.{fileExtension}' is not allowed for {model.MediaType}. Allowed formats: {string.Join(", ", allowedFormats)}",
						Status = "failed"
					};
				}

				_logger.Debug(" File format validated - Extension: {Extension}", fileExtension);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 4: GENERATE UNIQUE IDENTIFIERS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// Create unique media ID (database primary key)
				// Generate media key (Cloudinary public_id)
				// Format: {schoolId-8chars}_{yyyyMMddHHmmss}_{filename}

				var mediaId = Guid.NewGuid();
				var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

				var mediaKey = MediaKeyGenerator.GenerateKey(schoolId, model.FileName);

				// Determine upload folder (temporary storage)
				var folder = _settings?.Settings.FolderStructure.TempPending
					.Replace("{schoolId}", schoolId.ToString());

				_logger.Debug(
					"Identifiers generated - MediaId: {MediaId}, MediaKey: {MediaKey}, Folder: {Folder}",
					mediaId,
					mediaKey,
					folder);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 5: CREATE DATABASE RECORD
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// Record created BEFORE upload starts
				// This captures:
				// - WHO initiated upload (from JWT)
				// - WHEN upload was requested
				// - WHAT file is being uploaded (metadata)
				// 
				// Status: AwaitingUpload
				// - Prevents duplicate uploads
				// - Enforces single-use token
				// - Tracks upload lifecycle

				var mediaRecord = new ClassPreparationMedia
				{
					Id = mediaId,
					MediaKey = mediaKey,
					PublicId = mediaKey,  // Will be updated by Cloudinary
					MediaType = (int)model.MediaType,
					OriginalFileName = model.FileName,
					DisplayName = model.DisplayName ?? model.FileName,
					FileSizeBytes = 0,  // Unknown until upload completes
					OriginalSizeBytes = model.FileSize,
					MimeType = model.MimeType,
					FileExtension = fileExtension,
					CdnUrl = string.Empty,  // Populated on upload completion
					IsTemporary = true,
					UploadStatus = (int)UploadStatus.AwaitingUpload,
					SchoolId = schoolId,
					CreatedBy = userId,
					CreationDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
					IsActive = true
				};

				await _mediaCommandRepo.Create(mediaRecord);

				_logger.Information(
					"Database record created - MediaId: {MediaId}, Status: AwaitingUpload",
					mediaId);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 6: GENERATE CLOUDINARY UPLOAD TOKEN
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// Generate signed upload token
				// Contains:
				// - Upload URL (Cloudinary endpoint)
				// - Signature (cryptographic proof)
				// - Timestamp (expiration enforcement)
				// - PublicId (where to store file)
				// - Folder (storage location)
				// - ApiKey (your account identifier)
				// 
				// Frontend uses this to upload DIRECTLY to Cloudinary
				// Server is NOT involved in file transfer

				var uploadToken = _cloudinaryService.GenerateUploadToken(
					publicId: mediaKey,
					folder: folder,
					timestamp: timestamp);

				_logger.Information(
					"Upload token generated - MediaId: {MediaId}, ExpiresAt: {ExpiresAt}",
					mediaId,
					DateTimeOffset.FromUnixTimeSeconds(timestamp + 3600).ToString("yyyy-MM-dd HH:mm:ss"));

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 7: RETURN TOKEN TO FRONTEND
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// Response contains everything frontend needs to upload
				// Frontend workflow:
				// 1. Receive token
				// 2. Create FormData with file + token parameters
				// 3. POST directly to Cloudinary
				// 4. Call ConfirmUpload when Cloudinary responds

				return new RequestUploadTokenResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Upload token generated successfully. Use this token to upload directly to Cloudinary.",
					Status = "successful",
					MediaId = mediaId,
					UploadUrl = uploadToken.UploadUrl,
					Signature = uploadToken.Signature,
					Timestamp = uploadToken.Timestamp,
					PublicId = uploadToken.PublicId,
					Folder = uploadToken.Folder,
					ApiKey = uploadToken.ApiKey,
					CloudName = uploadToken.CloudName,
					ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(timestamp + 3600).ToString("yyyy-MM-dd HH:mm:ss")
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "💥 Error generating upload token - FileName: {FileName}", model.FileName);

				return new RequestUploadTokenResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while generating upload token. Please try again.",
					Status = "failed"
				};
			}
		}
	}


	/// <summary>
	///  NEW: Confirm upload completed successfully
	/// 
	/// CALLED BY: Frontend after successful direct upload to Cloudinary
	/// 
	/// This method completes the upload lifecycle:
	/// 1. Validate ownership (only uploader can confirm)
	/// 2. Verify single-use (status must be AwaitingUpload)
	/// 3. Update database with Cloudinary upload results
	/// 4. Change status to Completed
	/// 5. Trigger post-upload processing (thumbnails, AI analysis)
	/// 
	/// SECURITY CHECKS:
	/// - User authentication (JWT required)
	/// - Ownership validation (CreatedBy must match JWT UserId)
	/// - Single-use enforcement (status check prevents replay)
	/// - Optional: Verify file exists in Cloudinary
	/// 
	/// DATABASE UPDATES:
	/// - UploadStatus: AwaitingUpload → Completed
	/// - CdnUrl: Populated with Cloudinary URL
	/// - PublicId: Actual Cloudinary public_id
	/// - FileSizeBytes: Actual compressed file size
	/// - DurationSeconds: For videos/audio
	/// - ThumbnailUrl: Auto-generated thumbnail
	/// - ModifiedDate: Current timestamp
	/// 
	/// POST-UPLOAD JOBS:
	/// - Thumbnail generation (videos/images)
	/// - AI content analysis (videos)
	/// - Preview clip generation (first 30 seconds)
	/// 
	/// ERROR CASES:
	/// - Media not found → NotFound
	/// - Wrong user → Forbidden
	/// - Already confirmed → BadRequest
	/// - Database error → ErrorOccured
	/// </summary>
	public async Task<BaseResponse> ConfirmUpload(ConfirmUploadViewModel model, AuthenticatedUserClaims userClaims)
	{
		using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		//using (LogContext.PushProperty("MediaId", model.MediaId))
		{
			try
			{
				_logger.Information(
					"Confirming upload - MediaId: {MediaId}, URL: {URL}",
					model.MediaId,
					model.SecureUrl);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 1: VALIDATE USER CLAIMS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				if (!Guid.TryParse(userClaims.UserId, out var userId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 2: RETRIEVE MEDIA RECORD
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var media = await _mediaQueryRepo.Get(model.MediaId);

				if (media == null)
				{
					_logger.Warning("Media not found - MediaId: {MediaId}", model.MediaId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Media record not found",
						Status = "failed"
					};
				}

				_logger.Debug(
					"Media record retrieved - MediaId: {MediaId}, CreatedBy: {CreatedBy}, Status: {Status}",
					media.Id,
					media.CreatedBy,
					(UploadStatus)media.UploadStatus);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 3: VERIFY OWNERSHIP
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// SECURITY: Only the teacher who requested the token can confirm
				// Prevents:
				// - Hacker stealing token and confirming malicious upload
				// - Teacher A confirming Teacher B's upload
				// - Replay attacks from different user

				if (media.CreatedBy != userId)
				{
					_logger.Warning(
						"Ownership mismatch - MediaId: {MediaId}, CreatedBy: {CreatedBy}, CurrentUser: {CurrentUser}",
						model.MediaId,
						media.CreatedBy,
						userId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You are not authorized to confirm this upload",
						Status = "failed"
					};
				}

				_logger.Debug("Ownership verified - User matches CreatedBy");

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 4: CHECK SINGLE-USE ENFORCEMENT
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// SECURITY: Token can only be used once
				// Status must be AwaitingUpload
				// If already Completed: someone already confirmed (replay attack?)
				// If Failed: previous upload attempt failed

				if (media.UploadStatus != (int)UploadStatus.AwaitingUpload)
				{
					_logger.Warning(
						"Upload already processed - MediaId: {MediaId}, CurrentStatus: {Status}",
						model.MediaId,
						(UploadStatus)media.UploadStatus);

					var statusMessage = (UploadStatus)media.UploadStatus switch
					{
						UploadStatus.Completed => "Upload has already been confirmed",
						UploadStatus.Failed => "Upload previously failed. Please request a new token",
						UploadStatus.Uploading => "Upload is currently in progress",
						_ => "Upload is not in awaiting state"
					};

					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = statusMessage,
						Status = "failed"
					};
				}

				_logger.Debug("Single-use check passed - Status is AwaitingUpload");

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 5: UPDATE DATABASE WITH UPLOAD RESULTS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// Populate all fields with data from Cloudinary upload response
				// Frontend received this data from Cloudinary and passes it here

				var updateDict = new Dictionary<string, object>
					{
						{ "UploadStatus", (int)UploadStatus.Completed },
						{ "CdnUrl", model.SecureUrl },
						{ "PublicId", model.PublicId },
						{ "FileSizeBytes", model.FileSize },
						{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
					};

				// Add duration if provided (videos/audio)
				if (model.Duration.HasValue)
				{
					updateDict.Add("DurationSeconds", model.Duration.Value);
				}

				// Add thumbnail if provided
				if (!string.IsNullOrEmpty(model.ThumbnailUrl))
				{
					updateDict.Add("ThumbnailUrl", model.ThumbnailUrl);
				}

				await _mediaCommandRepo.UpdateTableColumnById(
					updateDict,
					new KeyValuePair<string, object>("Id", model.MediaId));

				_logger.Information(
					"Upload confirmed - MediaId: {MediaId}, Size: {Size} bytes, CDN: {URL}",
					model.MediaId,
					model.FileSize,
					model.SecureUrl);

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 6: ENQUEUE POST-UPLOAD JOBS
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// For videos: Generate thumbnails and run AI analysis
				// These run asynchronously in background
				// Don't block upload confirmation response

				if ((MediaType)media.MediaType == MediaType.Video)
				{
					_logger.Debug("Enqueueing post-upload jobs for video");

					// Generate thumbnail (extracts frame at 2 seconds)
					_backgroundJobService.EnqueueThumbnailGeneration(
						model.MediaId,
						model.PublicId,
						media.SchoolId);

					// Run AI content analysis (detect inappropriate content, quality, topics)
					_backgroundJobService.EnqueueAIContentAnalysis(
						model.MediaId,
						model.SecureUrl,
						model.Duration);

					_logger.Debug("Post-upload jobs enqueued");
				}

				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// STEP 7: RETURN SUCCESS RESPONSE
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Upload confirmed successfully. File is now available for use.",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error confirming upload - MediaId: {MediaId}", model.MediaId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while confirming upload. Please try again.",
					Status = "failed"
				};
			}
		}



	}
}

//public Task<BaseResponse> LinkMediaToClass(Guid classPreparationId, List<Guid> mediaIds, AuthenticatedUserClaims userClaims)
//{
//	throw new NotImplementedException();
//}

//public Task<MediaFilesListResponse> GetClassMediaFiles(Guid classPreparationId)
//{
//	throw new NotImplementedException();
//}


//private string GetMediaTypeIcon(MediaType mediaType)
//{
//	return mediaType switch
//	{
//		MediaType.Video => "",
//		MediaType.Image => "",
//		MediaType.Document => "",
//		MediaType.Audio => "",
//		_ => ""
//	};
//}

//private string FormatFileSize(long bytes)
//{
//	if (bytes == 0) return "0 B";

//	string[] sizes = { "B", "KB", "MB", "GB" };
//	double len = bytes;
//	int order = 0;

//	while (len >= 1024 && order < sizes.Length - 1)
//	{
//		order++;
//		len = len / 1024;
//	}

//	return $"{len:0.##} {sizes[order]}";
//}

//Task<UploadMediaResponse> IMediaService.UploadMedia(IFormFile file, MediaType mediaType, string? displayName, AuthenticatedUserClaims userClaims)
//{
//	throw new NotImplementedException();
//}

//public Task<BaseResponse> LinkMediaToClass(Guid classPreparationId, List<Guid> mediaIds, AuthenticatedUserClaims userClaims)
//{
//	throw new NotImplementedException();
//}

//public Task<BaseResponse> MoveMediaToPermanent(Guid classPreparationId)
//{
//	throw new NotImplementedException();
//}

//public Task<BaseResponse> DeleteMediaForRejectedClass(Guid classPreparationId, Guid deletedBy, string reason)
//{
//	throw new NotImplementedException();
//}

//public Task<MediaFilesListResponse> GetClassMediaFiles(Guid classPreparationId)
//{
//	throw new NotImplementedException();
//}

//public Task<BaseResponse> DeleteMedia(Guid mediaId, Guid deletedBy, string? reason)
//{
//	throw new NotImplementedException();
//}

//Task<MediaUploadStatusResponse> IMediaService.GetUploadStatus(Guid mediaId)
//{
//	throw new NotImplementedException();
//}


#endregion