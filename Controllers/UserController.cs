using Microsoft.AspNetCore.Mvc;
using TechHub.Core.ViewModel;
using TechHub.Core;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class UserController : ControllerBase
	{
		private readonly IUserService _userService;

		public UserController(IUserService userService)
		{
			_userService = userService;
		}
		[HttpPost("login")]
		public async Task<ActionResult<BaseResponse>> login(LoginViewModel loginViewModel)
		{
			var result = await _userService.LoginUser(loginViewModel);
			return Ok(result);
		}
		[HttpPost("createUser")]
		public async Task<ActionResult<BaseResponse>> Create(UserViewModel userViewModel)
		{
			var result = await _userService.CreateUser(userViewModel);
			return Ok(result);
		}
		[HttpPost("updatePassword")]
		public async Task<ActionResult<BaseResponse>> UpdatePassword(UpdatePasswordViewModel updatePasswordViewModel)
		{
			var result = await _userService.updatePassword(updatePasswordViewModel);
			return Ok(result);
		}
	}
}
