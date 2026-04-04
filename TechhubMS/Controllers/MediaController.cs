using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.classroom;
using TechHub.Service.Extension;
using TechHub.Service.Interface;
using TechHub.Service.ViewModels;

namespace TechhubMS.Controllers;

/// <summary>
/// Controller for media file operations
/// Handles upload, status tracking, and media management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MediaController : ControllerBase
{
	private readonly IMediaService _mediaService;

	public MediaController(IMediaService mediaService)
	{
		_mediaService = mediaService;
	}

	#region Upload & Status

	
	[HttpPost("upload")]
	[ProducesResponseType(typeof(UploadMediaResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(UploadMediaResponse), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(UploadMediaResponse), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(UploadMediaResponse), StatusCodes.Status409Conflict)]
	[ProducesResponseType(typeof(UploadMediaResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> UploadMedia([FromForm] IFormFile file,[FromForm] MediaType mediaType,[FromForm] string? displayName = null)
	{
		var userClaims = User.GetAuthenticatedUserClaims();

		if (userClaims == null || string.IsNullOrEmpty(userClaims.UserId))
		{
			return Unauthorized(new UploadMediaResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "User not authenticated",
				Status = "failed"
			});
		}

		var result = await _mediaService.UploadMedia(file,mediaType,displayName,userClaims);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.Conflict => Conflict(result),
			ResponseCode.BadRequest => BadRequest(result),
			ResponseCode.ErrorOccured => StatusCode(StatusCodes.Status500InternalServerError, result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	
	/// Recommended polling strategy:
	/// - Poll every 2 seconds
	/// - Stop when status = 2 (Completed) or 3 (Failed)
	/// - Timeout after 5 minutes (video upload should complete by then)
	/// </remarks>
	/// <response code="200">Status retrieved successfully</response>
	/// <response code="404">Media not found</response>
	/// <response code="500">Server error</response>
	[HttpGet("status/{mediaId}")]
	[ProducesResponseType(typeof(MediaUploadStatusResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(MediaUploadStatusResponse), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(MediaUploadStatusResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> GetUploadStatus(Guid mediaId)
	{
		var result = await _mediaService.GetUploadStatus(mediaId);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.NotFound => NotFound(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	#endregion

	#region Media Management

	/// </remarks>
	/// <response code="200">Media files retrieved successfully</response>
	/// <response code="404">Class preparation not found</response>
	/// <response code="500">Server error</response>
	[HttpGet("class/{classPreparationId}")]
	[ProducesResponseType(typeof(MediaFilesListResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(MediaFilesListResponse), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(MediaFilesListResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> GetClassMediaFiles(Guid classPreparationId)
	{
		var result = await _mediaService.GetClassMediaFiles(classPreparationId);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.NotFound => NotFound(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	
	/// What happens:
	/// 1. Soft delete in database (IsDeleted = true)
	/// 2. Physical deletion from Cloudinary happens in background
	/// 3. Immediate response to user
	/// 
	/// Note: Only unlinking media (not linked to any class) can be deleted by teachers
	/// </remarks>
	/// <response code="200">Media deleted successfully</response>
	/// <response code="401">User not authenticated</response>
	/// <response code="404">Media not found</response>
	/// <response code="500">Server error</response>
	[HttpDelete("delete")]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status404NotFound)]
	[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> DeleteMedia([FromBody] TechHub.Core.ViewModel.DeleteMediaViewModel request)
	{
		var userClaims = User.GetAuthenticatedUserClaims();

		if (userClaims == null || !Guid.TryParse(userClaims.UserId, out var userId))
		{
			return Unauthorized(new BaseResponse
			{
				ResponseCode = ResponseCode.Unauthorized,
				ResponseMessage = "User not authenticated",
				Status = "failed"
			});
		}

		var result = await _mediaService.DeleteMedia(request.MediaId,userId,request.Reason);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.NotFound => NotFound(result),
			ResponseCode.BadRequest => BadRequest(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	#endregion

	/// <summary>
	/// NEW: Request upload token for direct-to-CDN upload
	/// 
	/// RECOMMENDED METHOD for large files (>= 100 MB)
	/// 
	/// WORKFLOW:
	/// 1. Frontend calls this endpoint with file metadata (not file bytes)
	/// 2. Server generates signed token
	/// 3. Frontend uploads directly to Cloudinary
	/// 4. Frontend calls confirm-upload when done
	/// 
	/// BENEFITS:
	/// - Fast uploads (direct to CDN)
	/// - Scalable (1000+ concurrent uploads)
	/// - No server bandwidth used
	/// </summary>
	[HttpPost("request-upload-token")]
	[Authorize(Roles = "HeadTeacher,SubjectTeacher,Administrator,SuperAdministrator")]
	public async Task<IActionResult> RequestUploadToken([FromBody] RequestUploadTokenViewModel model)
	{
		var userClaims = User.GetAuthenticatedUserClaims();
		var result = await _mediaService.RequestUploadToken(model, userClaims);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.BadRequest => BadRequest(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

	/// <summary>
	/// NEW: Confirm upload completed
	/// 
	/// Called by frontend after successful Cloudinary upload
	/// Updates database with upload results
	/// Triggers background jobs (thumbnails, AI analysis)
	/// </summary>
	[HttpPost("confirm-upload")]
	[Authorize(Roles = "HeadTeacher,SubjectTeacher,Administrator,SuperAdministrator")]
	public async Task<IActionResult> ConfirmUpload([FromBody] ConfirmUploadViewModel model)
	{
		var userClaims = User.GetAuthenticatedUserClaims();
		var result = await _mediaService.ConfirmUpload(model, userClaims);

		return result.ResponseCode switch
		{
			ResponseCode.successful => Ok(result),
			ResponseCode.NotFound => NotFound(result),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
			ResponseCode.BadRequest => BadRequest(result),
			_ => StatusCode(StatusCodes.Status500InternalServerError, result)
		};
	}

}

