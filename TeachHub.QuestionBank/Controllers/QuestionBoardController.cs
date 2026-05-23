using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.ViewModel;
using TechHub.QuestionBank.Services;
using TechHub.QuestionBank.Services.interfaces;

namespace TechHub.QuestionBank.Controllers;

[Authorize]
[ApiController]
[Route("api/questions")]
public class QuestionBoardController : ControllerBase
{
	private readonly IQuestionBoardService _boardService;
	private readonly ILogger _logger;

	public QuestionBoardController(
		IQuestionBoardService boardService,
		ILogger logger)
	{
		_boardService = boardService;
		_logger = logger;
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// ATTACH BOARD SESSION
	// POST api/questions/board/attach
	// Frontend calls after:
	// 1. Teacher finishes drawing
	// 2. PNG snapshot uploaded to CDN
	// Backend links session and snapshot to question
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPost("board/attach")]
	public async Task<IActionResult> AttachBoardSession(
		[FromBody] AttachBoardSessionViewModel model)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _boardService
			.AttachBoardSession(model, userClaims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// DETACH BOARD SESSION
	// DELETE api/questions/{questionId}/board/detach
	// Teacher removes board content from question
	// CDN snapshot deleted in background
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpDelete("{questionId:guid}/board/detach")]
	public async Task<IActionResult> DetachBoardSession(Guid questionId)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var model = new DetachBoardSessionViewModel
		{
			QuestionId = questionId
		};

		var result = await _boardService.DetachBoardSession(model, userClaims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// GET BOARD SESSION
	// GET api/questions/{questionId}/board
	// Returns BoardSessionId reference
	// Frontend uses ID to load strokes
	// from MongoDB for editing or display
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpGet("{questionId:guid}/board")]
	public async Task<IActionResult> GetBoardSession(Guid questionId)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null)
			return Unauthorized();

		var result = await _boardService.GetBoardSession(questionId, userClaims);

		return result.ResponseCode ==ResponseCode.successful ? Ok(result) : NotFound(result);
	}

	[HttpGet("jobs/{jobId}/questions")]
	[Authorize]
	public async Task<IActionResult> GetQuestionsByJobId(Guid jobId)
	{
		var claims = GetUserClaims();
		if (claims == null) return Unauthorized();

		var result = await _boardService.GetQuestionsByJobId(jobId, claims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}


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

