using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.ViewModel;
using TechHub.QuestionBank.Services.interfaces;

namespace TechHub.QuestionBank.Controllers;

[Authorize]
[ApiController]
[Route("api/questions")]
public class QuestionController : ControllerBase
{
	private readonly IQuestionService _questionService;
	private readonly IQuestionSyncService _questionSyncService;
	private readonly ILogger _logger;

	public QuestionController(IQuestionService questionService,IQuestionSyncService questionSyncService,ILogger logger)
	{
		_questionService = questionService;
		_questionSyncService = questionSyncService;
		_logger = logger;
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// CREATE QUESTION
	// POST api/questions
	// Teacher manually creates a question
	// Returns ServerId and ClientId for local reconciliation
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPost("createquestions")]
	public async Task<IActionResult> CreateQuestion([FromBody] CreateQuestionViewModel model)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionService.CreateQuestion(model, userClaims);

		return result.ResponseCode ==ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// UPDATE QUESTION
	// PUT api/questions
	// Teacher edits an existing question
	// Dirty detection runs inside service
	// Returns conflict detail if detected
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPut]
	public async Task<IActionResult> UpdateQuestion(
		[FromBody] UpdateQuestionViewModel model)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionService.UpdateQuestion(model, userClaims);

		// Conflict is a distinct response
		// Frontend handles it differently
		// from a normal bad request
		if (result.IsConflict)
			return Conflict(result);

		return result.ResponseCode == ResponseCode.successful? Ok(result) : BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// GET QUESTION
	// GET api/questions/{questionId}
	// Returns full QuestionDto
	// IsCorrect hidden for students
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpGet("{questionId:guid}")]
	public async Task<IActionResult> GetQuestion(Guid questionId)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionService.GetQuestion(questionId, userClaims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : NotFound(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// GET SUBJECT QUESTIONS
	// GET api/questions/subjects/{subjectId}
	// Returns paginated QuestionSummaryDto list
	// Excludes PendingReview by default
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpGet("subjects/{subjectId:guid}")]
	public async Task<IActionResult> GetSubjectQuestions(
		Guid subjectId,
		[FromQuery] QuestionFilterViewModel filter)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionService.GetSubjectQuestions(subjectId,filter,userClaims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// DELETE QUESTION
	// DELETE api/questions/{questionId}
	// Soft delete only
	// Published questions blocked — unpublish first
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpDelete("{questionId:guid}")]
	public async Task<IActionResult> DeleteQuestion(Guid questionId)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionService.DeleteQuestion(questionId, userClaims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// PUBLISH QUESTION
	// POST api/questions/{questionId}/publish
	// Moves Draft → Published
	// Readiness check runs inside service
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPost("{questionId:guid}/publish")]
	public async Task<IActionResult> PublishQuestion(Guid questionId)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionService.PublishQuestion(questionId, userClaims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// CONFIRM QUESTION (SCAN REVIEW)
	// POST api/questions/{questionId}/confirm
	// Moves PendingReview → Draft
	// Teacher verified AI extraction is correct
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPost("{questionId:guid}/confirm")]
	public async Task<IActionResult> ConfirmQuestion(Guid questionId)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionService.ConfirmQuestion(questionId, userClaims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// REJECT QUESTION (SCAN REVIEW)
	// POST api/questions/{questionId}/reject
	// Soft deletes a PendingReview question
	// Teacher decided AI extraction was wrong
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPost("{questionId:guid}/reject")]
	public async Task<IActionResult> RejectQuestion(
		Guid questionId)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionService.RejectQuestion(questionId, userClaims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// GET PENDING REVIEW QUESTIONS
	// GET api/questions/scan-sessions/{sessionId}/review
	// Returns all PendingReview questions for a session
	// Includes original file url for side by side view
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpGet("scan-sessions/{sessionId:guid}/review")]
	public async Task<IActionResult> GetPendingReviewQuestions(Guid sessionId)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionService.GetPendingReviewQuestions(sessionId,userClaims);

		return result.ResponseCode == ResponseCode.successful? Ok(result): BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// SYNC QUESTIONS
	// POST api/questions/sync
	// Batch sync of offline created or edited questions
	// Partial success supported
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPost("sync")]
	public async Task<IActionResult> SyncQuestions(
		[FromBody] SyncQuestionsViewModel model)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionSyncService
			.SyncQuestions(model, userClaims);

		return result.ResponseCode ==
			ResponseCode.successful
				? Ok(result)
				: BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// CHECK FOR CONFLICTS
	// POST api/questions/sync/conflicts/check
	// Frontend calls before sync attempt
	// Returns safe to sync vs conflicts list
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPost("sync/conflicts/check")]
	public async Task<IActionResult> CheckForConflicts(
		[FromBody] ConflictCheckViewModel model)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionSyncService
			.CheckForConflicts(model, userClaims);

		return result.ResponseCode ==
			ResponseCode.successful
				? Ok(result)
				: BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// RESOLVE CONFLICTS
	// POST api/questions/sync/conflicts/resolve
	// Teacher submits resolution decisions
	// KeepLocal / KeepServer / KeepMerged / DiscardLocal
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPost("sync/conflicts/resolve")]
	public async Task<IActionResult> ResolveConflict([FromBody] ResolveConflictViewModel model)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _questionSyncService
			.ResolveConflict(model, userClaims);

		return result.ResponseCode ==
			ResponseCode.successful
				? Ok(result)
				: BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// PRIVATE HELPERS
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	private AuthenticatedUserClaims GetUserClaims()
	{
		try
		{
			return new AuthenticatedUserClaims
			{
				UserId = User.FindFirst("UserId")?.Value,
				SchoolId = User.FindFirst("SchoolId")?.Value,
				Role = User.FindFirst("Role")?.Value,
				Email = User.FindFirst("Email")?.Value
			};
		}
		catch
		{
			return null;
		}
	}
}
