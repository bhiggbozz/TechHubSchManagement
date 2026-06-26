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
public class QuizController : ControllerBase
{
	private readonly IQuizService _quizService;

	public QuizController(IQuizService quizService)
	{
		_quizService = quizService;
	}

	// ═══════════════════════════════════════════════════════════════════════
	// EXISTING ENDPOINTS
	// ═══════════════════════════════════════════════════════════════════════

	[HttpPost("create")]
	[Authorize]
	public async Task<IActionResult> CreateQuiz([FromBody] CreateQuizViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.CreateQuiz(model, claims);
		return MapResponse(response);
	}

	[HttpPost("assessments")]
	[Authorize]
	public async Task<IActionResult> CreateAssessment([FromBody] CreateAssessmentViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.CreateAssessment(model, claims);
		return MapResponse(response);
	}

	[HttpPost("configure")]
	[Authorize]
	public async Task<IActionResult> ConfigureQuiz([FromBody] ConfigureQuizViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.ConfigureQuiz(model, claims);
		return MapResponse(response);
	}

	[HttpPatch("lesson/{lessonId}/attach")]
	[Authorize]
	public async Task<IActionResult> AttachQuizToLesson(
		Guid lessonId,
		[FromBody] AttachQuizViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.AttachQuizToLesson(lessonId, model, claims);
		return MapResponse(response);
	}

	[HttpGet("lesson/{lessonId}")]
	[Authorize]
	public async Task<IActionResult> GetQuizByLesson(Guid lessonId)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetQuizByLesson(lessonId, claims);
		return MapResponse(response);
	}

	// ═══════════════════════════════════════════════════════════════════════
	// QUIZ CONFIG
	// ═══════════════════════════════════════════════════════════════════════

	[HttpPost("config")]
	[Authorize]
	public async Task<IActionResult> SaveQuizConfig([FromBody] QuizConfigViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.SaveQuizConfig(model, claims);
		return MapResponse(response);
	}

	[HttpGet("config")]
	[Authorize]
	public async Task<IActionResult> GetQuizConfig()
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetQuizConfig(claims);
		return MapResponse(response);
	}

	// ═══════════════════════════════════════════════════════════════════════
	// ATTEMPT (student)
	// ═══════════════════════════════════════════════════════════════════════

	[HttpGet("student/lesson/{lessonId}/display")]
	[Authorize]
	public async Task<IActionResult> GetStudentQuizDisplay(Guid lessonId)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetStudentQuizDisplay(lessonId, claims);
		return MapResponse(response);
	}

	[HttpGet("code/{quizCode}/display")]
	[Authorize]
	public async Task<IActionResult> GetStudentQuizDisplayByCode(string quizCode)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetStudentQuizDisplayByCode(quizCode, claims);
		return MapResponse(response);
	}

	[HttpPost("attempt/start")]
	[Authorize]
	public async Task<IActionResult> StartQuizAttempt([FromBody] StartQuizViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.StartQuizAttempt(model, claims);
		return MapResponse(response);
	}

	[HttpPost("attempt/{attemptId}/submit")]
	[Authorize]
	public async Task<IActionResult> SubmitQuizAttempt(
		Guid attemptId,
		[FromBody] SubmitQuizViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.SubmitQuizAttempt(attemptId, model, claims);
		return MapResponse(response);
	}

	[HttpGet("attempt/{attemptId}/result")]
	[Authorize]
	public async Task<IActionResult> GetQuizResult(Guid attemptId)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetQuizResult(attemptId, claims);
		return MapResponse(response);
	}

	// ═══════════════════════════════════════════════════════════════════════
	// GRADING (teacher)
	// ═══════════════════════════════════════════════════════════════════════

	[HttpGet("grading/pending")]
	[Authorize]
	public async Task<IActionResult> GetPendingGrades()
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetPendingGrades(claims);
		return MapResponse(response);
	}

	[HttpPost("grading/{answerId}/grade")]
	[Authorize]
	public async Task<IActionResult> GradeAnswer(
		Guid answerId,
		[FromBody] GradeAnswerViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GradeAnswer(answerId, model, claims);
		return MapResponse(response);
	}

	// ═══════════════════════════════════════════════════════════════════════
	// ASSESSMENT SETS
	// ═══════════════════════════════════════════════════════════════════════

	[HttpPost("assessment-sets")]
	[Authorize]
	public async Task<IActionResult> CreateAssessmentSet([FromBody] CreateAssessmentSetViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.CreateAssessmentSet(model, claims);
		return MapResponse(response);
	}

	[HttpPost("assessment-sets/{id}/update")]
	[Authorize]
	public async Task<IActionResult> UpdateAssessmentSet(Guid id, [FromBody] UpdateAssessmentSetViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.UpdateAssessmentSet(id, model, claims);
		return MapResponse(response);
	}

	[HttpDelete("assessment-sets/{id}")]
	[Authorize]
	public async Task<IActionResult> DeleteAssessmentSet(Guid id)
	{
		var claims = GetUserClaims();
		var response = await _quizService.DeleteAssessmentSet(id, claims);
		return MapResponse(response);
	}

	[HttpGet("assessment-sets")]
	[Authorize]
	public async Task<IActionResult> GetAssessmentSets()
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetAssessmentSets(claims);
		return MapResponse(response);
	}

	[HttpGet("assessment-sets/{id}")]
	[Authorize]
	public async Task<IActionResult> GetAssessmentSet(Guid id)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetAssessmentSet(id, claims);
		return MapResponse(response);
	}

	[HttpPatch("lesson/{lessonId}/assessment-set")]
	[Authorize]
	public async Task<IActionResult> AttachAssessmentSetToLesson(Guid lessonId, [FromBody] AttachAssessmentSetViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.AttachAssessmentSetToLesson(lessonId, model, claims);
		return MapResponse(response);
	}

	[HttpGet("lesson/{lessonId}/assessment-set")]
	[Authorize]
	public async Task<IActionResult> GetLessonAssessmentSet(Guid lessonId)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetLessonAssessmentSet(lessonId, claims);
		return MapResponse(response);
	}

	[HttpGet("lesson/{lessonId}/assessment-config")]
	[Authorize]
	public async Task<IActionResult> GetLessonAssessmentConfig(Guid lessonId)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetLessonAssessmentConfig(lessonId, claims);
		return MapResponse(response);
	}

	// ═══════════════════════════════════════════════════════════════════════
	// ANALYTICS
	// ═══════════════════════════════════════════════════════════════════════

	[HttpGet("lesson/{lessonId}/results")]
	[Authorize]
	public async Task<IActionResult> GetLessonQuizResults(Guid lessonId)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetLessonQuizResults(lessonId, claims);
		return MapResponse(response);
	}

	[HttpGet("lesson/{lessonId}/analytics")]
	[Authorize]
	public async Task<IActionResult> GetLessonQuizAnalytics(Guid lessonId)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetLessonQuizAnalytics(lessonId, claims);
		return MapResponse(response);
	}

	[HttpGet("student/{studentId}/history")]
	[Authorize]
	public async Task<IActionResult> GetStudentQuizHistory(Guid studentId)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetStudentQuizHistory(studentId, claims);
		return MapResponse(response);
	}

	// ═══════════════════════════════════════════════════════════════════════
	// HELPERS
	// ═══════════════════════════════════════════════════════════════════════

	private AuthenticatedUserClaims GetUserClaims() => new AuthenticatedUserClaims
	{
		UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
		SchoolId = User.FindFirst("SchoolId")?.Value,
		Role = User.FindFirst(ClaimTypes.Role)?.Value
	};

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
}
