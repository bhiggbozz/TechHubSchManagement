using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

/// <summary>
/// Configures which features a school is privileged to use.
/// Managed by platform admins or a school's own Administrators/SuperAdministrators.
/// </summary>
[ApiController]
[Route("api/school-features")]
[Authorize]
public class SchoolFeatureController : ControllerBase
{
	private readonly ISchoolFeatureService _featureService;

	public SchoolFeatureController(ISchoolFeatureService featureService)
	{
		_featureService = featureService;
	}

	/// <summary>
	/// Creates or updates a feature flag for a school (idempotent per feature key).
	/// </summary>
	[HttpPost]
	public async Task<IActionResult> SetFeature([FromBody] SchoolFeatureViewModel model)
	{
		var claims = GetClaims();
		var result = await _featureService.SetFeatureAsync(model, claims);
		return MapResult(result);
	}

	/// <summary>
	/// Lists features for a school. Platform admins may pass ?schoolId; others
	/// always read their own school.
	/// </summary>
	[HttpGet]
	public async Task<IActionResult> GetFeatures([FromQuery] Guid? schoolId)
	{
		var claims = GetClaims();
		var result = await _featureService.GetFeaturesAsync(schoolId, claims);
		return MapResult(result);
	}

	/// <summary>
	/// Returns whether a specific feature is enabled for a school. When
	/// ?schoolId is omitted, the caller's own school (from the JWT) is used, so
	/// school users can call /check?featureKey=... directly.
	/// </summary>
	[HttpGet("check")]
	public async Task<IActionResult> CheckFeature([FromQuery] Guid? schoolId, [FromQuery] string featureKey)
	{
		if (string.IsNullOrWhiteSpace(featureKey))
			return BadRequest(new BaseResponse
			{
				ResponseCode = ResponseCode.BadRequest,
				ResponseMessage = "featureKey is required",
				Status = "failed"
			});

		// Resolve the school: explicit query param wins, otherwise the caller's own.
		var targetSchoolId = schoolId ?? (Guid.TryParse(User.FindFirst("SchoolId")?.Value, out var tokenSchoolId)
			? tokenSchoolId
			: Guid.Empty);

		if (targetSchoolId == Guid.Empty)
			return BadRequest(new BaseResponse
			{
				ResponseCode = ResponseCode.BadRequest,
				ResponseMessage = "schoolId is required (or call with a school-user token)",
				Status = "failed"
			});

		var result = await _featureService.CheckFeatureAsync(targetSchoolId, featureKey);
		return MapResult(result);
	}

	private AuthenticatedUserClaims GetClaims() => new()
	{
		UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
		SchoolId = User.FindFirst("SchoolId")?.Value,
		Role = User.FindFirst(ClaimTypes.Role)?.Value,
		Email = User.FindFirst(ClaimTypes.Email)?.Value
	};

	private IActionResult MapResult(BaseResponse result) => result.ResponseCode switch
	{
		ResponseCode.successful => Ok(result),
		ResponseCode.NotFound => NotFound(result),
		ResponseCode.Forbidden => StatusCode(403, result),
		ResponseCode.Unauthorized => Unauthorized(result),
		_ => BadRequest(result)
	};
}
