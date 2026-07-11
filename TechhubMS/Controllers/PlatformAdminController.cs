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

    public PlatformAdminController(IPlatformAdminService platformAdminService)
    {
        _platformAdminService = platformAdminService;
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
    [Authorize(Roles = "PlatformSuperAdmin")]
    public async Task<IActionResult> CreatePlatformAdmin([FromBody] CreatePlatformAdminViewModel model)
    {
        var claims = GetUserClaims();
        var result = await _platformAdminService.CreatePlatformAdminAsync(model, claims);
        return MapResponse(result);
    }
}
