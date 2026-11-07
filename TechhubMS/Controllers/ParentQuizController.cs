using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.ParentQuizModels;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers
{
	/// <summary>
	/// Practice quizzes a parent sets for their own child. Parents only; a parent can act only
	/// for children linked to them (enforced in <see cref="IParentQuizService"/>).
	/// </summary>
	[ApiController]
	[Route("api/parent-quiz")]
	[Authorize(Roles = "Parent")]
	public class ParentQuizController : ControllerBase
	{
		private readonly IParentQuizService _service;

		public ParentQuizController(IParentQuizService service)
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

		/// <summary>The child's subjects and topics, with how many usable questions each has by difficulty.</summary>
		[HttpGet("children/{studentId:guid}/subjects")]
		public async Task<IActionResult> GetChildSubjects(Guid studentId)
			=> Map(await _service.GetChildSubjectsAsync(studentId, User.GetAuthenticatedUserClaims()));

		/// <summary>The parent's saved quiz defaults.</summary>
		[HttpGet("settings")]
		public async Task<IActionResult> GetSettings()
			=> Map(await _service.GetSettingsAsync(User.GetAuthenticatedUserClaims()));

		[HttpPut("settings")]
		public async Task<IActionResult> SaveSettings([FromBody] ParentQuizSettingsViewModel model)
			=> Map(await _service.SaveSettingsAsync(model, User.GetAuthenticatedUserClaims()));

		/// <summary>Create and assign a quiz. Only studentId and subjectId are required; everything else uses the saved settings.</summary>
		[HttpPost]
		public async Task<IActionResult> Create([FromBody] CreateParentQuizViewModel model)
			=> Map(await _service.CreateAsync(model, User.GetAuthenticatedUserClaims()));

		/// <summary>The parent's quizzes, newest first; <c>?studentId=</c> narrows to one child.</summary>
		[HttpGet]
		public async Task<IActionResult> List([FromQuery] Guid? studentId)
			=> Map(await _service.ListForParentAsync(studentId, User.GetAuthenticatedUserClaims()));

		/// <summary>Score and a question-by-question review with the correct answers. Available once the child has finished.</summary>
		[HttpGet("{quizId:guid}/result")]
		public async Task<IActionResult> GetResult(Guid quizId)
			=> Map(await _service.GetResultForParentAsync(quizId, User.GetAuthenticatedUserClaims()));

		/// <summary>Cancel a quiz the child hasn't started.</summary>
		[HttpDelete("{quizId:guid}")]
		public async Task<IActionResult> Cancel(Guid quizId)
			=> Map(await _service.CancelAsync(quizId, User.GetAuthenticatedUserClaims()));
	}
}
