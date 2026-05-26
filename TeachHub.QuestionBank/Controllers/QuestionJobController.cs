using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.QuestionBank.Core.ViewModel;
using TechHub.QuestionBank.Services;
using TechHub.QuestionBank.Services.interfaces;
using TechHub.Service.Extension;

namespace TechHub.QuestionBank.Controllers;
	

// ═══════════════════════════════════════════════════════════
// QUESTION JOB CONTROLLER
// Async AI upload pipeline
// ═══════════════════════════════════════════════════════════

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class QuestionJobController : ControllerBase
{
	private readonly IQuestionJobService _jobService;

	public QuestionJobController(IQuestionJobService jobService)
	{
		_jobService = jobService;
	}

	/// <summary>
	/// Step 1 — Teacher uploads question image
	/// Returns JobId immediately (~200ms)
	/// Teacher stores JobId and polls GetStatus
	///
	/// POST api/questionjob/submit
	/// Body: multipart/form-data
	///   image       : file
	///   SubTopicId  : guid
	///   QuestionType: string  (Objective | Theory | TrueFalse)
	///   HasImages   : bool
	///   MarksAllocation: int
	/// </summary>
	[HttpPost("submit")]
	[Consumes("multipart/form-data")]
	public async Task<IActionResult> SubmitJob([FromForm] IFormFile image,[FromForm] SubmitQuestionJobViewModel model)
	{
		var userClaims = User.GetAuthenticatedUserClaims();
		var result = await _jobService.SubmitJob(image, model, userClaims);
		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	/// <summary>
	/// Step 2 — Teacher polls job status
	/// Returns: Pending | Processing | Completed | Failed
	/// When Completed → QuestionId is populated
	///
	/// GET api/questionjob/{jobId}/status
	/// </summary>
	[HttpGet("{jobId:guid}/status")]
	public async Task<IActionResult> GetJobStatus(Guid jobId)
	{
		var userClaims = User.GetAuthenticatedUserClaims();
		var result = await _jobService.GetJobStatus(jobId, userClaims);
		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	/// <summary>
	/// Get all jobs for this teacher
	/// Shows upload history with statuses
	///
	/// GET api/questionjob/my-jobs
	/// </summary>
	[HttpGet("my-jobs")]
	public async Task<IActionResult> GetMyJobs()
	{
		var userClaims = User.GetAuthenticatedUserClaims();
		var result = await _jobService.GetMyJobs(userClaims);
		return Ok(result);
	}

	/// <summary>
	/// Retry a failed job
	/// Resets Status to Pending
	/// Background worker picks it up again
	///
	/// POST api/questionjob/{jobId}/retry
	/// </summary>
	[HttpPost("{jobId:guid}/retry")]
	public async Task<IActionResult> RetryJob(Guid jobId)
	{
		var userClaims = User.GetAuthenticatedUserClaims();
		var result = await _jobService.RetryJob(jobId, userClaims);
		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	/// <summary>
	/// Step 3 — Teacher fetches processed question for preview
	/// Only available when Status = Completed
	/// Returns HTML + ContentParts + Options
	///
	/// GET api/questionjob/{jobId}/preview
	/// </summary>
	[HttpGet("{jobId:guid}/preview")]
	public async Task<IActionResult> GetQuestionPreview(Guid jobId)
	{
		var userClaims = User.GetAuthenticatedUserClaims();
		var result = await _jobService.GetQuestionPreview(jobId, userClaims);
		return result.ResponseCode == ResponseCode.successful? Ok(result) : BadRequest(result);
	}

	[HttpGet("jobs/status")]
	[Authorize]
	public async Task<IActionResult> GetJobStatuses([FromQuery] Guid classroomId,[FromQuery] Guid subjectId,[FromQuery] Guid? topicId,[FromQuery] Guid? subTopicId)
	{
		var claims = User.GetAuthenticatedUserClaims();
		if (claims == null) return Unauthorized();

		var result = await _jobService.GetJobStatuses(classroomId, subjectId, topicId, subTopicId, claims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}


	[HttpPost("jobs/{jobId}/confirm")]
	[Authorize]
	public async Task<IActionResult> ConfirmJobQuestions(Guid jobId)
	{
		var claims = User.GetAuthenticatedUserClaims();
		if (claims == null) return Unauthorized();

		var result = await _jobService.ConfirmJobQuestions(jobId, claims);

		return result.ResponseCode == ResponseCode.successful
			? Ok(result)
			: BadRequest(result);
	}
}

