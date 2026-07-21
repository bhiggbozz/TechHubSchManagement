using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.classroom;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssessmentController : ControllerBase
{
	private readonly IAssessmentService _assessmentService;

	public AssessmentController(IAssessmentService assessmentService)
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

	[HttpPost("create")]
	[Authorize]
	public async Task<IActionResult> CreateAssessment([FromBody] CreateAssessmentViewModel model)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.CreateAssessment(model, claims);
		return MapResponse(result);
	}

	[HttpPost("assign")]
	[Authorize]
	public async Task<IActionResult> AssignAssessment([FromBody] AssignAssessmentViewModel model)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.AssignAssessment(model, claims);
		return MapResponse(result);
	}

	[HttpGet("student/list")]
	[Authorize]
	public async Task<IActionResult> GetStudentAssessments()
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GetStudentAssessments(claims);
		return MapResponse(result);
	}

	[HttpGet("assigned")]
	[Authorize]
	public async Task<IActionResult> GetAssignedAssessments()
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GetAssignedAssessments(claims);
		return MapResponse(result);
	}

	[HttpGet("teacher/list")]
	[Authorize]
	public async Task<IActionResult> GetTeacherAssessments()
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GetTeacherAssessments(claims);
		return MapResponse(result);
	}

	[HttpGet("{assessmentId}/assignments")]
	[Authorize]
	public async Task<IActionResult> GetAssessmentAssignments(Guid assessmentId)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GetAssessmentAssignments(assessmentId, claims);
		return MapResponse(result);
	}

	[HttpGet("code/{code}/detail")]
	[Authorize]
	public async Task<IActionResult> GetAssessmentDetailByCode(string code)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GetAssessmentDetailByCode(code, claims);
		return MapResponse(result);
	}

	[HttpGet("{assessmentId}/detail")]
	[Authorize]
	public async Task<IActionResult> GetAssessmentDetail(Guid assessmentId)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GetAssessmentDetail(assessmentId, claims);
		return MapResponse(result);
	}

	[HttpPost("{assessmentId}/start")]
	[Authorize]
	public async Task<IActionResult> StartAttempt(Guid assessmentId)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.StartAttempt(assessmentId, claims);
		return MapResponse(result);
	}

	[HttpPost("answer")]
	[Authorize]
	public async Task<IActionResult> SubmitAnswer([FromBody] SubmitAssessmentAnswerViewModel model)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.SubmitAnswer(model, claims);
		return MapResponse(result);
	}

	[HttpPost("submit-all")]
	[Authorize]
	public async Task<IActionResult> SubmitAllAnswers([FromBody] SubmitAssessmentBatchViewModel model)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.SubmitAllAnswers(model, claims);
		return MapResponse(result);
	}

	[HttpPost("{attemptId}/submit")]
	[Authorize]
	public async Task<IActionResult> SubmitAttempt(Guid attemptId)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.SubmitAttempt(attemptId, claims);
		return MapResponse(result);
	}

	[HttpGet("result/{attemptId}")]
	[Authorize]
	public async Task<IActionResult> GetResult(Guid attemptId)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GetResult(attemptId, claims);
		return MapResponse(result);
	}

	[HttpGet("{assessmentId}/history")]
	[Authorize]
	public async Task<IActionResult> GetAttemptHistory(Guid assessmentId)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GetAttemptHistory(assessmentId, claims);
		return MapResponse(result);
	}

	[HttpGet("grading/pending")]
	[Authorize]
	public async Task<IActionResult> GetPendingGrading()
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GetPendingGrading(claims);
		return MapResponse(result);
	}

	[HttpPost("grading/{answerId}/grade")]
	[Authorize]
	public async Task<IActionResult> GradeAnswer(Guid answerId, [FromBody] GradeAssessmentAnswerViewModel model)
	{
		var claims = GetUserClaims();
		var result = await _assessmentService.GradeAnswer(answerId, model, claims);
		return MapResponse(result);
	}
}
