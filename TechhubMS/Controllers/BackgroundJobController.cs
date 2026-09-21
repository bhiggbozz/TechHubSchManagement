using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers
{
	/// <summary>
	/// Run history for the periodic 24h IHostedService workers (performance
	/// aggregation, admin dashboard aggregation) — not Hangfire, which has its
	/// own dashboard/tables already. Lets an admin actually answer "did today's
	/// aggregation run, when, how long did it take, did it fail" without
	/// grepping raw log files.
	/// </summary>
	[ApiController]
	[Route("api/background-jobs")]
	[Authorize(Roles = "Administrator,SuperAdministrator")]
	public class BackgroundJobController : ControllerBase
	{
		private readonly IBackgroundJobRunTracker _tracker;

		public BackgroundJobController(IBackgroundJobRunTracker tracker)
		{
			_tracker = tracker;
		}

		[HttpGet("runs")]
		public async Task<IActionResult> GetRecentRuns([FromQuery] string? jobName, [FromQuery] int limit = 20)
		{
			var result = await _tracker.GetRecentRunsAsync(jobName, limit);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}
	}
}
