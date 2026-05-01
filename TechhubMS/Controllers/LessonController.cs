using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

[ApiController]
[Route("api/lessons")]
[Authorize]
public class LessonController : ControllerBase
{
	private readonly ILessonService _lessonService;
	private readonly ICloudinaryService _signatureService;

	[HttpGet("upload-signature")]
	public IActionResult GetUploadSignature()
	{
		var claims = GetClaims();
		if (!Guid.TryParse(claims.SchoolId, out var schoolId))
			return Unauthorized();
		if (!Guid.TryParse(claims.UserId, out var teacherId))
			return Unauthorized();

		var signature = _signatureService.GenerateUploadSignature(schoolId, teacherId);
		return Ok(new BaseResponse
		{
			ResponseCode = ResponseCode.successful,
			Status = "successful",
			Data = signature
		});
	}

	[HttpPost("submit")]
	public async Task<IActionResult> SubmitLesson([FromBody] SubmitLessonViewModel model)
	{
		var claims = GetClaims();
		var result = await _lessonService.SubmitLesson(model, claims);
		return result.ResponseCode == ResponseCode.successful
			? Ok(result) : BadRequest(result);
	}

	[HttpPost("draft")]
	//[Authorize(Roles = "SubjectTeacher,HeadTeacher")]
	public async Task<IActionResult> SaveDraft([FromBody] SubmitLessonViewModel model)
	{
		model.IsDraft = true;   
		model.BypassApproval = false;
		var claims = GetClaims();
		var result = await _lessonService.SubmitLesson(model, claims);
		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	[HttpGet("classroom/{classroomId}")]
	public async Task<IActionResult> GetLessonsByClassroom(Guid classroomId)
	{
		var claims = GetClaims();
		var result = await _lessonService.GetLessonsByClassroom(classroomId, claims);
		return Ok(result);
	}

	[HttpGet("{lessonId}")]
	public async Task<IActionResult> GetLesson(Guid lessonId)
	{
		var claims = GetClaims();
		var result = await _lessonService.GetLessonById(lessonId, claims);
		return result.ResponseCode == ResponseCode.successful
			? Ok(result) : NotFound(result);
	}

	[HttpPost("{lessonId}/respond")]
	public async Task<IActionResult> RespondToLesson(
		Guid lessonId, [FromBody] LessonResponseViewModel model)
	{
		var claims = GetClaims();
		var result = await _lessonService.RespondToLesson(
			lessonId, model.Approved, model.RejectionReason, claims);
		return result.ResponseCode == ResponseCode.successful
			? Ok(result) : BadRequest(result);
	}

	[HttpGet("pending-approvals")]
	public async Task<IActionResult> GetPendingApprovals()
	{
		var claims = GetClaims();
		var result = await _lessonService.GetPendingApprovals(claims);
		return Ok(result);
	}

	[HttpGet("my-lessons")]
	[Authorize(Roles = "SubjectTeacher,HeadTeacher")]
	[ProducesResponseType(typeof(BaseResponse), 200)]
	public async Task<IActionResult> GetMyLessons(
	[FromQuery] string? status = null,
	[FromQuery] int pageNumber = 1,
	[FromQuery] int pageSize = 50)
	{
		var claims = GetClaims();
		var result = await _lessonService.GetLessonsByTeacher(
			claims, status, pageNumber, pageSize);
		return Ok(result);
	}
	private AuthenticatedUserClaims GetClaims() => new AuthenticatedUserClaims
	{
		UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
		SchoolId = User.FindFirst("SchoolId")?.Value,
		Role = User.FindFirst(ClaimTypes.Role)?.Value
	};
}

public class LessonResponseViewModel
{
	public bool Approved { get; set; }
	public string RejectionReason { get; set; }
}

