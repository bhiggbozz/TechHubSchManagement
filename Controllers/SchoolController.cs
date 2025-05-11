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
		
	}
}
