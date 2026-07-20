using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.school;
using TechHub.Service.Extension;
using TechHub.Service.Interface;

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
		public async Task<ActionResult<BaseResponse>> RegisterClassroomSubect(CreateClassroomViewModel createClassroomSubject)
		{
			var result = await _schoolService.RegisterClassroomSubjects(createClassroomSubject);
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

			var result = await _schoolService.UpdateSchoolClassroom(updateClassroom);
			return Ok(result);
		}
	}
}
