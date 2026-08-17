using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TechSchPlatform.Api.Extensions;
using TechSchPlatform.Core;
using TechSchPlatform.Core.Model;
using TechSchPlatform.Core.ViewModel.Platform;
using TechSchPlatform.Service.Interfaces;

namespace TechSchPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PlatformAdminController : ControllerBase
{
    private readonly IPlatformAdminService _platformAdminService;
    private readonly IPlatformAuditService _platformAuditService;

    public PlatformAdminController(IPlatformAdminService platformAdminService, IPlatformAuditService platformAuditService)
    {
        _platformAdminService = platformAdminService;
        _platformAuditService = platformAuditService;
    }

    private IActionResult MapResponse(BaseResponse response) => response.ResponseCode switch
    {
        ResponseCode.successful => Ok(response),
        ResponseCode.NotFound => NotFound(response),
        ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
        ResponseCode.Unauthorized => Unauthorized(response),
        ResponseCode.Conflict => StatusCode(StatusCodes.Status409Conflict, response),
        ResponseCode.BadRequest => BadRequest(response),
        _ => BadRequest(response)
    };

    [HttpPost("create")]
    [Authorize(Roles = "PlatformSuperAdmin,PlatformAdmin")]
    public async Task<IActionResult> CreatePlatformUser([FromBody] CreatePlatformAdminViewModel model)
    {
        var claims = User.GetAuthenticatedUserClaims();
        var result = await _platformAdminService.CreatePlatformUserAsync(model, claims);

        if (result.ResponseCode == ResponseCode.successful)
        {
            await _platformAuditService.LogAsync(
                claims,
                PlatformAuditAction.PlatformUserCreated,
                PlatformAuditAction.EntityPlatformUser,
                null,
                $"Platform user '{model.Username}' created with role '{model.Role}'",
                new { model.Username, model.Email, model.Role });
        }

        return MapResponse(result);
    }

    [HttpGet("users")]
    [Authorize(Roles = "PlatformSuperAdmin,PlatformAdmin")]
    public async Task<IActionResult> GetPlatformUsers()
    {
        var result = await _platformAdminService.GetPlatformUsersAsync(User.GetAuthenticatedUserClaims());
        return MapResponse(result);
    }

    [HttpGet("login-history")]
    [Authorize(Roles = "PlatformSuperAdmin,PlatformAdmin")]
    public async Task<IActionResult> GetLoginHistory([FromQuery] Guid? userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _platformAdminService.GetPlatformLoginHistoryAsync(userId, pageNumber, pageSize);
        return MapResponse(result);
    }

    [HttpGet("audit-logs")]
    [Authorize(Roles = "PlatformSuperAdmin,PlatformAdmin")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] string? action = null, [FromQuery] string? entityType = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _platformAuditService.GetLogsAsync(action, entityType, pageNumber, pageSize);
        return MapResponse(result);
    }
}