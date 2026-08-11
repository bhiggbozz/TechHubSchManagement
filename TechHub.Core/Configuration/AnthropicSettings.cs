namespace TechHub.Core.Configuration;

/// <summary>
/// Binds the "Anthropic" appsettings section. Used by the Claude-backed
/// instructional prompt refinement step that redefines a draft teaching-image
/// prompt (built from the lesson aim + objectives + the teacher's material
/// words) into a stronger prompt before it is sent to the image agent.
/// </summary>
public class AnthropicSettings
{
	public const string SectionName = "Anthropic";

	/// <summary>Claude API key (sk-ant-...).</summary>
	public string ApiKey { get; set; } = string.Empty;

	/// <summary>Claude model used for prompt refinement.</summary>
	public string Model { get; set; } = "claude-sonnet-4-5";

	/// <summary>Maximum output tokens for the refined prompt.</summary>
	public int MaxTokens { get; set; } = 2048;

	/// <summary>Maximum length of the refined prompt (characters).</summary>
	public int MaxPromptLength { get; set; } = 2000;

	/// <summary>
	/// Whether Claude prompt refinement is enabled. When false (or when no API
	/// key is configured), the draft prompt built from the lesson data is used
	/// as-is so image generation never breaks.
	/// </summary>
	public bool EnablePromptRefinement { get; set; } = true;
}
