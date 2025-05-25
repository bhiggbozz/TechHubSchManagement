using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.ViewModel;
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
	}
}
