using System.ComponentModel.DataAnnotations;

namespace TechHub.Core.ViewModel;

/// <summary>
/// Request body for generating an image for a lesson.
///
/// When <see cref="Prompt"/> is supplied (a modified/edited prompt) it is used
/// as-is and saved as the new "last prompt". When omitted, the prompt is built
/// automatically from the lesson's aim + objectives.
/// </summary>
public class GenerateImageViewModel
{
	/// <summary>Optional teacher-supplied prompt override.</summary>
	[StringLength(4000, ErrorMessage = "Prompt cannot exceed 4000 characters")]
	public string? Prompt { get; set; }

	/// <summary>Optional style hint (e.g. "flat illustration", "photorealistic").</summary>
	[StringLength(200)]
	public string? Style { get; set; }

	/// <summary>
	/// Optional teacher-supplied words describing the required materials /
	/// visual elements. When no full <see cref="Prompt"/> override is supplied,
	/// these words are combined with the lesson's aim + objectives.
	/// </summary>
	[StringLength(2000, ErrorMessage = "Material words cannot exceed 2000 characters")]
	public string? MaterialWords { get; set; }

	/// <summary>
	/// Number of images to generate for the lesson. Defaults to 1; clamped to
	/// the configured maximum (ImageGeneration:MaxImagesPerLesson).
	/// </summary>
	[Range(1, 5, ErrorMessage = "Image count must be between 1 and 5")]
	public int? ImageCount { get; set; }

	public string? NegativePrompt { get; set; }

	[Range(256, 2048)]
	public int? Width { get; set; }

	[Range(256, 2048)]
	public int? Height { get; set; }
}

/// <summary>
/// Request body for configuring a school's feature access (SchoolFeature table).
/// </summary>
public class SchoolFeatureViewModel
{
	[Required]
	public Guid SchoolId { get; set; }

	[Required]
	[StringLength(100)]
	public string FeatureKey { get; set; } = string.Empty;

	public bool IsEnabled { get; set; }

	/// <summary>Optional JSON payload for per-school feature tuning.</summary>
	public string? ConfigurationJson { get; set; }
}

/// <summary>Request body for checking whether a feature is enabled for a school.</summary>
public class CheckFeatureViewModel
{
	[Required]
	public Guid SchoolId { get; set; }

	[Required]
	[StringLength(100)]
	public string FeatureKey { get; set; } = string.Empty;
}
