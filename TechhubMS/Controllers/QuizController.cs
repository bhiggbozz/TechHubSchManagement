using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

	// Teacher creates quiz from selected questions
	[HttpPost("create")]
	[Authorize]
	public async Task<IActionResult> CreateQuiz([FromBody] CreateQuizViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.CreateQuiz(model, claims);
		return response.ResponseCode switch
		{
			ResponseCode.successful => Ok(response),
			ResponseCode.NotFound => NotFound(response),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
			ResponseCode.Unauthorized => Unauthorized(response),
			_ => BadRequest(response)
		};
	}

	// Teacher attaches quiz code to lesson
	[HttpPatch("lesson/{lessonId}/attach")]
	[Authorize]
	public async Task<IActionResult> AttachQuizToLesson(
		Guid lessonId,
		[FromBody] AttachQuizViewModel model)
	{
		var claims = GetUserClaims();
		var response = await _quizService.AttachQuizToLesson(lessonId, model, claims);
		return response.ResponseCode switch
		{
			ResponseCode.successful => Ok(response),
			ResponseCode.NotFound => NotFound(response),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
			ResponseCode.Unauthorized => Unauthorized(response),
			_ => BadRequest(response)
		};
	}

	// Student fetches quiz for a lesson
	[HttpGet("lesson/{lessonId}")]
	[Authorize]
	public async Task<IActionResult> GetQuizByLesson(Guid lessonId)
	{
		var claims = GetUserClaims();
		var response = await _quizService.GetQuizByLesson(lessonId, claims);
		return response.ResponseCode switch
		{
			ResponseCode.successful => Ok(response),
			ResponseCode.NotFound => NotFound(response),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
			ResponseCode.Unauthorized => Unauthorized(response),
			_ => BadRequest(response)
		};
	}

	private AuthenticatedUserClaims GetUserClaims() => new AuthenticatedUserClaims
	{
		UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
		SchoolId = User.FindFirst("SchoolId")?.Value,
		Role = User.FindFirst(ClaimTypes.Role)?.Value
	};
}

