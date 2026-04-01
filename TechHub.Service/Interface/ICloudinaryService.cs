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

	/// <summary>
	/// NEW: Generate signed upload token for direct browser-to-Cloudinary upload
	/// 
	/// SECURITY:
	/// - Signature proves request came from your server
	/// - Timestamp limits validity (expires 1 hour)
	/// - PublicId locks upload to specific file location
	/// - Folder restricts upload destination
	/// - Frontend cannot forge signatures (no API secret)
	/// 
	/// WORKFLOW:
	/// 1. Create parameters to sign (timestamp, folder, publicId)
	/// 2. Generate SHA-1 signature using API secret
	/// 3. Return token with all upload credentials
	/// 4. Frontend uploads directly to Cloudinary
	/// 5. Webhook notifies your API when complete
	/// 
	/// BENEFITS:
	/// - Server handles 1000+ concurrent uploads
	/// - No server bandwidth used
	/// - Faster for users (direct CDN upload)
	/// </summary>
	CloudinaryUploadToken GenerateUploadToken(string publicId,string folder,long timestamp);

	/// <summary>
	/// Generate signature for Cloudinary operations
	/// 
	/// ALGORITHM:
	/// 1. Sort parameters alphabetically
	/// 2. Create "key1=value1&key2=value2" string
	/// 3. Append API secret
	/// 4. Compute SHA-1 hash
	/// 5. Return hex string
	/// 
	/// USED FOR:
	/// - Upload tokens
	/// - Webhook validation
	/// - Any authenticated Cloudinary API call
	/// </summary>
	string GenerateSignature(System.Collections.Generic.SortedDictionary<string, object> uploadParams);
	Task<CloudinaryUploadResult> UploadSchoolLogoAsync(Stream imageStream,string fileName,Guid schoolId);
	string GetUrl(Guid schoolId, MediaType mediaType, string entityId, bool isTemporary = false);

}

public class CloudinaryUploadToken
{
	/// <summary>Cloudinary upload endpoint URL</summary>
	public string UploadUrl { get; set; }

	/// <summary>Cryptographic signature (proves request from your server)</summary>
	public string Signature { get; set; }

	/// <summary>Unix timestamp when token was created</summary>
	public long Timestamp { get; set; }

	/// <summary>Cloudinary public_id for the file</summary>
	public string PublicId { get; set; }

	/// <summary>Folder where file will be stored</summary>
	public string Folder { get; set; }

	/// <summary>Your Cloudinary API key (safe to expose)</summary>
	public string ApiKey { get; set; }

	/// <summary>Your Cloudinary cloud name</summary>
	public string CloudName { get; set; }
}
