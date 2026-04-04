using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.Users;
using TechHub.Service.Extension;
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
		public async Task<ActionResult<BaseResponse>> Create(UserViewModelV2 userViewModel)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();

			var result = await _userService.CreateUser(userViewModel, schoolIdClaim);
			return Ok(result);
		}
		[HttpPost("EditUser")]
		[Authorize]
		public async Task<ActionResult<BaseResponse>> EditUser(TechHub.Core.ViewModel.UpdateUserView userUpdateViewModel)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();

			var result = await _userService.EditUser(userUpdateViewModel, schoolIdClaim);
			return Ok(result);
		}

		/// <summary>
		/// Get all students
		/// </summary>
		[HttpGet("GetStudents")]
		public async Task<ActionResult<BaseResponse>> GetStudents([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();
			var result = await _userService.GetStudents(schoolIdClaim, pageNumber, pageSize);
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

		// Controllers/UsersController.cs

		/// <summary>
		/// Get user by ID
		/// </summary>
		[HttpGet("GetUserById")]
		public async Task<ActionResult<BaseResponse>> GetUserById([FromQuery] Guid userId)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();
			var result = await _userService.GetUserById(userId, schoolIdClaim);
			return Ok(result);
		}


		[HttpPost("AssignPermissions")]
		//[Authorize(Roles = "SuperAdministrator")]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		[ProducesResponseType(typeof(BaseResponse), 400)]
		[ProducesResponseType(typeof(BaseResponse), 403)]
		[ProducesResponseType(typeof(BaseResponse), 404)]
		public async Task<ActionResult<BaseResponse>> AssignPermissions(
			[FromBody] AssignAdminPermissionsViewModel model)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _userService.AssignAdminPermissions(model, userClaims);
			return Ok(result);
		}

		/// <summary>
		/// Get permissions for a specific administrator
		/// </summary>
		/// <param name="adminUserId">Admin user ID</param>
		/// <returns>Admin permissions details</returns>
		/// <response code="200">Permissions retrieved successfully</response>
		/// <response code="403">Admin belongs to different school</response>
		/// <response code="404">Admin user not found</response>
		[HttpGet("GetAdminPermissions")]
		[Authorize(Roles = "Administrator,SuperAdministrator")]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		[ProducesResponseType(typeof(BaseResponse), 403)]
		[ProducesResponseType(typeof(BaseResponse), 404)]
		public async Task<ActionResult<BaseResponse>> GetAdminPermissions(
			[FromQuery] Guid adminUserId)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _userService.GetAdminPermissions(adminUserId, userClaims);
			return Ok(result);
		}

		/// <summary>
		/// Get all admin permissions for the school (paginated)
		/// </summary>
		/// <param name="pageNumber">Page number (default: 1)</param>
		/// <param name="pageSize">Page size (default: 50, max: 100)</param>
		/// <returns>Paginated list of admin permissions</returns>
		/// <response code="200">Permissions list retrieved successfully</response>
		[HttpGet("GetAllAdminPermissions")]
		[Authorize(Roles = "SuperAdministrator")]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		public async Task<ActionResult<BaseResponse>> GetAllAdminPermissions(
			[FromQuery] int pageNumber = 1,
			[FromQuery] int pageSize = 50)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _userService.GetAllAdminPermissions(userClaims, pageNumber, pageSize);
			return Ok(result);
		}

		/// <summary>
		/// Revoke all permissions from an administrator
		/// </summary>
		/// <param name="adminUserId">Admin user ID</param>
		/// <returns>Success response</returns>
		/// <response code="200">Permissions revoked successfully</response>
		/// <response code="403">Not authorized (only SuperAdmins can revoke)</response>
		/// <response code="404">No permissions found for admin</response>
		[HttpPost("RevokePermissions")]
		[Authorize(Roles = "SuperAdministrator")]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		[ProducesResponseType(typeof(BaseResponse), 403)]
		[ProducesResponseType(typeof(BaseResponse), 404)]
		public async Task<ActionResult<BaseResponse>> RevokePermissions(
			[FromQuery] Guid adminUserId)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _userService.RevokeAdminPermissions(adminUserId, userClaims);
			return Ok(result);
		}
	}

}

