using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;

namespace TechHub.Service.Interface;

/// <summary>
/// AI teaching-material generation pipeline (image first).
/// Prompt history is kept per lesson so teachers can edit the last prompt
/// and regenerate; the latest row is always the one used.
/// </summary>
public interface IImageGenerationService
{
	/// <summary>
	/// Generates an image for a lesson from a prompt built out of the lesson's
	/// aim + objectives (or the teacher's own edited prompt), uploads it to
	/// Cloudinary and attaches it as LessonMedia.
	/// </summary>
	Task<BaseResponse> GenerateImageAsync(Guid lessonId, GenerateImageViewModel model, AuthenticatedUserClaims claims);

	/// <summary>
	/// Returns the last (most recent) prompt row for a lesson.
	/// </summary>
	Task<BaseResponse> GetLastPromptAsync(Guid lessonId, AuthenticatedUserClaims claims);

	/// <summary>
	/// Returns the full generation/prompt history for a lesson (newest first).
	/// </summary>
	Task<BaseResponse> GetGenerationHistoryAsync(Guid lessonId, AuthenticatedUserClaims claims);

	/// <summary>
	/// Returns the successfully generated AI images (Cloudinary URLs) for a
	/// lesson so the frontend can render them.
	/// </summary>
	Task<BaseResponse> GetGeneratedImagesAsync(Guid lessonId, AuthenticatedUserClaims claims);

	/// <summary>
	/// Returns the current image-generation job status for a lesson, including
	/// a friendly message when the lesson has not been approved yet (so the
	/// teacher knows no image is available until it is approved).
	/// </summary>
	Task<BaseResponse> GetGenerationStatusAsync(Guid lessonId, AuthenticatedUserClaims claims);
}
