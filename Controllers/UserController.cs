using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.ViewModel;
using TechHub.Service.Extension;
using TechHub.Service.Interface;
using TechhubMS.Middleware.Services;
using TechhubMS.util;

namespace TechhubMS.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class UserController : ControllerBase
	{
		private readonly IUserService _userService;
        private readonly AuthService _authService;
		private readonly ITenantService _tenantService;
       // private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, AuthService authService, ITenantService tenantService)
		{
			_userService = userService;
			_authService = authService;
			_tenantService = tenantService;
		}
		[AllowAnonymous]
		[HttpPost("login")]
		public async Task<ActionResult<BaseResponse>> login(LoginViewModel loginViewModel)
		{
            var host = HttpContext.Request.Host.Host;
			var tenant = _tenantService.GetCurrentTenant();

			if (tenant == null)
			{
				return BadRequest(new BaseResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = "Invalid tenant. Use subdomain (e.g., pearl.myapp.com)"
				});
			}
			// var domain = ExtractSubdomain(host);

			var result = await _userService.LoginUser(loginViewModel, tenant);
			return Ok(result);
		}
		[HttpPost("createUser")]
		[Authorize]
		public async Task<ActionResult<BaseResponse>> Create(UserViewModel userViewModel)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();

			var result = await _userService.CreateUser(userViewModel, schoolIdClaim);
			return Ok(result);
		}
		[HttpPost("updatePassword")]
		[Authorize]
		public async Task<ActionResult<BaseResponse>> UpdatePassword(UpdatePasswordViewModel updatePasswordViewModel)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();

			var result = await _userService.updatePassword(updatePasswordViewModel, schoolIdClaim);
			return Ok(result);
		}

	}
}
