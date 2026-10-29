using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Calendar;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers
{
	/// <summary>
	/// School calendar: academic sessions and their terms. Read-only for everyone in the
	/// school; set up by SuperAdministrator or an Administrator with the ManageCalendar
	/// permission (enforced in <see cref="ICalendarService"/>, the role gate here is a first filter).
	/// </summary>
	[ApiController]
	[Route("api/[controller]")]
	[Authorize]
	public class CalendarController : ControllerBase
	{
		private const string AdminRoles = "Administrator,SuperAdministrator";

		/// <summary>Roles that may add entries; what each may add and edit is enforced in <see cref="ICalendarEntryService"/>.</summary>
		private const string EntryWriterRoles = "Administrator,SuperAdministrator,HeadTeacher,ClassTeacher,SubjectTeacher";

		private readonly ICalendarService _calendarService;
		private readonly ICalendarEntryService _entryService;

		public CalendarController(ICalendarService calendarService, ICalendarEntryService entryService)
		{
			_calendarService = calendarService;
			_entryService = entryService;
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

		/// <summary>Preset starting points for the setup form (names only — the admin supplies every date).</summary>
		[HttpGet("templates")]
		public IActionResult GetTemplates() => Map(_calendarService.GetTemplates());

		/// <summary>All of the school's academic sessions (newest first), each with its terms.</summary>
		[HttpGet("sessions")]
		public async Task<IActionResult> GetSessions()
			=> Map(await _calendarService.GetSessionsAsync(User.GetAuthenticatedUserClaims()));

		/// <summary>The session containing today, else the next upcoming, else the latest past one.</summary>
		[HttpGet("sessions/current")]
		public async Task<IActionResult> GetCurrentSession()
			=> Map(await _calendarService.GetSessionAsync(null, User.GetAuthenticatedUserClaims()));

		[HttpGet("sessions/{sessionId:guid}")]
		public async Task<IActionResult> GetSession(Guid sessionId)
			=> Map(await _calendarService.GetSessionAsync(sessionId, User.GetAuthenticatedUserClaims()));

		/// <summary>Create a session together with its terms, atomically.</summary>
		[HttpPost("sessions")]
		[Authorize(Roles = AdminRoles)]
		public async Task<IActionResult> CreateSession([FromBody] CreateAcademicSessionViewModel model)
			=> Map(await _calendarService.CreateSessionAsync(model, User.GetAuthenticatedUserClaims()));

		[HttpPut("sessions/{sessionId:guid}")]
		[Authorize(Roles = AdminRoles)]
		public async Task<IActionResult> UpdateSession(Guid sessionId, [FromBody] UpdateAcademicSessionViewModel model)
			=> Map(await _calendarService.UpdateSessionAsync(sessionId, model, User.GetAuthenticatedUserClaims()));

		[HttpDelete("sessions/{sessionId:guid}")]
		[Authorize(Roles = AdminRoles)]
		public async Task<IActionResult> DeleteSession(Guid sessionId)
			=> Map(await _calendarService.DeleteSessionAsync(sessionId, User.GetAuthenticatedUserClaims()));

		[HttpPost("sessions/{sessionId:guid}/terms")]
		[Authorize(Roles = AdminRoles)]
		public async Task<IActionResult> AddTerm(Guid sessionId, [FromBody] TermInputViewModel model)
			=> Map(await _calendarService.AddTermAsync(sessionId, model, User.GetAuthenticatedUserClaims()));

		[HttpPut("terms/{termId:guid}")]
		[Authorize(Roles = AdminRoles)]
		public async Task<IActionResult> UpdateTerm(Guid termId, [FromBody] TermInputViewModel model)
			=> Map(await _calendarService.UpdateTermAsync(termId, model, User.GetAuthenticatedUserClaims()));

		[HttpDelete("terms/{termId:guid}")]
		[Authorize(Roles = AdminRoles)]
		public async Task<IActionResult> DeleteTerm(Guid termId)
			=> Map(await _calendarService.DeleteTermAsync(termId, User.GetAuthenticatedUserClaims()));

		// ── Calendar entries (assessment weeks, quiz periods, exams, holidays …) ──

		/// <summary>The fixed list of entry types for the entry form.</summary>
		[HttpGet("entry-types")]
		public IActionResult GetEntryTypes() => Map(_entryService.GetEntryTypes());

		/// <summary>Entries the caller can see (school-wide plus their own classes, subjects or children), optionally within a date range.</summary>
		[HttpGet("entries")]
		public async Task<IActionResult> GetEntries([FromQuery] CalendarEntryQueryViewModel query)
			=> Map(await _entryService.ListAsync(query, User.GetAuthenticatedUserClaims()));

		/// <summary>One entry with its full change history.</summary>
		[HttpGet("entries/{entryId:guid}")]
		public async Task<IActionResult> GetEntry(Guid entryId)
			=> Map(await _entryService.GetAsync(entryId, User.GetAuthenticatedUserClaims()));

		[HttpPost("entries")]
		[Authorize(Roles = EntryWriterRoles)]
		public async Task<IActionResult> CreateEntry([FromBody] CreateCalendarEntryViewModel model)
			=> Map(await _entryService.CreateAsync(model, User.GetAuthenticatedUserClaims()));

		[HttpPut("entries/{entryId:guid}")]
		[Authorize(Roles = EntryWriterRoles)]
		public async Task<IActionResult> UpdateEntry(Guid entryId, [FromBody] UpdateCalendarEntryViewModel model)
			=> Map(await _entryService.UpdateAsync(entryId, model, User.GetAuthenticatedUserClaims()));

		[HttpDelete("entries/{entryId:guid}")]
		[Authorize(Roles = EntryWriterRoles)]
		public async Task<IActionResult> DeleteEntry(Guid entryId)
			=> Map(await _entryService.DeleteAsync(entryId, User.GetAuthenticatedUserClaims()));
	}
}
