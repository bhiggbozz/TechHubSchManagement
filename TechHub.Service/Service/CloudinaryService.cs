// Services/CloudinaryService.cs

using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using Serilog;
using System.Security.Cryptography;
using System.Text;
using TechHub.Core.Configuration;
using TechHub.Core.Enum;
using TechHub.Core.Enums;
using TechHub.Service.Interface;

namespace TechHub.Service.Service
{
	public class CloudinaryService : ICloudinaryService
	{
		private readonly Cloudinary _cloudinary;
		private readonly ILogger _logger;
		private readonly CloudinarySettings _settings;

		public CloudinaryService(IOptions<CloudinarySettings> options,ILogger logger)
		{
	
			_settings = options.Value;
			_logger = logger;

			// Validate configuration
			if (string.IsNullOrEmpty(_settings.CloudName) ||
				string.IsNullOrEmpty(_settings.ApiKey) ||
				string.IsNullOrEmpty(_settings.ApiSecret))
			{
				var errorMsg = "Cloudinary configuration is missing. " +
							  "Please add Cloudinary section to appsettings.json with CloudName, ApiKey, and ApiSecret.";

				_logger.Fatal(errorMsg);
				throw new InvalidOperationException(errorMsg);
			}

			// Initialize Cloudinary
			var account = new Account(_settings.CloudName,_settings.ApiKey,_settings.ApiSecret);

			_cloudinary = new Cloudinary(account);
			_cloudinary.Api.Secure = true;

			_logger.Information(
				"Cloudinary initialized - CloudName: {CloudName}, MaxVideoWidth: {MaxWidth}p, MaxFileSize: {MaxSize}MB",
				_settings.CloudName,
				_settings.Settings.Compression.Video.MaxWidth,
				_settings.Settings.FileLimits.MaxFileSizeMB);
		}

		#region Main Upload Method

		/// <summary>
		/// Main upload method - Routes to appropriate handler based on media type
		/// </summary>
	   
		public async Task<CloudinaryUploadResult> UploadMediaAsync(Stream mediaStream,string mediaKey,Guid schoolId,MediaType mediaType,bool isTemporary = true)
		{
			try
			{

				// Get folder template from configuration
				// Template: "temp/pending/{schoolId}" or "schools/{schoolId}"
				var folderTemplate = isTemporary
					? _settings.Settings.FolderStructure.TempPending
					: _settings.Settings.FolderStructure.Permanent;

				// Replace placeholder with actual school GUID
				// Result: "temp/pending/e5898860-31a3-4f17-8f32-0528212ba69d"
				var folder = folderTemplate.Replace("{schoolId}", schoolId.ToString());

				_logger.Information(
					"Starting Cloudinary upload - MediaKey: {MediaKey}, Folder: {Folder}, Type: {MediaType}, IsTemp: {IsTemp}",
					mediaKey,
					folder,
					mediaType,
					isTemporary);

				// ========================================
				// STEP 2: ROUTE TO APPROPRIATE HANDLER
				// ========================================

				// Each media type needs different processing:
				// - Video/Audio: Compression, transcoding, thumbnail generation
				// - Image: Optimization, format conversion, resizing
				// - Document: Upload as-is, no processing

				CloudinaryUploadResult result;

				if (mediaType == MediaType.Video || mediaType == MediaType.Audio)
				{
					// Handle video/audio with compression
					result = await UploadVideoAsync(mediaStream, mediaKey, folder);
				}
				else if (mediaType == MediaType.Image)
				{
					// Handle image with optimization
					result = await UploadImageAsync(mediaStream, mediaKey, folder);
				}
				else
				{
					// Handle document (PDF, DOC, etc.) - no processing
					result = await UploadDocumentAsync(mediaStream, mediaKey, folder);
				}

				// ========================================
				// STEP 3: RETURN RESULT
				// ========================================

				if (result.Success)
				{
					_logger.Information(
						"Upload completed successfully - PublicId: {PublicId}, Size: {Size}, Type: {Type}",
						result.PublicId,
						FormatFileSize(result.FileSizeBytes),
						mediaType);
				}
				else
				{
					_logger.Error(
						"Upload failed - MediaKey: {MediaKey}, Error: {Error}",
						mediaKey,
						result.ErrorMessage);
				}

				return result;
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Exception during Cloudinary upload - MediaKey: {MediaKey}", mediaKey);

				return new CloudinaryUploadResult
				{
					Success = false,
					ErrorMessage = $"Upload exception: {ex.Message}"
				};
			}
		}

