using System;

namespace TechHub.Core.Entities;

/// <summary>
/// One row per AI image-generation attempt for a lesson.
///
/// The row stores the exact prompt that was sent to the generation agent,
/// so a teacher who is not satisfied can read the last prompt, tweak it and
/// regenerate — a new row is inserted and becomes the "last prompt".
/// </summary>
public class LessonGenerationPrompt
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid SchoolId { get; set; }
	public Guid LessonId { get; set; }

	/// <summary>Teacher who requested the generation.</summary>
	public Guid CreatedBy { get; set; }

	/// <summary>
	/// The final, fully-composed prompt that was actually sent to the agent.
	/// </summary>
	public string PromptText { get; set; } = string.Empty;

	/// <summary>
	/// Raw prompt supplied by the teacher (if they overrode / edited the
	/// auto-generated one). Null when the prompt was built from lesson data.
	/// </summary>
	public string? TeacherPrompt { get; set; }

	/// <summary>Agent that generated the image (e.g. "Stability", "OpenAI").</summary>
	public string AgentType { get; set; } = string.Empty;

	/// <summary>Optional style hint appended to the prompt.</summary>
	public string? Style { get; set; }

	/// <summary>Pending | Completed | Failed</summary>
	public string Status { get; set; } = "Pending";

	/// <summary>LessonMedia row created for the generated image (null on failure).</summary>
	public Guid? MediaId { get; set; }

	public string? ImageUrl { get; set; }
	public string? ImagePublicId { get; set; }
	public string? ErrorMessage { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public bool IsActive { get; set; } = true;
}
