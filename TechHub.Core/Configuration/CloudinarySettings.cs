using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Configuration;

public class CloudinarySettings
{
	public const string SectionName = "Cloudinary";

	[Required(ErrorMessage = "Cloudinary CloudName is required")]
	public string CloudName { get; set; } = string.Empty;

	[Required(ErrorMessage = "Cloudinary ApiKey is required")]
	public string ApiKey { get; set; } = string.Empty;

	[Required(ErrorMessage = "Cloudinary ApiSecret is required")]
	public string ApiSecret { get; set; } = string.Empty;

	public CloudinaryAppSettings Settings { get; set; } = new();
}

public class CloudinaryAppSettings
{
	public FolderStructureSettings FolderStructure { get; set; } = new();
	public CompressionSettings Compression { get; set; } = new();
	public FileLimitsSettings FileLimits { get; set; } = new();
}

public class FolderStructureSettings
{
	public string TempPending { get; set; } = "temp/pending/{schoolId}";
	public string Permanent { get; set; } = "schools/{schoolId}";
}

/// <summary>
/// Compression settings for videos and images
/// </summary>
public class CompressionConfig
{
	/// <summary>Video compression settings</summary>
	public VideoCompressionConfig Video { get; set; }

	/// <summary>Image compression settings</summary>
	public ImageCompressionConfig Image { get; set; }
}

/// <summary>
/// Video compression configuration
/// </summary>
public class VideoCompressionConfig
{
	/// <summary>Quality setting (e.g., "auto:eco", "auto:good", "auto:best")</summary>
	public string Quality { get; set; }

	/// <summary>Maximum width in pixels (downscale if larger)</summary>
	public int MaxWidth { get; set; }

	/// <summary>Maximum height in pixels (downscale if larger)</summary>
	public int MaxHeight { get; set; }

	/// <summary>Video codec (e.g., "h264", "vp9")</summary>
	public string VideoCodec { get; set; }

	/// <summary>Audio codec (e.g., "aac", "mp3")</summary>
	public string AudioCodec { get; set; }

	/// <summary>Target bitrate (e.g., "1m" for 1 Mbps)</summary>
	public string Bitrate { get; set; }

	/// <summary>Target frame rate (e.g., 30)</summary>
	public int FrameRate { get; set; }
}

/// <summary>
/// Image compression configuration
/// </summary>
public class ImageCompressionConfig
{
	/// <summary>Quality setting (e.g., "auto:eco", "auto:good")</summary>
	public string Quality { get; set; }

	/// <summary>Maximum width in pixels</summary>
	public int MaxWidth { get; set; }

	/// <summary>Output format (e.g., "auto", "webp", "jpg")</summary>
	public string Format { get; set; }
}

/// <summary>
/// File size limits and allowed formats
/// </summary>
public class FileLimitsConfig
{
	/// <summary>Maximum file size in megabytes</summary>
	public int MaxFileSizeMB { get; set; }

	/// <summary>Allowed video file extensions (without dot)</summary>
	public List<string> AllowedVideoFormats { get; set; }

	/// <summary>Allowed image file extensions (without dot)</summary>
	public List<string> AllowedImageFormats { get; set; }

	/// <summary>Allowed document file extensions (without dot)</summary>
	public List<string> AllowedDocumentFormats { get; set; }

	/// <summary>Allowed audio file extensions (without dot)</summary>
	public List<string> AllowedAudioFormats { get; set; }
}
