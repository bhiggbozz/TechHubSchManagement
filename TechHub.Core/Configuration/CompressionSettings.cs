using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Configuration;

public class CompressionSettings
{
	public VideoCompressionSettings Video { get; set; } = new();
	public ImageCompressionSettings Image { get; set; } = new();
}

public class VideoCompressionSettings
{
	[Required]
	public string Quality { get; set; } = "auto:eco";

	[Range(480, 1920)]
	public int MaxWidth { get; set; } = 1280;

	[Range(360, 1080)]
	public int MaxHeight { get; set; } = 720;

	[Required]
	public string VideoCodec { get; set; } = "h264";

	[Required]
	public string AudioCodec { get; set; } = "aac";

	[Required]
	public string Bitrate { get; set; } = "1m";

	[Range(15, 60)]
	public int FrameRate { get; set; } = 30;
}

public class ImageCompressionSettings
{
	[Required]
	public string Quality { get; set; } = "auto:eco";

	[Range(480, 3840)]
	public int MaxWidth { get; set; } = 1920;

	[Required]
	public string Format { get; set; } = "auto";
}

public class FileLimitsSettings
{
	[Range(1, 500)]
	public int MaxFileSizeMB { get; set; } = 100;

	[MinLength(1)]
	public List<string> AllowedVideoFormats { get; set; } = new() { "mp4", "mov", "avi", "webm" };

	[MinLength(1)]
	public List<string> AllowedImageFormats { get; set; } = new() { "jpg", "jpeg", "png", "gif", "webp" };

	public List<string> AllowedDocumentFormats { get; set; } = new() { "pdf", "doc", "docx", "ppt", "pptx" };

	public List<string> AllowedAudioFormats { get; set; } = new() { "mp3", "wav", "ogg", "webm", "m4a", "aac" };
}

