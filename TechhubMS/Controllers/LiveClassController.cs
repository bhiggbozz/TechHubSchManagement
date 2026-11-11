using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.LiveClassModels;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers
{
	/// <summary>
	/// Live classes: a teacher and up to three students they picked. Teachers create/start/end;
	/// the picked students join. Everyone else gets "not found". The live board runs over the
	/// SignalR hub at /hubs/live-board; audio runs over LiveKit using the token returned by join.
	/// </summary>
	[ApiController]
	[Route("api/live-class")]
	[Authorize]
	public class LiveClassController : ControllerBase
	{
		private const string TeacherRoles = "SubjectTeacher,HeadTeacher,ClassTeacher";
		private const string AllRoles = "SubjectTeacher,HeadTeacher,ClassTeacher,Student";

		private readonly ILiveClassService _service;

		public LiveClassController(ILiveClassService service)
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

		/// <summary>Set up a class from an Approved lesson with 1–3 students. It does not start until /start.</summary>
		[HttpPost]
		[Authorize(Roles = TeacherRoles)]
		public async Task<IActionResult> Create([FromBody] CreateLiveClassViewModel model)
			=> Map(await _service.CreateAsync(model, User.GetAuthenticatedUserClaims()));

		/// <summary>Live and upcoming classes (and those finished in the last 7 days) for the signed-in teacher or student.</summary>
		[HttpGet("mine")]
		[Authorize(Roles = AllRoles)]
		public async Task<IActionResult> Mine()
			=> Map(await _service.MineAsync(User.GetAuthenticatedUserClaims()));

		[HttpGet("{id:guid}")]
		[Authorize(Roles = AllRoles)]
		public async Task<IActionResult> Get(Guid id)
			=> Map(await _service.GetAsync(id, User.GetAuthenticatedUserClaims()));

		/// <summary>Go live. Starts the 40-minute clock, opens the audio room and notifies the picked students.</summary>
		[HttpPost("{id:guid}/start")]
		[Authorize(Roles = TeacherRoles)]
		public async Task<IActionResult> Start(Guid id)
			=> Map(await _service.StartAsync(id, User.GetAuthenticatedUserClaims()));

		/// <summary>Enter a live class. Returns the audio token, the board hub details and the time left.</summary>
		[HttpPost("{id:guid}/join")]
		[Authorize(Roles = AllRoles)]
		public async Task<IActionResult> Join(Guid id)
			=> Map(await _service.JoinAsync(id, User.GetAuthenticatedUserClaims()));

		[HttpPost("{id:guid}/end")]
		[Authorize(Roles = TeacherRoles)]
		public async Task<IActionResult> End(Guid id)
			=> Map(await _service.EndAsync(id, User.GetAuthenticatedUserClaims()));

		/// <summary>Cancel a class that has not started.</summary>
		[HttpDelete("{id:guid}")]
		[Authorize(Roles = TeacherRoles)]
		public async Task<IActionResult> Cancel(Guid id)
			=> Map(await _service.CancelAsync(id, User.GetAuthenticatedUserClaims()));

		/// <summary>Move a scheduled class (that has not started) to a new time. The picked students are told again.</summary>
		[HttpPut("{id:guid}/schedule")]
		[Authorize(Roles = TeacherRoles)]
		public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleLiveClassViewModel model)
			=> Map(await _service.RescheduleAsync(id, model, User.GetAuthenticatedUserClaims()));

		/// <summary>Let a student speak (or mute them again). Students listen only until the teacher allows it.</summary>
		[HttpPost("{id:guid}/students/{studentId:guid}/speak")]
		[Authorize(Roles = TeacherRoles)]
		public async Task<IActionResult> Speak(Guid id, Guid studentId, [FromBody] SetSpeakViewModel model)
			=> Map(await _service.SetStudentSpeakAsync(id, studentId, model?.Allow ?? false, User.GetAuthenticatedUserClaims()));
	}
}
