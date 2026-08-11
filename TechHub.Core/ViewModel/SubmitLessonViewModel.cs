using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel;

// What frontend sends after uploading to Cloudinary
public class SubmitLessonViewModel
{
	[Required]
	public Guid ClassroomId { get; set; }

	[Required]
	public Guid SubjectId { get; set; }

	[Required]
	public Guid TopicId { get; set; }

	[Required]
	public Guid SubTopicId { get; set; }

	[Required]
	[StringLength(200)]
	public string SubTopic { get; set; }

	[Required]
	[StringLength(500)]
	public string Aim { get; set; }

	[Required]
	[StringLength(2000)]
	public string Description { get; set; }
	public bool IsDraft { get; set; }

	// ✅ Frontend uploads to Cloudinary first, sends back URLs
	//[Required]
	//[MinLength(1, ErrorMessage = "At least one media file is required")]
	public List<LessonMediaViewModel> MediaFiles { get; set; } = new();

	// ✅ Bypass flag for approval replay
	[JsonIgnore]
	public bool BypassApproval { get; set; } = false;

	public string? QuizId { get; set; }

	public DateTime? AccessDate { get; set; }  
	public TimeSpan? AccessTime { get; set; } 
	public int? DurationMinutes { get; set; }

	/// <summary>
	/// Whether the system should auto-generate an AI image for this lesson
	/// once it is approved. When false, no image is generated for the lesson.
	/// </summary>
	public bool ShouldGenerateImage { get; set; } = true;

	/// <summary>
	/// Optional teacher-supplied words describing the kind of materials / images
	/// needed for this lesson. The system combines these with the lesson's aim
	/// and objectives when generating the image.
	/// </summary>
	[StringLength(2000, ErrorMessage = "Image material words cannot exceed 2000 characters")]
	public string? ImageMaterialWords { get; set; }

	/// <summary>
	/// Number of AI images to generate for this lesson once it is approved.
	/// Clamped to the configured maximum (ImageGeneration:MaxImagesPerLesson).
	/// </summary>
	[Range(1, 5, ErrorMessage = "Image count must be between 1 and 5")]
	public int ImageCount { get; set; } = 1;
}

public class LessonMediaViewModel
{
	[Required]
	public string FileName { get; set; }

	[Required]
	public string OriginalFileName { get; set; }

	[Required]
	public string FileExtension { get; set; }

	[Required]
	public string CloudinaryUrl { get; set; }

	[Required]
	public string PublicId { get; set; }

	public long FileSizeBytes { get; set; }
	public int? Duration { get; set; }  // seconds — frontend can detect
	public int DisplayOrder { get; set; }
	public string MetaData { get; set; } = string.Empty;
}

public class CloudinarySignatureResponse
{
	public string ResourceType { get; set; }

	public string Signature { get; set; }
	public string ApiKey { get; set; }
	public string CloudName { get; set; }
	public long Timestamp { get; set; }
	public string Folder { get; set; }
	public string UploadPreset { get; set; }
}

