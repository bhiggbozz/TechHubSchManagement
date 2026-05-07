using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.ViewModels.Board;
using TechHub.Core.ViewModels.Board.Manifest;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

[ApiController]
[Route("api/board")]
public class BoardSessionController : ControllerBase
{
    private readonly IBoardSessionService _boardSessionService;

    public BoardSessionController(IBoardSessionService boardSessionService)
    {
        _boardSessionService = boardSessionService;
    }

    /// <summary>
    /// Receives 1-minute board batches during a live class.
    /// Publishes to RabbitMQ queue and returns immediately.
    /// </summary>
    [HttpPost("session/{sessionId}/batch")]
    [Authorize(Roles = "SubjectTeacher,HeadTeacher")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubmitBatch(
        [FromRoute] string sessionId,
        [FromBody] BoardBatchViewModel model)
    {
        var claims = User.GetAuthenticatedUserClaims();

        if (claims == null || string.IsNullOrEmpty(claims.UserId))
        {
            return Unauthorized(new BaseResponse
            {
                ResponseCode = ResponseCode.Unauthorized,
                ResponseMessage = "Invalid user claims",
                Status = "failed"
            });
        }

        if (sessionId != model.SessionId)
        {
            return BadRequest(new BaseResponse
            {
                ResponseCode = ResponseCode.BadRequest,
                ResponseMessage = $"Session ID in route ({sessionId}) does not match body ({model.SessionId})",
                Status = "failed"
            });
        }

        await _boardSessionService.PublishBatchAsync(sessionId, model, claims);

        return NoContent();
    }

    /// <summary>
    /// Saves session manifest when class ends.
    /// Called once to finalize the recording session.
    /// </summary>
    [HttpPost("session/{sessionId}/manifest")]
    [Authorize(Roles = "SubjectTeacher,HeadTeacher")]
    [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BaseResponse>> SubmitManifest(
        [FromRoute] string sessionId,
        [FromBody] SessionManifestViewModel model)
    {
        var claims = User.GetAuthenticatedUserClaims();

        if (claims == null || string.IsNullOrEmpty(claims.UserId))
        {
            return Unauthorized(new BaseResponse
            {
                ResponseCode = ResponseCode.Unauthorized,
                ResponseMessage = "Invalid user claims",
                Status = "failed"
            });
        }

        var result = await _boardSessionService.SaveManifestAsync(sessionId, model, claims);

        if (result.ResponseCode == ResponseCode.BadRequest)
        {
            return BadRequest(result);
        }

        if (result.ResponseCode == ResponseCode.Unauthorized)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get a session by ID (for debugging/admin purposes)
    /// </summary>
    [HttpGet("session/{sessionId}")]
    [Authorize(Roles = "SubjectTeacher,HeadTeacher,Admin")]
    [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse>> GetSession([FromRoute] string sessionId)
    {
        var claims = User.GetAuthenticatedUserClaims();

        if (claims == null || string.IsNullOrEmpty(claims.SchoolId))
        {
            return Unauthorized(new BaseResponse
            {
                ResponseCode = ResponseCode.Unauthorized,
                ResponseMessage = "Invalid user claims",
                Status = "failed"
            });
        }

        var session = await _boardSessionService.GetSessionAsync(sessionId, claims.SchoolId);

        if (session == null)
        {
            return NotFound(new BaseResponse
            {
                ResponseCode = ResponseCode.NotFound,
                ResponseMessage = $"Session {sessionId} not found",
                Status = "failed"
            });
        }

        return Ok(new BaseResponse
        {
            ResponseCode = ResponseCode.Ok,
            ResponseMessage = "Session retrieved successfully",
            Status = "success",
            Data = session
        });
    }
}
