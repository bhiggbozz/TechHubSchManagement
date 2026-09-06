using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModels.Board;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

/// <summary>
/// Student equivalent of BoardSessionController's live batch endpoint, for
/// recording a whiteboard as part of a group-content submission. Published to a
/// dedicated RabbitMQ queue (GroupContentBatchQueue) so it never shares a queue,
/// worker, or Mongo collection with the teacher's live board pipeline.
///
/// No sessionId: keyed directly by GroupId (route/body) + the caller's StudentId
/// (JWT claims) — a real business key the caller already has, same principle the
/// teacher flow already uses in practice (its "sessionId" is set to LessonId by
/// frontend convention), just made explicit in the schema here.
/// </summary>
[ApiController]
[Route("api/board/group-content")]
public class GroupContentBoardController : ControllerBase
{
    private readonly IGroupContentBoardSessionService _groupContentBoardSessionService;

    public GroupContentBoardController(IGroupContentBoardSessionService groupContentBoardSessionService)
    {
        _groupContentBoardSessionService = groupContentBoardSessionService;
    }

    /// <summary>
    /// Receives 1-minute board (+ optional per-minute audio URL) batches while a
    /// student is recording content for their group. Publishes to RabbitMQ and
    /// returns immediately.
    /// </summary>
    [HttpPost("group/{groupId}/batch")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitBatch(
        [FromRoute] string groupId,
        [FromBody] GroupContentBoardBatchViewModel model)
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

        var result = await _groupContentBoardSessionService.PublishBatchAsync(groupId, model, claims);

        return result.ResponseCode switch
        {
            ResponseCode.successful => NoContent(),
            ResponseCode.NotFound => NotFound(result),
            ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
            ResponseCode.Unauthorized => Unauthorized(result),
            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// Finalizes a student's group-content board recording — stats, batch refs,
    /// boards, and final concatenated audio URL. Student equivalent of the
    /// teacher's POST session/{sessionId}/manifest.
    /// </summary>
    [HttpPost("group/{groupId}/manifest")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse>> SubmitManifest(
        [FromRoute] string groupId,
        [FromBody] GroupContentManifestViewModel model)
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

        var result = await _groupContentBoardSessionService.SaveManifestAsync(groupId, model, claims);

        return result.ResponseCode switch
        {
            ResponseCode.successful => Ok(result),
            ResponseCode.NotFound => NotFound(result),
            ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
            ResponseCode.Unauthorized => Unauthorized(result),
            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// Tells the frontend what to do with a locally-saved, paused recording:
    /// resume it, wait on approval, or start fresh. Checks the SQL submission
    /// (GroupLessonContent) first — if one exists, that's authoritative — then
    /// falls back to the Mongo recording state (manifest saved vs. batches only).
    /// </summary>
    [HttpGet("group/{groupId}/status")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse>> GetStatus([FromRoute] string groupId)
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

        var result = await _groupContentBoardSessionService.GetStatusAsync(groupId, claims);

        return result.ResponseCode switch
        {
            ResponseCode.successful => Ok(result),
            ResponseCode.NotFound => NotFound(result),
            ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
            ResponseCode.Unauthorized => Unauthorized(result),
            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// Playback/download for a student's board recording — stats, batch refs,
    /// boards, final audio URL. Not Student-only: the classroom's approver
    /// (teacher) needs this to review before approving, so any authenticated
    /// caller is allowed in here and the real check happens in the service —
    /// creator or approver see it at any status, other group members only once
    /// the submission is Approved.
    /// </summary>
    [HttpGet("group/{groupId}/student/{studentId}/manifest")]
    [Authorize]
    [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse>> GetManifestForView(
        [FromRoute] string groupId, [FromRoute] string studentId)
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

        var result = await _groupContentBoardSessionService.GetManifestForViewAsync(groupId, studentId, claims);

        return result.ResponseCode switch
        {
            ResponseCode.successful => Ok(result),
            ResponseCode.NotFound => NotFound(result),
            ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
            ResponseCode.Unauthorized => Unauthorized(result),
            _ => BadRequest(result)
        };
    }

    /// <summary>
    /// One stroke batch (playback chunk) for a student's board recording.
    /// Same access rule as the manifest endpoint above.
    /// </summary>
    [HttpGet("group/{groupId}/student/{studentId}/batch/{batchIndex:int}")]
    [Authorize]
    [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse>> GetBatchForView(
        [FromRoute] string groupId, [FromRoute] string studentId, [FromRoute] int batchIndex)
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

        var result = await _groupContentBoardSessionService.GetBatchForViewAsync(groupId, studentId, batchIndex, claims);

        return result.ResponseCode switch
        {
            ResponseCode.successful => Ok(result),
            ResponseCode.NotFound => NotFound(result),
            ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result),
            ResponseCode.Unauthorized => Unauthorized(result),
            _ => BadRequest(result)
        };
    }
}
