using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.classroom;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

// Lets a Parent see topics taught to their child within a date range, see
// available question counts by difficulty for chosen topics, and create +
// assign an assessment to their own child in one call. Purely additive —
// reuses IAssessmentService.CreateAssessment/AssignAssessment unchanged.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Parent")]
public class ParentAssessmentController : ControllerBase
{
	private readonly IAssessmentService _assessmentService;

	public ParentAssessmentController(IAssessmentService assessmentService)
	{
		_assessmentService = assessmentService;
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
		_ => BadRequest(response)
	};

	[HttpGet("children/{studentId:guid}/taught-topics")]
	public async Task<IActionResult> GetTaughtTopics(Guid studentId, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GetTaughtTopicsForChildAsync(studentId, fromDate, toDate, claims);
		return MapResponse(result);
	}

	[HttpPost("children/{studentId:guid}/question-availability")]
	public async Task<IActionResult> GetQuestionAvailability(Guid studentId, [FromBody] QuestionAvailabilityViewModel model)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GetQuestionAvailabilityAsync(studentId, model.TopicIds, claims);
		return MapResponse(result);
	}

	[HttpPost("children/{studentId:guid}/quick-create")]
	public async Task<IActionResult> CreateQuickAssessment(Guid studentId, [FromBody] ParentQuickAssessmentViewModel model)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.CreateQuickAssessmentForChildAsync(studentId, model, claims);
		return MapResponse(result);
	}
}
