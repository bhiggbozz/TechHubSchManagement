using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.classroom;
using TechHub.Core.ViewModel.school;
using TechHub.QuestionBank.Core.Helpers;
using TechHub.Service.Extension;
using TechHub.Service.Interface;
using TechhubMS.util;

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
			var schoolIdClaim = User.GetAuthenticatedUserClaims();
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
		public async Task<ActionResult<BaseResponse>> RegisterClassroomSubect(CreateClassroomViewModel createClassroomSubject)
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

	}
}
