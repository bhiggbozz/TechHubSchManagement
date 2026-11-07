using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.ParentQuizModels;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers
{
	/// <summary>The student's side of the practice quizzes their parents set ("From your parent" on the dashboard).</summary>
	[ApiController]
	[Route("api/student/parent-quiz")]
	[Authorize(Roles = "Student")]
	public class StudentParentQuizController : ControllerBase
	{
		private readonly IParentQuizService _service;

		public StudentParentQuizController(IParentQuizService service)
		{
			_service = service;
		}

		private IActionResult Map(BaseResponse response) => response?.ResponseCode switch
		{
			ResponseCode.successful => Ok(response),
			ResponseCode.BadRequest => BadRequest(response),
			ResponseCode.NotFound => NotFound(response),
			ResponseCode.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
			ResponseCode.Unauthorized => Unauthorized(response),
			ResponseCode.Conflict => Conflict(response),
			_ => StatusCode(StatusCodes.Status500InternalServerError, response)
		};

		[HttpGet]
		public async Task<IActionResult> List()
			=> Map(await _service.ListForStudentAsync(User.GetAuthenticatedUserClaims()));

		/// <summary>Start the quiz, or resume it if already started. Returns the questions (no answers).</summary>
		[HttpPost("{quizId:guid}/start")]
		public async Task<IActionResult> Start(Guid quizId)
			=> Map(await _service.StartAsync(quizId, User.GetAuthenticatedUserClaims()));

		/// <summary>Submit every answer at once; the quiz is marked and the result returned.</summary>
		[HttpPost("{quizId:guid}/submit")]
		public async Task<IActionResult> Submit(Guid quizId, [FromBody] SubmitParentQuizViewModel model)
			=> Map(await _service.SubmitAsync(quizId, model, User.GetAuthenticatedUserClaims()));

		[HttpGet("{quizId:guid}/result")]
		public async Task<IActionResult> GetResult(Guid quizId)
			=> Map(await _service.GetResultForStudentAsync(quizId, User.GetAuthenticatedUserClaims()));
	}
}
