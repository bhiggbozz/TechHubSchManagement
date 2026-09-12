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
/// (JWT claims) + ContentId (route/body) — a real business key the caller already
/// has by the time recording starts (the GroupLessonContent row is always created
/// first), same principle the teacher flow already uses in practice (its
/// "sessionId" is set to LessonId by frontend convention), just made explicit in
/// the schema here. ContentId scopes each recording to one specific submission so
/// a second recording for a different submission in the same group can never
/// overwrite or leak into this one.
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
    [HttpPost("group/{groupId}/content/{contentId}/batch")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitBatch(
        [FromRoute] string groupId,
        [FromRoute] string contentId,
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

        var result = await _groupContentBoardSessionService.PublishBatchAsync(groupId, contentId, model, claims);

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
    [HttpPost("group/{groupId}/content/{contentId}/manifest")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse>> SubmitManifest(
        [FromRoute] string groupId,
        [FromRoute] string contentId,
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

        var result = await _groupContentBoardSessionService.SaveManifestAsync(groupId, contentId, model, claims);

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
    /// Tells the frontend what to do with a locally-saved, paused recording for
    /// this specific content item: resume it, note it's already recorded, or
    /// start fresh. If this content has already been decided (Approved/Rejected)
    /// that status short-circuits and blocks further recording; otherwise falls
    /// back to the Mongo recording state (manifest saved vs. batches only) for
    /// this exact {groupId, studentId, contentId}.
    /// </summary>
    [HttpGet("group/{groupId}/content/{contentId}/status")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse>> GetStatus([FromRoute] string groupId, [FromRoute] string contentId)
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

        var result = await _groupContentBoardSessionService.GetStatusAsync(groupId, contentId, claims);

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
    [HttpGet("group/{groupId}/content/{contentId}/manifest")]
    [Authorize]
    [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse>> GetManifestForView(
        [FromRoute] string groupId, [FromRoute] string contentId)
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

        var result = await _groupContentBoardSessionService.GetManifestForViewAsync(groupId, contentId, claims);

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
    [HttpGet("group/{groupId}/content/{contentId}/batch/{batchIndex:int}")]
    [Authorize]
    [ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse>> GetBatchForView(
        [FromRoute] string groupId, [FromRoute] string contentId, [FromRoute] int batchIndex)
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

        var result = await _groupContentBoardSessionService.GetBatchForViewAsync(groupId, contentId, batchIndex, claims);

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
