using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TechHub.Core;
using TechHub.Core.Enum;
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
	public LessonController(ILessonService lessonService, ICloudinaryService signatureService)
	{
		_lessonService = lessonService;
		_signatureService = signatureService;
	}


	[HttpGet("upload-signature")]
	public IActionResult GetUploadSignature([FromQuery] MediaType mediaType)
	{
		var claims = GetClaims();
		if (!Guid.TryParse(claims.SchoolId, out var schoolId))
			return Unauthorized();
		if (!Guid.TryParse(claims.UserId, out var teacherId))
			return Unauthorized();

		var signature = _signatureService.GenerateUploadSignature(schoolId, teacherId, mediaType);

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
	[Authorize(Roles = "SubjectTeacher,HeadTeacher,ClassTeacher")]
	[ProducesResponseType(typeof(BaseResponse), 200)]
	public async Task<IActionResult> GetMyLessons([FromQuery] string? status = null,[FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 50)
	{
		var claims = GetClaims();
		var result = await _lessonService.GetLessonsByTeacher(claims, status, pageNumber, pageSize);
		return Ok(result);
	}

	// GET api/lesson/{lessonId}/class
	[HttpGet("{lessonId}/class")]
	[Authorize(Roles = "SubjectTeacher,HeadTeacher")]
	[ProducesResponseType(typeof(BaseResponse), 200)]
	public async Task<IActionResult> GetLessonForClass(Guid lessonId)
	{
		var claims = GetClaims();
		var result = await _lessonService.GetLessonForClass(lessonId, claims);
		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// GET api/lesson/student/classroom/{classroomId}
	[HttpGet("student/classroom/{classroomId}")]
	[Authorize(Roles = "Student")]
	public async Task<IActionResult> GetLessonsForStudent(Guid classroomId)
	{
		var claims = GetClaims();
		var result = await _lessonService.GetLessonsForStudent(classroomId, claims);
		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	// GET api/lesson/admin/classroom/{classroomId}
	[HttpGet("admin/classroom/{classroomId}")]
	[Authorize(Roles = "Administrator,SuperAdministrator")]
	public async Task<IActionResult> GetLessonsByClassroomForAdmin(Guid classroomId)
	{
		var claims = GetClaims();
		var result = await _lessonService.GetLessonsByClassroomForAdmin(classroomId, claims);
		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
	}

	[HttpGet("subject/{subjectId}")]
	[Authorize]
	public async Task<IActionResult> GetLessonsBySubject(Guid subjectId)
	{
		var claims = GetClaims();
		var result = await _lessonService.GetLessonsBySubject(subjectId, claims);
		return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);

		
	}

	//[HttpGet("classroom/{classroomId}/subject/{subjectId}/summary")]
	//[Authorize]
	//public async Task<IActionResult> GetSubjectQuestionSummary(Guid classroomId, Guid subjectId)
	//{
	//	var claims = GetClaims();
	//	if (claims == null) return Unauthorized();

	//	var result = await _questionService.GetSubjectQuestionSummary(classroomId, subjectId, claims);

	//	return result.ResponseCode == ResponseCode.successful
	//		? Ok(result)
	//		: BadRequest(result);
	//}

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

