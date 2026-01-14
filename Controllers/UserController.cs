using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;
using TechhubMS.Middleware.Services;

namespace TechhubMS.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class UserController : ControllerBase
	{
		private readonly IUserService _userService;
        private readonly AuthService _authService;
       // private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, AuthService authService)
		{
			_userService = userService;
			_authService = authService;
		}
		[HttpPost("login")]
		public async Task<ActionResult<BaseResponse>> login(LoginViewModel loginViewModel)
		{
            var host = HttpContext.Request.Host.Host;
           // var domain = ExtractSubdomain(host);

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
