namespace TechHub.Core.Configuration;

/// <summary>
/// Binds the "ImageGeneration" appsettings section.
/// The provider name selects which agent implementation the factory resolves,
/// so the image-generation agent is fully swappable via configuration alone.
/// </summary>
public class ImageGenerationSettings
{
	public const string SectionName = "ImageGeneration";

	/// <summary>Active agent name — must match a key in <see cref="Providers"/>.</summary>
	public string Provider { get; set; } = "OpenAI";

	/// <summary>Maximum accepted prompt length (characters).</summary>
	public int MaxPromptLength { get; set; } = 4000;

	/// <summary>Maximum time (seconds) allowed for a single agent call.</summary>
	public int TimeoutSeconds { get; set; } = 60;

	/// <summary>Default width for generated images.</summary>
	public int DefaultWidth { get; set; } = 1024;

	/// <summary>Default height for generated images.</summary>
	public int DefaultHeight { get; set; } = 1024;

	/// <summary>
	/// Maximum number of images a single lesson may auto-generate (clamped).
	/// A teacher can request 1..N images at lesson submission time.
	/// </summary>
	public int MaxImagesPerLesson { get; set; } = 5;

	/// <summary>Per-provider credentials / options keyed by provider name.</summary>
	public Dictionary<string, ImageGenerationProviderOptions> Providers { get; set; } = new();
}

public class ImageGenerationProviderOptions
{
	public string? ApiKey { get; set; }
	public string? BaseUrl { get; set; }
	public string? Model { get; set; }
	public string? Endpoint { get; set; }
	public string? Size { get; set; }

	/// <summary>
	/// Optional quality hint for OpenAI GPT Image (low / medium / high / auto).
	/// Only sent when set, so the provider default is used otherwise.
	/// </summary>
	public string? Quality { get; set; }
}
