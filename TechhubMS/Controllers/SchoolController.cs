using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.classroom;
using TechHub.Core.ViewModel.Platform;
using TechHub.Core.ViewModel.school;
using TechHub.QuestionBank.Core.Helpers;
using TechHub.Service.Extension;
using TechHub.Service.Interface;
using TechHub.Service.Service;

namespace TechhubMS.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class SchoolController :ControllerBase
	{
		private readonly ISchoolService _schoolService;
		public SchoolController(ISchoolService schoolService) 
		{ 
			_schoolService = schoolService;
		}
		[HttpPost("createschool")]
		public async Task<ActionResult<BaseResponse>> CreateSchool(SchoolViewModel schoolViewModel)
		{
			var result = await _schoolService.CreateSchool(schoolViewModel);
			return Ok(result);
		}
		[HttpPost("getState")]

		public async Task<ActionResult<BaseResponse>> GetStates(int countryId)
		{
			var result = await _schoolService.GetAllStates(countryId);
			return Ok(result);
		}
		[HttpPost("updateSchoolCode")]
		public async Task<ActionResult<BaseResponse>> UpdateSchoolCode(SchoolCodeViewModel schCodeViewModel)
		{
			var result = await _schoolService.UpdateSchoolCode(schCodeViewModel);
			return Ok(result);
		}

		[HttpPost("createschoolclassroom")]
		public async Task<ActionResult<BaseResponse>> CreateStudentClass(CreateStudentClassViewModel createStudentClassViewModel)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.CreateStudentClassV2(createStudentClassViewModel, schoolIdClaim);
			return Ok(result);
		}

		[HttpPost("registersubject")]
		public async Task<ActionResult<BaseResponse>> CreateSchoolSubjects(CreateSubjectViewModel createSubjectViewModel)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.CreateSchoolSubjects(createSubjectViewModel, schoolIdClaim);
			return Ok(result);
		}

		[HttpPost("getAllSchoolSubjects")]
		public async Task<ActionResult<BaseResponse>> GetAllSchoolSubjects(Guid schoolId)
		{
			var result = await _schoolService.GetAllSubjects(schoolId);
			return Ok(result);
		}

		[HttpPost("[action]")]
		public async Task<ActionResult<BaseResponse>> RegisterClassroomSubject(CreateClassroomViewModel createClassroomSubject)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();

			var result = await _schoolService.RegisterClassroomSubjects(createClassroomSubject, schoolIdClaim);
			return Ok(result);
		}

		[HttpPost("updatesubject")]
		public async Task<ActionResult<BaseResponse>> UpdateSchoolSubjects(updateSchoolSubject updateSchoolSubject)
		{
			var result = await _schoolService.UpdateSchoolId(updateSchoolSubject);
			return Ok(result);
		}
		[HttpPost("updateclassroom")]
		public async Task<ActionResult<BaseResponse>> UpdateClassroom(UpdateClassroomView updateClassroom)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();

			var result = await _schoolService.UpdateSchoolClassroom(updateClassroom, schoolIdClaim);
			return Ok(result);
		}
		[HttpGet("getSubjectById")]
		public async Task<ActionResult<BaseResponse>> GetSubjectById(Guid subjectId)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();

			var result = await _schoolService.GetSubjectById(subjectId, schoolIdClaim);
			return Ok(result);
		}

		[HttpGet("getClassroomById")]
		public async Task<ActionResult<BaseResponse>> GetClassroomById(Guid classroomId)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetClassroomById(classroomId, userClaims);
			return Ok(result);
		}
		[HttpGet("getSubjectsByClassroom")]
		public async Task<ActionResult<BaseResponse>> GetSubjectsByClassroom(Guid classroomId)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetSubjectsByClassroom(classroomId, userClaims);
			return Ok(result);
		}
		[HttpPost("AssignTeachers")]
		//[Authorize(Roles = "Administrator,SuperAdministrator")]
		public async Task<ActionResult<BaseResponse>> AssignTeachers(AssignTeacherViewModel model)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.AssignTeachersToClassroom(model, userClaims);
			return Ok(result);
		}
		/// <summary>
		/// Get all classrooms (with pagination)
		/// </summary>
		[HttpGet("GetAllClassrooms")]
		public async Task<ActionResult<ClassroomDetails>> GetAllClassrooms([FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 50)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetAllClassrooms(userClaims, pageNumber, pageSize);
			return Ok(result);
		}

		/// <summary>
		/// Get all subjects (with optional filters)
		/// </summary>
		[HttpGet("GetAllSubjects")]
		public async Task<ActionResult<SubjectsListResponse>> GetAllSubjects([FromQuery] int? classCategory = null,[FromQuery] int? subjectCategory = null,[FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 50)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetAllSubjects(userClaims,classCategory,subjectCategory,pageNumber,pageSize);
			return Ok(result);
		}

		/// <summary>
		/// Get subjects by class category
		/// </summary>
		[HttpGet("GetSubjectsByClassCategory")]
		public async Task<ActionResult<SubjectsListResponse>> GetSubjectsByClassCategory([FromQuery] int classCategory,[FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 50)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetSubjectsByClassCategory(classCategory,userClaims,pageNumber,pageSize);
			return Ok(result);
		}

		/// <summary>
		/// Get subjects by subject category
		/// </summary>
		[HttpGet("GetSubjectsBySubjectCategory")]
		public async Task<ActionResult<SubjectsListResponse>> GetSubjectsBySubjectCategory([FromQuery] int subjectCategory,[FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 50)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetSubjectsBySubjectCategory(subjectCategory,userClaims,pageNumber,pageSize);
			return Ok(result);
		}

		/// <summary>
		/// Update classroom teacher assignments (add/remove/reactivate)
		/// </summary>
		[HttpPost("UpdateClassroomTeachers")]
		//[Authorize(Roles = "Administrator,SuperAdministrator")]
		public async Task<ActionResult<BaseResponse>> UpdateClassroomTeachers(UpdateClassroomTeachersViewModel model)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.UpdateClassroomTeachers(model, userClaims);
			return Ok(result);
		}

		[HttpPut("logo")]
		[Authorize]
		[Consumes("multipart/form-data")]
		public async Task<IActionResult> UpdateSchoolLogo([FromForm] IFormFile logo)
		{
			var userClaims = User.GetAuthenticatedUserClaims();

			var result = await _schoolService.UpdateSchoolLogoAsync(logo, userClaims);

			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}


		[HttpPost("topics")]
		public async Task<IActionResult> CreateTopic([FromBody] CreateTopicViewModel model)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.CreateTopic(model, userClaims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		/// <summary>
		/// Get all topics for a subject
		/// GET api/subtopic/topics/{subjectId}
		/// </summary>
		[HttpGet("topics/{subjectId:guid}")]
		public async Task<IActionResult> GetTopics(Guid subjectId)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetTopics(subjectId, userClaims);
			return Ok(result);
		}

		// ── SUBTOPIC ─────────────────────────────────────────────

		/// <summary>
		/// Create a new subtopic under a topic
		/// POST api/subtopic/subtopics
		/// </summary>
		[HttpPost("subtopics")]
		public async Task<IActionResult> CreateSubTopic([FromBody] CreateSubTopicViewModel model)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.CreateSubTopic(model, userClaims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		/// <summary>
		/// Get all subtopics for a topic
		/// GET api/subtopic/subtopics/{topicId}
		/// </summary>
		[HttpGet("subtopics/{topicId:guid}")]
		public async Task<IActionResult> GetSubTopics(Guid topicId)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetSubTopics(topicId, userClaims);
			return Ok(result);
		}

		[HttpGet("classroom/{classroomId:guid}/curriculum")]
		[Authorize]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		public async Task<IActionResult> GetClassroomCurriculum(Guid classroomId)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetClassroomCurriculum(classroomId, userClaims);
			return Ok(result);
		}

		[HttpGet("subjects/{subjectId:guid}/curriculum")]
		[Authorize]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		public async Task<IActionResult> GetSubjectCurriculum(Guid subjectId, [FromQuery] Guid classroomId)
		{
			if (classroomId == Guid.Empty)
				return BadRequest(new BaseResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = "classroomId query parameter is required",
					Status = "failed"
				});

			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetSubjectCurriculum(subjectId, classroomId, userClaims);
			return Ok(result);
		}

		[HttpGet("subject/{subjectId}/classroom/{classroomId}")]
		[Authorize]
		[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetTopicsWithSubTopics(Guid subjectId, Guid classroomId)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetTopicsWithSubTopics(subjectId, classroomId, userClaims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				ResponseCode.Unauthorized => Unauthorized(result),
				_ => BadRequest(result)
			};
		}

		[HttpGet("subject/{subjectId}/classroom/{classroomId}/stats")]
		[Authorize]
		[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetSubjectStats(Guid subjectId, Guid classroomId)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.GetSubjectStatsAsync(subjectId, classroomId, userClaims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				ResponseCode.Unauthorized => Unauthorized(result),
				_ => BadRequest(result)
			};
		}

		// POST api/topic/subtopics/add
		[HttpPost("subtopics/add")]
		[Authorize(Roles = "SubjectTeacher,HeadTeacher,ClassTeacher,Administrator,SuperAdministrator")]
		[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		[ProducesResponseType(StatusCodes.Status409Conflict)]
		public async Task<IActionResult> AddSubTopics([FromBody] AddSubTopicsViewModel model)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.AddSubTopicsToTopic(model, userClaims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				ResponseCode.Conflict => Conflict(result),
				ResponseCode.Forbidden => StatusCode(403, result),
				_ => BadRequest(result)
			};
		}

		[HttpPost("createtopics")]
		[Authorize]
		public async Task<IActionResult> CreateTopicsWithSubTopics([FromBody] CreateTopicsWithSubTopicsViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.CreateTopicsWithSubTopics(model, claims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				ResponseCode.Conflict => Conflict(result),
				ResponseCode.Forbidden => StatusCode(403, result),
				_ => BadRequest(result)
			};
		}

		/// <summary>
		/// Get all students enrolled in a specific classroom
		/// </summary>
		[HttpGet("students/classroom/{classroomId}")]
		[Authorize]
		public async Task<IActionResult> GetStudentsByClassroom(Guid classroomId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var response = await _schoolService.GetStudentsByClassroom(classroomId, claims);

			return response.ResponseCode switch
			{
				ResponseCode.successful => Ok(response),
				ResponseCode.NotFound => NotFound(response),
				ResponseCode.Unauthorized => Unauthorized(response),
				_ => BadRequest(response)
			};
		}

		/// <summary>
		/// Get all students enrolled in a specific subject
		/// </summary>
		[HttpGet("students/subject/{subjectId}")]
		[Authorize]
		public async Task<IActionResult> GetStudentsBySubject(Guid subjectId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var response = await _schoolService.GetStudentsBySubject(subjectId, claims);

			return response.ResponseCode switch
			{
				ResponseCode.successful => Ok(response),
				ResponseCode.NotFound => NotFound(response),
				ResponseCode.Unauthorized => Unauthorized(response),
				_ => BadRequest(response)
			};
		}

		[HttpPut("teacher/{teacherId}/classrooms")]
		[Authorize]
		public async Task<IActionResult> UpdateTeacherClassroom(Guid teacherId,[FromBody] UpdateTeacherClassroomViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var response = await _schoolService.UpdateTeacherClassroom(teacherId, model, claims);

			return response.ResponseCode switch
			{
				ResponseCode.successful => Ok(response),
				ResponseCode.NotFound => NotFound(response),
				ResponseCode.Unauthorized => Unauthorized(response),
				_ => BadRequest(response)
			};
		}

		[HttpPost("provision")]
		[Authorize(Roles = "PlatformAdmin,PlatformSuperAdmin")]
		public async Task<IActionResult> ProvisionSchool([FromBody] ProvisionSchoolViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.ProvisionSchool(model, claims);
			return result.ResponseCode switch
			{
				"99000" => Ok(result),
				"99134" => NotFound(result),
				"AX1003" => StatusCode(StatusCodes.Status403Forbidden, result),
				"99107" => Unauthorized(result),
				"99161" => StatusCode(StatusCodes.Status409Conflict, result),
				_ => BadRequest(result)
			};
		}

		/// <summary>
		/// Submit a school registration request (anonymous)
		/// POST /api/School/register
		/// </summary>
		[HttpPost("register")]
		[AllowAnonymous]
		public async Task<IActionResult> RegisterSchool([FromBody] SchoolRegistrationRequestViewModel model)
		{
			var result = await _schoolService.SubmitRegistrationRequest(model);
			return result.ResponseCode switch
			{
				"99000" => Ok(result),
				"99161" => StatusCode(StatusCodes.Status409Conflict, result),
				_ => BadRequest(result)
			};
		}

		/// <summary>
		/// List registration requests (Platform Admin)
		/// GET /api/School/registration-requests?status=Pending
		/// </summary>
		[HttpGet("registration-requests")]
		[AllowAnonymous]
		public async Task<IActionResult> GetRegistrationRequests([FromQuery] string? status = null)
		{
			var result = await _schoolService.GetRegistrationRequests(status, null);
			return result.ResponseCode switch
			{
				"99000" => Ok(result),
				_ => BadRequest(result)
			};
		}

		/// <summary>
		/// Approve a school registration request (Platform Admin)
		/// POST /api/School/approve/{requestId}
		/// </summary>
		[HttpPost("approve/{requestId:guid}")]
		[Authorize(Roles = "PlatformAdmin,PlatformSuperAdmin")]
		public async Task<IActionResult> ApproveRegistration(Guid requestId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.ApproveRegistrationRequest(requestId, claims);
			return result.ResponseCode switch
			{
				"99000" => Ok(result),
				"99134" => NotFound(result),
				"99161" => StatusCode(StatusCodes.Status409Conflict, result),
				_ => BadRequest(result)
			};
		}

		/// <summary>
		/// Reject a school registration request (Platform Admin)
		/// POST /api/School/reject/{requestId}
		/// </summary>
		[HttpPost("reject/{requestId:guid}")]
		[Authorize(Roles = "PlatformAdmin,PlatformSuperAdmin")]
		public async Task<IActionResult> RejectRegistration(Guid requestId, [FromBody] string reason)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.RejectRegistrationRequest(requestId, reason, claims);
			return result.ResponseCode switch
			{
				"99000" => Ok(result),
				"99134" => NotFound(result),
				_ => BadRequest(result)
			};
		}

		/// <summary>
		/// Edit school info (Platform Admin)
		/// POST /api/School/edit/{schoolId}
		/// </summary>
		[HttpPut("edit/{schoolId:guid}")]
		[Authorize(Roles = "PlatformAdmin,PlatformSuperAdmin")]
		public async Task<IActionResult> EditSchool(Guid schoolId, [FromBody] SchoolEditViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _schoolService.EditSchoolInfoAsync(schoolId, model, claims);
			return result.ResponseCode switch
			{
				"99000" => Ok(result),
				"99134" => NotFound(result),
				_ => BadRequest(result)
			};
		}

		[HttpGet("schools-status")]
		[AllowAnonymous]
		public async Task<IActionResult> GetAllSchoolsWithStatus()
		{
			var result = await _schoolService.GetAllSchoolsWithStatus();
			return Ok(result);
		}

		[HttpGet("pending")]
		[AllowAnonymous]
		public async Task<IActionResult> GetPendingSchoolIds()
		{
			var result = await _schoolService.GetPendingSchoolIds();
			return Ok(result);
		}

		[HttpGet("{schoolId:guid}/approval-status")]
		[AllowAnonymous]
		public async Task<IActionResult> GetSchoolApprovalStatus(Guid schoolId)
		{
			var result = await _schoolService.GetSchoolApprovalStatus(schoolId);
			return result.ResponseCode switch
			{
				"99000" => Ok(result),
				"99134" => NotFound(result),
				_ => BadRequest(result)
			};
		}
	}
}
	



