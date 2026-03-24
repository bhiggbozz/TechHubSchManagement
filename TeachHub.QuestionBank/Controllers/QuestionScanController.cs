using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.ViewModel;
using TechHub.QuestionBank.Services.interfaces;
using TechhubMS.util;

namespace TechHub.QuestionBank.Controllers;

[Authorize]
[ApiController]
[Route("api/questions/scan")]
public class QuestionScanController : ControllerBase
{
	private readonly IQuestionScanService _scanService;
	private readonly ILogger _logger;

	public QuestionScanController(IQuestionScanService scanService, ILogger logger)
	{
		_scanService = scanService;
		_logger = logger;
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// GET SCAN QUOTA
	// GET api/questions/scan/quota
	// Frontend calls before showing scan UI
	// Prevents teacher attempting scan
	// only to be told quota exceeded
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpGet("quota")]
	public async Task<IActionResult> GetScanQuota()
	{
		var userClaims = GetUserClaims();
		if (userClaims == null) return Unauthorized();

		var result = await _scanService.GetScanQuota(userClaims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// REQUEST SCAN TOKEN
	// POST api/questions/scan/token/request
	// Teacher requests permission for one scan
	// Must be called before ProcessScan
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPost("token/request")]
	public async Task<IActionResult> RequestScanToken(
		[FromBody] RequestScanTokenViewModel model)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null) return Unauthorized();

		var result = await _scanService.RequestScanToken(model, userClaims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// PROCESS SCAN (STREAMING)
	// POST api/questions/scan/process/{tokenId}
	// Web Worker calls this directly
	// Response is SSE stream — not JSON
	// Token validated and consumed here
	// Image piped to Claude
	// Claude response streamed back
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPost("process/{tokenId:guid}")]
	public async Task ProcessScan(Guid tokenId,[FromForm] Microsoft.AspNetCore.Http.IFormFile image)
	{
		// Note: No IActionResult return type
		// Response written directly to HttpContext
		// This is required for SSE streaming
		await _scanService.ProcessScan(tokenId,image,HttpContext);
	}

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// SAVE SCAN RESULTS
	// POST api/questions/scan/save
	// Called after teacher completes review
	// Contains only confirmed questions
	// Creates ScanSession and saves as PendingReview
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	[HttpPost("save")]
	public async Task<IActionResult> SaveScanResults([FromBody] SaveScanResultsViewModel model)
	{
		var userClaims = GetUserClaims();
		if (userClaims == null) return Unauthorized();

		var result = await _scanService.SaveScanResults(model, userClaims);

		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
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

