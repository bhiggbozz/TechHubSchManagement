using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Service.Service;

namespace TechHub.Service.Interface;
/// <summary>
/// Service for managing media files in Cloudinary CDN
/// </summary>
public interface ICloudinaryService
{
	/// <summary>
	/// Upload media file to Cloudinary with automatic compression
	/// </summary>
	/// <param name="mediaStream">File content as stream</param>
	/// <param name="mediaKey">Unique filename (e.g., "e589_20250308_a1b2c3_video.mp4")</param>
	/// <param name="schoolId">School GUID for folder organization</param>
	/// <param name="mediaType">Type of media (Video, Image, Document, Audio)</param>
	/// <param name="isTemporary">True = temp/pending folder, False = schools/permanent folder</param>
	/// <returns>Upload result with CDN URL and metadata</returns>
	Task<CloudinaryUploadResult> UploadMediaAsync(Stream mediaStream,string mediaKey,Guid schoolId,MediaType mediaType,bool isTemporary = true);

	/// <summary>
	/// Move file from temporary to permanent storage (when class is approved)
	/// </summary>
	/// <param name="publicId">Cloudinary public ID (e.g., "temp/pending/e589/file.mp4")</param>
	/// <param name="schoolId">School GUID</param>
	/// <returns>True if successful</returns>
	Task<bool> MoveToPermamentStorageAsync(string publicId, Guid schoolId);

	/// <summary>
	/// Delete file from Cloudinary (when class is rejected)
	/// </summary>
	/// <param name="publicId">Cloudinary public ID</param>
	/// <param name="mediaType">Type of media for proper deletion</param>
	/// <returns>True if successful</returns>
	Task<bool> DeleteMediaAsync(string publicId, MediaType mediaType);

	/// <summary>
	/// Generate thumbnail URL for video
	/// </summary>
	/// <param name="publicId">Video public ID</param>
	/// <param name="width">Thumbnail width (default 320px)</param>
	/// <param name="height">Thumbnail height (default 180px)</param>
	/// <returns>Thumbnail URL</returns>
	string GenerateThumbnailUrl(string publicId, int width = 320, int height = 180);
}
