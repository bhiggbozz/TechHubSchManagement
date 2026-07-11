using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PerformanceController : ControllerBase
{
    private readonly IPerformanceDashboardService _dashboardService;
    private readonly IPerformanceAggregationService _aggregationService;
    private readonly IStudentDashboardService _studentDashboardService;

    public PerformanceController(
        IPerformanceDashboardService dashboardService,
        IPerformanceAggregationService aggregationService,
        IStudentDashboardService studentDashboardService)
    {
        _dashboardService = dashboardService;
        _aggregationService = aggregationService;
        _studentDashboardService = studentDashboardService;
    }

    [HttpGet("navbar")]
    public async Task<IActionResult> GetNavbar()
    {
        var claims = GetUserClaims();
        var response = await _dashboardService.GetNavbarAsync(claims);
        return MapResponse(response);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var claims = GetUserClaims();
        var response = await _dashboardService.GetDashboardAsync(claims);
        return MapResponse(response);
    }

    [HttpGet("classroom/{classroomId}")]
    public async Task<IActionResult> GetClassroomDetail(Guid classroomId)
    {
        var claims = GetUserClaims();
        var response = await _dashboardService.GetClassroomDetailAsync(classroomId, claims);
        return MapResponse(response);
    }

    [HttpGet("subject/{subjectId}")]
    public async Task<IActionResult> GetSubjectDetail(Guid subjectId)
    {
        var claims = GetUserClaims();
        var response = await _dashboardService.GetSubjectDetailAsync(subjectId, claims);
        return MapResponse(response);
    }

    [HttpGet("subject/{subjectId}/classrooms")]
    public async Task<IActionResult> GetSubjectClassrooms(Guid subjectId)
    {
        var claims = GetUserClaims();
        var response = await _dashboardService.GetSubjectClassroomsAsync(subjectId, claims);
        return MapResponse(response);
    }

    [HttpGet("subject/{subjectId}/topics")]
    public async Task<IActionResult> GetSubjectTopics(Guid subjectId)
    {
        var claims = GetUserClaims();
        var response = await _dashboardService.GetSubjectTopicsAsync(subjectId, claims);
        return MapResponse(response);
    }

    [HttpGet("student-summary")]
    public async Task<IActionResult> GetStudentSummary()
    {
        var claims = GetUserClaims();
        var response = await _studentDashboardService.GetStudentSummaryAsync(claims);
        return MapResponse(response);
    }

    [HttpPost("lesson/{lessonId}/watch")]
    public async Task<IActionResult> MarkLessonAsWatched(Guid lessonId)
    {
        var claims = GetUserClaims();
        var response = await _studentDashboardService.MarkLessonAsWatchedAsync(lessonId, claims);
        return MapResponse(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var claims = GetUserClaims();
        if (!Guid.TryParse(claims.SchoolId, out var schoolId))
            return Unauthorized();

        if (!Guid.TryParse(claims.UserId, out var userId))
            return Unauthorized();

        if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role)
            || (role != UserRole.Administrator && role != UserRole.SuperAdministrator
                && role != UserRole.HeadTeacher))
            return Forbid();

        await _aggregationService.AggregateSchoolAsync(schoolId);

        return Ok(new BaseResponse
        {
            ResponseCode = "99000",
            ResponseMessage = "Performance data refreshed",
            Status = "successful"
        });
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
}
