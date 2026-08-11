using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

/// <summary>
/// AI teaching-material generation (image first).
/// Feature-gated per school via the SchoolFeature table.
/// </summary>
[ApiController]
[Route("api/image-generation")]
[Authorize]
public class ImageGenerationController : ControllerBase
{
	private readonly IImageGenerationService _generationService;

	public ImageGenerationController(IImageGenerationService generationService)
	{
		_generationService = generationService;
	}

	/// <summary>
	/// Generates an image for a lesson from its aim + objectives (or an edited
	/// prompt), uploads it to Cloudinary and attaches it as LessonMedia.
	/// </summary>
	[HttpPost("lesson/{lessonId}/generate")]
	public async Task<IActionResult> GenerateImage(Guid lessonId, [FromBody] GenerateImageViewModel? model)
	{
		var claims = GetClaims();
		var result = await _generationService.GenerateImageAsync(
			lessonId, model ?? new GenerateImageViewModel(), claims);
		return MapResult(result);
	}

	/// <summary>
	/// Returns the last (most recent) prompt used for a lesson so teachers can
	/// review/edit it before regenerating.
	/// </summary>
	[HttpGet("lesson/{lessonId}/prompt")]
	public async Task<IActionResult> GetLastPrompt(Guid lessonId)
	{
		var claims = GetClaims();
		var result = await _generationService.GetLastPromptAsync(lessonId, claims);
		return MapResult(result);
	}

	/// <summary>
	/// Returns the full generation/prompt history for a lesson (newest first).
	/// </summary>
	[HttpGet("lesson/{lessonId}/history")]
	public async Task<IActionResult> GetHistory(Guid lessonId)
	{
		var claims = GetClaims();
		var result = await _generationService.GetGenerationHistoryAsync(lessonId, claims);
		return MapResult(result);
	}

	/// <summary>
	/// Returns the successfully generated AI images (Cloudinary URLs) for a
	/// lesson so the frontend can render them directly.
	/// </summary>
	[HttpGet("lesson/{lessonId}/images")]
	public async Task<IActionResult> GetGeneratedImages(Guid lessonId)
	{
		var claims = GetClaims();
		var result = await _generationService.GetGeneratedImagesAsync(lessonId, claims);
		return MapResult(result);
	}

	/// <summary>
	/// Returns the image-generation job status for a lesson, including a
	/// friendly message when the lesson has not been approved yet.
	/// </summary>
	[HttpGet("lesson/{lessonId}/status")]
	public async Task<IActionResult> GetGenerationStatus(Guid lessonId)
	{
		var claims = GetClaims();
		var result = await _generationService.GetGenerationStatusAsync(lessonId, claims);
		return MapResult(result);
	}

	private AuthenticatedUserClaims GetClaims() => new()
	{
		UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
		SchoolId = User.FindFirst("SchoolId")?.Value,
		Role = User.FindFirst(ClaimTypes.Role)?.Value,
		Email = User.FindFirst(ClaimTypes.Email)?.Value
	};

	private IActionResult MapResult(BaseResponse result) => result.ResponseCode switch
	{
		ResponseCode.successful => Ok(result),
		ResponseCode.NotFound => NotFound(result),
		ResponseCode.Forbidden => StatusCode(403, result),
		ResponseCode.Unauthorized => Unauthorized(result),
		_ => BadRequest(result)
	};
}
