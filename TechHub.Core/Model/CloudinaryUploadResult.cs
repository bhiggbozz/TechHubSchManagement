using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Model;
//public class CloudinaryUploadResult
//{
//	/// <summary>
//	/// Was the upload successful?
//	/// </summary>
//	public bool Success { get; set; }

//	/// <summary>
//	/// Cloudinary public ID (full path)
//	/// Example: "temp/pending/e589/video"
//	/// </summary>
//	public string PublicId { get; set; } = string.Empty;

//	/// <summary>
//	/// Direct HTTPS URL to access the file
//	/// Example: "https://res.cloudinary.com/techhub/video/upload/..."
//	/// </summary>
//	public string SecureUrl { get; set; } = string.Empty;

//	/// <summary>
//	/// File size in bytes (AFTER compression)
//	/// </summary>
//	public long FileSizeBytes { get; set; }

//	/// <summary>
//	/// File format (mp4, jpg, pdf, etc.)
//	/// </summary>
//	public string Format { get; set; } = string.Empty;

//	/// <summary>
//	/// Video/audio duration in seconds
//	/// </summary>
//	public double Duration { get; set; }

//	/// <summary>
//	/// Video/image width in pixels
//	/// </summary>
//	public int Width { get; set; }

//	/// <summary>
//	/// Video/image height in pixels
//	/// </summary>
//	public int Height { get; set; }

//	/// <summary>
//	/// URL to video thumbnail (if applicable)
//	/// </summary>
//	public string? ThumbnailUrl { get; set; }

//	/// <summary>
//	/// Error message (if Success = false)
//	/// </summary>
//	public string ErrorMessage { get; set; } = string.Empty;
//}

/// <summary>
/// NEW: Upload token for direct browser upload
/// Contains all credentials needed to upload directly to Cloudinary
/// </summary>
