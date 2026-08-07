using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Platform;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

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

    private AuthenticatedUserClaims GetUserClaims() => new()
    {
        UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        Role = User.FindFirst(ClaimTypes.Role)?.Value,
        Email = User.FindFirst(ClaimTypes.Email)?.Value
    };

    private IActionResult MapResponse(BaseResponse response) => response.ResponseCode switch
    {
        "99000" => Ok(response),
        "99134" => NotFound(response),
        "AX1003" => StatusCode(StatusCodes.Status403Forbidden, response),
        "99107" => Unauthorized(response),
        "99161" => StatusCode(StatusCodes.Status409Conflict, response),
        "99001" => BadRequest(response),
        _ => BadRequest(response)
    };

    [HttpPost("create")]
    [Authorize(Roles = "PlatformSuperAdmin,PlatformAdmin")]
    public async Task<IActionResult> CreatePlatformUser([FromBody] CreatePlatformAdminViewModel model)
    {
        var claims = GetUserClaims();
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
        var result = await _platformAdminService.GetPlatformUsersAsync(GetUserClaims());
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