		#endregion

		#region Upload Handlers (Video, Image, Document)

		/// <summary>
		/// Upload video/audio with compression and optimization
		/// </summary>
		/// <remarks>
		/// COMPRESSION BENEFITS:
		/// - Original: 500MB (1080p, 8 Mbps)
		/// - Compressed: 50MB (720p, 1 Mbps) - 90% reduction!
		/// - Quality: Still excellent for educational content
		/// - Benefits: Faster uploads, cheaper storage, faster downloads
		/// </remarks>
		private async Task<CloudinaryUploadResult> UploadVideoAsync(Stream mediaStream,string mediaKey,string folder)
		{
			try
			{
				var videoSettings = _settings.Settings.Compression.Video;

				_logger.Information(
					"Uploading video with compression - Quality: auto:eco, MaxWidth: {Width}p, Bitrate: {Bitrate}",
					videoSettings.MaxWidth,
					videoSettings.Bitrate);

				

				var videoParams = new VideoUploadParams
				{
					// File details
					File = new FileDescription(mediaKey, mediaStream),
					PublicId = mediaKey,
					Folder = folder,

					// Apply transformation during upload
					// This compresses the video before storing it
					Transformation = new Transformation()
						// Limit resolution to configured max (default 720p)
						// If video is 1080p → downscale to 720p
						// If video is 480p → keep as 480p (don't upscale)
						.Width(videoSettings.MaxWidth).Crop("limit")

						// Quality: "auto:eco" = aggressive compression while maintaining quality
						// Cloudinary automatically finds the best compression settings
						.Quality("auto:eco"),

					// ========================================
					// UPLOAD OPTIONS
					// ========================================

					// Don't overwrite if file already exists
					Overwrite = false,

					// Don't add random suffix to filename
					UniqueFilename = false,

					// Use our MediaKey as the filename
					UseFilename = true
				};

				var uploadResult = await _cloudinary.UploadAsync(videoParams);

				

				if (uploadResult.Error != null)
				{
					_logger.Error(
						"Video upload failed - Error: {Error}, MediaKey: {MediaKey}",
						uploadResult.Error.Message,
						mediaKey);

					return new CloudinaryUploadResult
					{
						Success = false,
						ErrorMessage = uploadResult.Error.Message
					};
				}


				// Generate a 320x180 thumbnail from the video
				// Cloudinary automatically extracts a frame from the video
				var thumbnailUrl = GenerateThumbnailUrl(uploadResult.PublicId);

				_logger.Information(
					"Video uploaded successfully - PublicId: {PublicId}, Size: {Size}, Duration: {Duration}s, Dimensions: {Width}x{Height}",
					uploadResult.PublicId,
					FormatFileSize(uploadResult.Bytes),
					uploadResult.Duration,
					uploadResult.Width,
					uploadResult.Height);


				return new CloudinaryUploadResult
				{
					Success = true,
					PublicId = uploadResult.PublicId,
					SecureUrl = uploadResult.SecureUrl?.ToString() ?? string.Empty,
					FileSizeBytes = uploadResult.Bytes,
					Format = uploadResult.Format,
					Duration = uploadResult.Duration,
					Width = uploadResult.Width,
					Height = uploadResult.Height,
					ThumbnailUrl = thumbnailUrl
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Exception uploading video - MediaKey: {MediaKey}", mediaKey);

				return new CloudinaryUploadResult
				{
					Success = false,
					ErrorMessage = $"Video upload exception: {ex.Message}"
				};
			}
		}

		/// <summary>
		/// Upload image with optimization
		/// </summary>
		/// <remarks>
		/// IMAGE OPTIMIZATION:
		/// - Original: 5MB (PNG, 4000x3000)
		/// - Optimized: 500KB (auto-format, 1920x1440) - 90% reduction!
		/// - Auto format: Cloudinary serves WebP to modern browsers, JPEG to older ones
		/// - Benefits: Faster page loads, cheaper storage, better UX
		/// </remarks>
		private async Task<CloudinaryUploadResult> UploadImageAsync(Stream mediaStream,string mediaKey,string folder)
		{
			try
			{
				var imageSettings = _settings.Settings.Compression.Image;

				_logger.Information(
					"Uploading image with optimization - Quality: auto:eco, MaxWidth: {Width}px",
					imageSettings.MaxWidth);

				var imageParams = new ImageUploadParams
				{
					File = new FileDescription(mediaKey, mediaStream),
					PublicId = mediaKey,
					Folder = folder,

					// Image optimization transformation
					Transformation = new Transformation()
						// Limit width to configured max (default 1920px - Full HD)
						.Width(imageSettings.MaxWidth).Crop("limit")

						// Quality: "auto:eco" = aggressive compression
						.Quality("auto:eco"),

					Overwrite = false,
					UniqueFilename = false,
					UseFilename = true
				};

				var uploadResult = await _cloudinary.UploadAsync(imageParams);

				if (uploadResult.Error != null)
				{
					_logger.Error(
						"Image upload failed - Error: {Error}, MediaKey: {MediaKey}",
						uploadResult.Error.Message,
						mediaKey);

					return new CloudinaryUploadResult
					{
						Success = false,
						ErrorMessage = uploadResult.Error.Message
					};
				}

				_logger.Information(
					"Image uploaded successfully - PublicId: {PublicId}, Size: {Size}, Dimensions: {Width}x{Height}",
					uploadResult.PublicId,
					FormatFileSize(uploadResult.Bytes),
					uploadResult.Width,
					uploadResult.Height);

				return new CloudinaryUploadResult
				{
					Success = true,
					PublicId = uploadResult.PublicId,
					SecureUrl = uploadResult.SecureUrl?.ToString() ?? string.Empty,
					FileSizeBytes = uploadResult.Bytes,
					Format = uploadResult.Format,
					Duration = 0,
					Width = uploadResult.Width,
					Height = uploadResult.Height,
					ThumbnailUrl = null
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "💥 Exception uploading image - MediaKey: {MediaKey}", mediaKey);

				return new CloudinaryUploadResult
				{
					Success = false,
					ErrorMessage = $"Image upload exception: {ex.Message}"
				};
			}
		}

		/// <summary>
		/// Upload document as-is (no processing)
		/// </summary>
		/// <remarks>
		/// DOCUMENTS (PDF, DOC, PPT):
		/// - Uploaded without any transformation
		/// - No compression applied
		/// - Why? Documents are already compressed internally
		/// - Converting might break formatting
		/// - Usually smaller than videos anyway
		/// </remarks>
		private async Task<CloudinaryUploadResult> UploadDocumentAsync(
			Stream mediaStream,
			string mediaKey,
			string folder)
		{
			try
			{
				_logger.Information("📄 Uploading document (no processing) - MediaKey: {MediaKey}", mediaKey);

				var rawParams = new RawUploadParams
				{
					File = new FileDescription(mediaKey, mediaStream),
					PublicId = mediaKey,
					Folder = folder,

					// No transformation - upload as-is
					Overwrite = false,
					UniqueFilename = false,
					UseFilename = true
				};

				var uploadResult = await _cloudinary.UploadAsync(rawParams);

				if (uploadResult.Error != null)
				{
					_logger.Error(
						"Document upload failed - Error: {Error}, MediaKey: {MediaKey}",
						uploadResult.Error.Message,
						mediaKey);

					return new CloudinaryUploadResult
					{
						Success = false,
						ErrorMessage = uploadResult.Error.Message
					};
				}

				_logger.Information(
					"Document uploaded successfully - PublicId: {PublicId}, Size: {Size}",
					uploadResult.PublicId,
					FormatFileSize(uploadResult.Bytes));

				return new CloudinaryUploadResult
				{
					Success = true,
					PublicId = uploadResult.PublicId,
					SecureUrl = uploadResult.SecureUrl?.ToString() ?? string.Empty,
					FileSizeBytes = uploadResult.Bytes,
					Format = uploadResult.Format,
					Duration = 0,
					Width = 0,
					Height = 0,
					ThumbnailUrl = null
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Exception uploading document - MediaKey: {MediaKey}", mediaKey);

				return new CloudinaryUploadResult
				{
					Success = false,
					ErrorMessage = $"Document upload exception: {ex.Message}"
				};
			}
		}

		#endregion

		#region Move to Permanent Storage

		/// <summary>
		/// Move file from temporary to permanent storage
		/// Called when admin approves a class
		/// </summary>
		/// <remarks>
		/// WORKFLOW:
		/// 1. Teacher uploads video → "temp/pending/{schoolId}/video.mp4"
		/// 2. Admin approves class → Move to "schools/{schoolId}/video.mp4"
		/// 3. File is now permanent (won't be auto-deleted)
		/// 
		/// WHY:
		/// - Temporary files auto-deleted after 7 days (cleanup policy)
		/// - Approved content should be kept forever
		/// - Clear separation between pending and approved
		/// </remarks>
		public async Task<bool> MoveToPermamentStorageAsync(string publicId, Guid schoolId)
		{
			try
			{
				// Build new permanent folder path
				var permanentFolderTemplate = _settings.Settings.FolderStructure.Permanent;
				var permanentFolder = permanentFolderTemplate.Replace("{schoolId}", schoolId.ToString());

				// Extract filename from current publicId
				// Example: "temp/pending/e589/video" → "video"
				var parts = publicId.Split('/');
				var fileName = parts[^1];  // Last element

				// Build new publicId
				// Example: "schools/e589/video"
				var newPublicId = $"{permanentFolder}/{fileName}";

				_logger.Information(
					"Moving to permanent storage - From: '{OldPublicId}' → To: '{NewPublicId}'",
					publicId,
					newPublicId);

				// Determine resource type (video/image/raw)
				var resourceType = DetermineResourceType(publicId);

				// Execute rename (move) operation
				var renameParams = new RenameParams(publicId, newPublicId)
				{
					ResourceType = resourceType,
					Type = "upload",
					Overwrite = false
				};

				var result = await _cloudinary.RenameAsync(renameParams);

				if (result.Error != null)
				{
					_logger.Error(
						"Failed to move to permanent storage - Error: {Error}, PublicId: {PublicId}",
						result.Error.Message,
						publicId);

					return false;
				}

				_logger.Information("Successfully moved to permanent storage");
				return true;
			}
			catch (Exception ex)
			{
				_logger.Error(
					ex,
					"Exception moving to permanent storage - PublicId: {PublicId}",
					publicId);

				return false;
			}
		}

		#endregion

		#region Delete Media

		/// <summary>
		/// Delete file from Cloudinary
		/// Called when admin rejects class or teacher deletes draft
		/// </summary>
		/// <remarks>
		/// WHEN WE DELETE:
		/// 1. Admin rejects class → Delete all media
		/// 2. Teacher deletes draft → Delete all media
		/// 3. Cleanup job → Delete orphaned temp files
		/// 
		/// WHY:
		/// - Save storage costs
		/// - Save bandwidth costs
		/// - Keep CDN clean
		/// - GDPR compliance
		/// </remarks>
		public async Task<bool> DeleteMediaAsync(string publicId, MediaType mediaType)
		{
			try
			{
				_logger.Information("🗑️ Deleting from Cloudinary - PublicId: {PublicId}", publicId);

				// Determine resource type for deletion
				var resourceType = mediaType switch
				{
					MediaType.Video => ResourceType.Video,
					MediaType.Audio => ResourceType.Video,  // Audio uses Video resource type
					MediaType.Image => ResourceType.Image,
					MediaType.Document => ResourceType.Raw,
					_ => ResourceType.Raw
				};

				var deletionParams = new DeletionParams(publicId)
				{
					ResourceType = resourceType
				};

				var result = await _cloudinary.DestroyAsync(deletionParams);

				// Result can be "ok" (deleted) or "not found" (already deleted - not an error)
				var success = result.Result == "ok" || result.Result == "not found";

				if (success)
				{
					_logger.Information(
						"Cloudinary deletion successful - PublicId: {PublicId}, Result: {Result}",
						publicId,
						result.Result);
				}
				else
				{
					_logger.Warning(
						"Cloudinary deletion unexpected result - PublicId: {PublicId}, Result: {Result}",
						publicId,
						result.Result);
				}

				return success;
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Exception deleting from Cloudinary - PublicId: {PublicId}", publicId);
				return false;
			}
		}

		#endregion

		#region Generate Thumbnail

		/// <summary>
		/// Generate thumbnail URL for video
		/// </summary>
		/// <remarks>
		/// HOW IT WORKS:
		/// 1. Cloudinary extracts a frame from video (usually 3 seconds in)
		/// 2. Resizes to specified dimensions
		/// 3. Returns URL to thumbnail image
		/// 4. Thumbnail is cached on CDN (fast delivery)
		/// 
		/// EXAMPLE:
		/// Video: https://res.cloudinary.com/.../video.mp4
		/// Thumbnail: https://res.cloudinary.com/.../w_320,h_180/video.jpg
		/// </remarks>
		public string GenerateThumbnailUrl(string publicId, int width = 320, int height = 180)
		{
			try
			{
				var transformation = new Transformation()
					.Width(width)
					.Height(height)
					.Crop("fill")        // Crop to exact dimensions
					.Gravity("center")   // Focus on center of frame
					.Quality("auto");    // Auto-optimize quality

				var url = _cloudinary.Api.UrlVideoUp
					.Transform(transformation)
					.BuildUrl(publicId + ".jpg");

				_logger.Debug("Generated thumbnail URL - Width: {Width}, Height: {Height}", width, height);

				return url;
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error generating thumbnail URL - PublicId: {PublicId}", publicId);
				return string.Empty;
			}
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Determine Cloudinary resource type from file extension
		/// </summary>
		private ResourceType DetermineResourceType(string publicId)
		{
			var extension = Path.GetExtension(publicId).ToLower();

			return extension switch
			{
				// Video formats
				".mp4" or ".mov" or ".avi" or ".webm" or ".mkv" or ".flv" => ResourceType.Video,

				// Audio formats (use Video resource type)
				".mp3" or ".wav" or ".aac" or ".ogg" or ".m4a" => ResourceType.Video,

				// Image formats
				".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" => ResourceType.Image,

				// Everything else (documents)
				_ => ResourceType.Raw
			};
		}

		/// <summary>
		/// Format file size for human-readable logging
		/// </summary>
		private string FormatFileSize(long bytes)
		{
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



		#endregion

		/// <summary>
		/// NEW: Generate signed upload token for direct browser-to-Cloudinary upload
		/// 
		/// SIGNATURE ALGORITHM:
		/// 1. Create sorted parameter dictionary (timestamp, folder, public_id)
		/// 2. Concatenate into "key1=value1&key2=value2" string
		/// 3. Append API secret (this makes it impossible to forge)
		/// 4. Compute SHA-1 hash
		/// 5. Return as hexadecimal string
		/// 
		/// SECURITY:
		/// - API Secret NEVER sent to frontend
		/// - Signature proves parameters came from your server
		/// - Frontend cannot generate valid signatures
		/// - Timestamp limits validity to 1 hour
		/// - PublicId and Folder lock upload destination
		/// 
		/// EXAMPLE:
		/// Input parameters:
		///   timestamp: 1710512345
		///   folder: "temp/pending/school-abc"
		///   public_id: "media-xyz-123"
		/// 
		/// String to sign:
		///   "folder=temp/pending/school-abc&public_id=media-xyz-123&timestamp=1710512345YOUR_API_SECRET"
		/// 
		/// Signature (SHA-1):
		///   "a7f8b9c2d3e4f5g6h7i8j9k0l1m2n3o4p5q6r7s8"
		/// </summary>
		public CloudinaryUploadToken GenerateUploadToken(string publicId,string folder,long timestamp)
		{
			try
			{
				_logger.Debug(
					"Generating upload token - PublicId: {PublicId}, Folder: {Folder}",publicId,folder);

				// Parameters to include in signature
				// MUST match exactly what frontend sends to Cloudinary
				// Sorted alphabetically for consistent signature generation
				var paramsToSign = new SortedDictionary<string, object>
				{
					{ "timestamp", timestamp },
					{ "folder", folder },
					{ "public_id", publicId }
				};

				// Generate cryptographic signature
				var signature = GenerateSignature(paramsToSign);

				_logger.Information(
					"Upload token generated - PublicId: {PublicId}, ExpiresAt: {ExpiresAt}",
					publicId,
					DateTimeOffset.FromUnixTimeSeconds(timestamp + 3600).ToString("yyyy-MM-dd HH:mm:ss"));

				return new CloudinaryUploadToken
				{
					UploadUrl = $"https://api.cloudinary.com/v1_1/{_settings.CloudName}/auto/upload",
					Signature = signature,
					Timestamp = timestamp,
					PublicId = publicId,
					Folder = folder,
					ApiKey =  _settings.ApiKey,  // Safe to expose (public identifier, not secret)
					CloudName = _settings.CloudName
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error generating upload token - PublicId: {PublicId}", publicId);
				throw;
			}
		}

		/// <summary>
		/// Generate SHA-1 signature for Cloudinary authentication
		/// 
		/// CRITICAL SECURITY:
		/// - API Secret appended to parameter string
		/// - Without API Secret, signature cannot be forged
		/// - This is what makes direct uploads secure
		/// 
		/// USED FOR:
		/// - Upload tokens (frontend direct upload)
		/// - Webhook validation (verify Cloudinary sent request)
		/// - Any authenticated Cloudinary API call
		/// 
		/// ALGORITHM DETAILS:
		/// 1. Sort parameters alphabetically by key
		/// 2. Build "key1=value1&key2=value2" string
		/// 3. Append API Secret: "params_stringYOUR_API_SECRET"
		/// 4. Compute SHA-1 hash of combined string
		/// 5. Convert to lowercase hexadecimal
		/// 
		/// WHY SHA-1 (not SHA-256)?
		/// - Cloudinary API requires SHA-1 specifically
		/// - SHA-1 is sufficient for HMAC signatures (not file hashing)
		/// </summary>
		public string GenerateSignature(SortedDictionary<string, object> parameters)
		{
			try
			{
				// Sort parameters alphabetically
				// This ensures consistent signature regardless of parameter order
				var sortedParams = new SortedDictionary<string, object>(parameters);

				// Build string to sign
				// Format: "key1=value1&key2=value2&key3=value3"
				var stringToSign = string.Join("&",
					sortedParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));

				// CRITICAL: Append API secret
				// This is what makes the signature secure
				// Frontend doesn't have API secret, so can't forge signatures
				stringToSign += _settings.ApiSecret;

				_logger.Debug("String to sign (without secret): {Params}",
					string.Join("&", sortedParams.Select(kvp => $"{kvp.Key}={kvp.Value}")));

				// Compute SHA-1 hash
				using (var sha1 = SHA1.Create())
				{
					var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
					var signature = BitConverter.ToString(hash)
						.Replace("-", "")
						.ToLowerInvariant();

					return signature;
				}
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error generating signature");
				throw;
			}
		}
	}

	#region Result Classes

	/// <summary>
	/// Custom result class for all Cloudinary upload operations
	/// </summary>
	public class CloudinaryUploadResult
	{
		/// <summary>
		/// Was the upload successful?
		/// </summary>
		public bool Success { get; set; }

		/// <summary>
		/// Cloudinary public ID (full path in CDN)
		/// Example: "temp/pending/e589/video"
		/// </summary>
		public string PublicId { get; set; } = string.Empty;

		/// <summary>
		/// Direct HTTPS URL to access the file
		/// Example: "https://res.cloudinary.com/techhub/video/upload/..."
		/// </summary>
		public string SecureUrl { get; set; } = string.Empty;

		/// <summary>
		/// File size in bytes (AFTER compression)
		/// </summary>
		public long FileSizeBytes { get; set; }

		/// <summary>
		/// File format (mp4, jpg, pdf, etc.)
		/// </summary>
		public string Format { get; set; } = string.Empty;

		/// <summary>
		/// Video/audio duration in seconds (0 for images/documents)
		/// </summary>
		public double Duration { get; set; }

		/// <summary>
		/// Video/image width in pixels (0 for documents)
		/// </summary>
		public int Width { get; set; }

		/// <summary>
		/// Video/image height in pixels (0 for documents)
		/// </summary>
		public int Height { get; set; }

		/// <summary>
		/// URL to video thumbnail (null for non-video files)
		/// </summary>
		public string? ThumbnailUrl { get; set; }

		/// <summary>
		/// Error message if Success = false
		/// </summary>
		public string ErrorMessage { get; set; } = string.Empty;
	}

	#endregion

	
}