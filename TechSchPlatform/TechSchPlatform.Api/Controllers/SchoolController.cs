using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechSchPlatform.Api.Extensions;
using TechSchPlatform.Core;
using TechSchPlatform.Core.Model;
using TechSchPlatform.Core.ViewModel.Platform;
using TechSchPlatform.Service.Interfaces;

namespace TechSchPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchoolController : ControllerBase
{
    private readonly ISchoolService _schoolService;

    public SchoolController(ISchoolService schoolService)
    {
        _schoolService = schoolService;
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

    /// <summary>
    /// Create a school record directly (platform admin). School code defaults to the school Id.
    /// POST /api/School/createschool
    /// </summary>
    [HttpPost("createschool")]
    [Authorize(Roles = "PlatformAdmin,PlatformSuperAdmin")]
    public async Task<IActionResult> CreateSchool([FromBody] CreateSchoolViewModel model)
    {
        var claims = User.GetAuthenticatedUserClaims();
        var result = await _schoolService.CreateSchoolAsync(model, claims);
        return MapResponse(result);
    }

    /// <summary>
    /// Get states by country.
    /// POST /api/School/getState
    /// </summary>
    [HttpPost("getState")]
    public async Task<IActionResult> GetStates([FromBody] GetStatesViewModel model)
    {
        var result = await _schoolService.GetStatesAsync(model.CountryId);
        return MapResponse(result);
    }

    /// <summary>
    /// Full provision: school + tenant + admin user + email (platform admin).
    /// POST /api/School/provision
    /// </summary>
    [HttpPost("provision")]
    [Authorize(Roles = "PlatformAdmin,PlatformSuperAdmin")]
    public async Task<IActionResult> ProvisionSchool([FromBody] ProvisionSchoolViewModel model)
    {
        var claims = User.GetAuthenticatedUserClaims();
        var result = await _schoolService.ProvisionSchoolAsync(model, claims);
        return MapResponse(result);
    }

    /// <summary>
    /// Submit a school registration request (anonymous).
    /// POST /api/School/register
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterSchool([FromBody] SchoolRegistrationRequestViewModel model)
    {
        var result = await _schoolService.SubmitRegistrationRequestAsync(model);
        return MapResponse(result);
    }

    /// <summary>
    /// List registration requests (anonymous).
    /// GET /api/School/registration-requests?status=Pending
    /// </summary>
    [HttpGet("registration-requests")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRegistrationRequests([FromQuery] string? status = null)
    {
        var result = await _schoolService.GetRegistrationRequestsAsync(status);
        return MapResponse(result);
    }

    /// <summary>
    /// Approve a school registration request (platform admin).
    /// POST /api/School/approve/{requestId}
    /// </summary>
    [HttpPost("approve/{requestId:guid}")]
    [Authorize(Roles = "PlatformAdmin,PlatformSuperAdmin")]
    public async Task<IActionResult> ApproveRegistration(Guid requestId)
    {
        var claims = User.GetAuthenticatedUserClaims();
        var result = await _schoolService.ApproveRegistrationRequestAsync(requestId, claims);
        return MapResponse(result);
    }

    /// <summary>
    /// Reject a school registration request (platform admin).
    /// POST /api/School/reject/{requestId}
    /// </summary>
    [HttpPost("reject/{requestId:guid}")]
    [Authorize(Roles = "PlatformAdmin,PlatformSuperAdmin")]
    public async Task<IActionResult> RejectRegistration(Guid requestId, [FromBody] string reason)
    {
        var claims = User.GetAuthenticatedUserClaims();
        var result = await _schoolService.RejectRegistrationRequestAsync(requestId, reason, claims);
        return MapResponse(result);
    }

    /// <summary>
    /// Edit school info (platform admin).
    /// PUT /api/School/edit/{schoolId}
    /// </summary>
    [HttpPut("edit/{schoolId:guid}")]
    [Authorize(Roles = "PlatformAdmin,PlatformSuperAdmin")]
    public async Task<IActionResult> EditSchool(Guid schoolId, [FromBody] SchoolEditViewModel model)
    {
        var claims = User.GetAuthenticatedUserClaims();
        var result = await _schoolService.EditSchoolInfoAsync(schoolId, model, claims);
        return MapResponse(result);
    }

    /// <summary>
    /// All schools with their status (approved vs pending request).
    /// GET /api/School/schools-status
    /// </summary>
    [HttpGet("schools-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllSchoolsWithStatus()
    {
        var result = await _schoolService.GetAllSchoolsWithStatusAsync();
        return Ok(result);
    }

    /// <summary>
    /// Pending school registration request ids.
    /// GET /api/School/pending
    /// </summary>
    [HttpGet("pending")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPendingSchoolIds()
    {
        var result = await _schoolService.GetPendingSchoolIdsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Approval status of a school.
    /// GET /api/School/{schoolId}/approval-status
    /// </summary>
    [HttpGet("{schoolId:guid}/approval-status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSchoolApprovalStatus(Guid schoolId)
    {
        var result = await _schoolService.GetSchoolApprovalStatusAsync(schoolId);
        return MapResponse(result);
    }
}