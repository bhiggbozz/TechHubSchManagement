namespace TechHub.Core.Model;

/// <summary>
/// Payload passed to an <see cref="TechHub.Core.Interface.IImageGenerationAgent"/>.
/// </summary>
public class ImageGenerationRequest
{
	public string Prompt { get; set; } = string.Empty;
	public string? NegativePrompt { get; set; }
	public int? Width { get; set; }
	public int? Height { get; set; }
	public string? Style { get; set; }
}

/// <summary>
/// Result returned by an image-generation agent. The image is carried in
/// memory as bytes so the caller can stream it straight to Cloudinary without
/// an intermediate file round-trip (latency priority).
/// </summary>
public class ImageGenerationResult
{
	public bool Success { get; set; }
	public byte[]? ImageBytes { get; set; }

	/// <summary>MIME type of the produced bytes (e.g. "image/png").</summary>
	public string? ContentType { get; set; }

	/// <summary>Model the provider actually used (for auditing).</summary>
	public string? ModelUsed { get; set; }

	public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of refining a draft teaching-image prompt via an LLM (Claude).
/// A failed result means the caller should fall back to the draft prompt.
/// </summary>
public class PromptRefinementResult
{
	public bool Success { get; set; }

	/// <summary>
	/// First refined prompt, kept for backward compatibility with callers that
	/// only ever requested a single image.
	/// </summary>
	public string Prompt { get; set; } = string.Empty;

	/// <summary>
	/// All refined prompts produced by the LLM. For a multi-image request this
	/// contains one distinct, scaffolded prompt per requested image (ordered
	/// from the foundational idea through how-it-works to a real-life example).
	/// </summary>
	public List<string> Prompts { get; set; } = new();

	/// <summary>The LLM model that produced the refined prompt (for auditing).</summary>
	public string? ModelUsed { get; set; }

	public string? ErrorMessage { get; set; }

	/// <summary>
	/// True when the LLM refused to produce a prompt because the teacher's
	/// requested materials do not align with the lesson's subject, aim or
	/// objectives. When declined, no image should be generated.
	/// </summary>
	public bool Declined { get; set; }

	/// <summary>The LLM's reason for declining the material request.</summary>
	public string? DeclineReason { get; set; }
}
