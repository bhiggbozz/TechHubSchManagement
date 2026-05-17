using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.DTO;
using TechHub.Core.Enum;

namespace TechHub.Core.ViewModel;

/// <summary>
/// Request to generate upload token for direct-to-CDN upload
/// Contains file metadata (NOT the actual file bytes)
/// </summary>
public class RequestUploadTokenViewModel
{
	/// <summary>Original filename (e.g., "lecture-video.mp4")</summary>
	public string FileName { get; set; }

	/// <summary>File size in bytes (e.g., 524288000 for 500 MB)</summary>
	public long FileSize { get; set; }

	/// <summary>Media type: Video, Image, Document, Audio</summary>
	public MediaType MediaType { get; set; }

	/// <summary>Optional display name (friendly name for UI)</summary>
	public string? DisplayName { get; set; }

	/// <summary>MIME type (e.g., "video/mp4", "application/pdf")</summary>
	public string? MimeType { get; set; }
}


/// <summary>
/// Confirmation from frontend that upload completed
/// Contains Cloudinary upload result data
/// </summary>
public class ConfirmUploadViewModel
{
	/// <summary>Database record ID (from RequestUploadToken response)</summary>
	public Guid MediaId { get; set; }

	/// <summary>Cloudinary secure HTTPS URL to uploaded file</summary>
	public string SecureUrl { get; set; }

	/// <summary>Actual Cloudinary public_id (may differ from requested)</summary>
	public string PublicId { get; set; }

	/// <summary>Actual file size after compression (bytes)</summary>
	public long FileSize { get; set; }

	/// <summary>Video/audio duration in seconds (null for images/documents)</summary>
	public decimal? Duration { get; set; }

	/// <summary>Auto-generated thumbnail URL (optional)</summary>
	public string? ThumbnailUrl { get; set; }
}

/// <summary>
/// Existing UploadMediaResponse (for server-side upload)
/// Keeping for backward compatibility
/// </summary>

/// <summary>
/// Upload status query response
/// </summary>


/// <summary>
/// List of media files for a class
/// </summary>
//public class MediaFilesListResponse : BaseResponse
//{
//	public List<MediaFileDto> MediaFiles { get; set; }
//	public int TotalCount { get; set; }
//}

