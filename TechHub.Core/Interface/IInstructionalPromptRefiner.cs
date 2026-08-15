using System.Threading;
using System.Threading.Tasks;
using TechHub.Core.Model;

namespace TechHub.Core.Interface;

/// <summary>
/// Refines a draft teaching-image prompt into a stronger, pedagogically
/// focused prompt using an LLM (Claude). The draft is composed from the lesson
/// aim + objectives + the teacher's requested material words, and the refined
/// prompt is what gets saved (LessonGenerationPrompt.PromptText) and then sent
/// to the image-generation agent.
/// </summary>
public interface IInstructionalPromptRefiner
{
	/// <summary>
	/// Returns the refined prompt(s), or a failed result when refinement is
	/// unavailable (no API key, disabled, or LLM error) so the caller can fall
	/// back to the draft prompt. The LLM is also instructed to DECLINE (with
	/// reasons) when the teacher's requested materials do not align with the
	/// lesson's subject, aim or objectives — the caller must not generate an
	/// image in that case.
	/// </summary>
	Task<PromptRefinementResult> RefineLessonImagePromptAsync(
		string draftPrompt,
		string? className,
		int imageCount = 1,
		CancellationToken cancellationToken = default);
}
