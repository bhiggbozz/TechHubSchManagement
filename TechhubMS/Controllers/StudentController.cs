using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentController : ControllerBase
{
    private readonly IStudentDashboardService _studentDashboardService;

    public StudentController(IStudentDashboardService studentDashboardService)
    {
        _studentDashboardService = studentDashboardService;
    }

    private AuthenticatedUserClaims GetUserClaims() => new()
    {
        UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        SchoolId = User.FindFirst("SchoolId")?.Value,
        Role = User.FindFirst(ClaimTypes.Role)?.Value
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

    [HttpGet("dashboard/stats")]
    [Authorize]
    public async Task<IActionResult> GetDashboardStats()
    {
        var claims = GetUserClaims();
        var result = await _studentDashboardService.GetStudentDashboardStatsAsync(claims);
        return MapResponse(result);
    }
}
