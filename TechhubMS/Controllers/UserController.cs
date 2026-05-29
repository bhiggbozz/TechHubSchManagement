using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.school;
using TechHub.Core.ViewModel.Users;
using TechHub.Service.Extension;
using TechHub.Service.Interface;
using TechHub.Service.Service;
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
		public async Task<ActionResult<BaseResponse>> UpdatePasswordForNewUser(UpdatePasswordViewModel updatePasswordViewModel)
		{
			var schoolIdClaim = User.GetAuthenticatedUserClaims();

			var result = await _userService.updatePassword(updatePasswordViewModel, schoolIdClaim);
			return Ok(result);
		}

		[HttpPost("update-password/newUser")]
		[AllowAnonymous]
		public async Task<IActionResult> UpdatePasswordForO([FromBody] UpdatePasswordViewModelV2 model)
		{
			//var claims = User.GetAuthenticatedUserClaims();
			//var tenant = await _tenantService.GetTenantBySchoolIdAsync(model.SchoolId);

			var result = await _userService.UpdatePasswordFirstTime(model);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
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
		[Authorize(Roles = "SuperAdministrator")]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		[ProducesResponseType(typeof(BaseResponse), 400)]
		[ProducesResponseType(typeof(BaseResponse), 403)]
		[ProducesResponseType(typeof(BaseResponse), 404)]
		public async Task<ActionResult<BaseResponse>> AssignPermissions([FromBody] AssignAdminPermissionsViewModel model)
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

		/// <summary>
		/// Get all teachers for the authenticated user's school
		/// </summary>
		[HttpGet("teachers")]
		[Authorize]
		public async Task<IActionResult> GetTeachers()
		{
			var userClaims = User.GetAuthenticatedUserClaims(); 
			var response = await _userService.GetTeachersBySchool(userClaims);

			return Ok(response);
		}
		[HttpPost("refresh-token")]
		[AllowAnonymous]
		public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
		{
			var result = await _userService.RefreshToken(request.RefreshToken);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.BadRequest => BadRequest(result),
				ResponseCode.Unauthorized => Unauthorized(result),
				_ => StatusCode(500, result)
			};
		}

		[HttpGet("GetUsersByRole")]
		[Authorize(Roles = "SuperAdministrator,Administrator")]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		public async Task<ActionResult<BaseResponse>> GetUsersByRole(int? roleId,[FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 50)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _userService.GetUsersByRole(userClaims, roleId, pageNumber, pageSize);
			return Ok(result);
		}

		[HttpGet("approvals")]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		public async Task<IActionResult> GetPendingApprovals()
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _userService.GetPendingApprovalsForUser(userClaims);
			return Ok(result);
		}

		[HttpPost("approvals/{id}/respond")]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		[ProducesResponseType(typeof(BaseResponse), 400)]
		[ProducesResponseType(typeof(BaseResponse), 404)]
		public async Task<IActionResult> RespondToApproval(Guid id, [FromBody] ApprovalRespondViewModel model)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _userService.RespondToApproval(id, model, userClaims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				_ => BadRequest(result)
			};
		}

		/// <summary>
		/// Assign a teacher to a classroom
		/// </summary>
		/// <param name="model">Teacher and classroom assignment details</param>
		/// <returns>Assignment result</returns>
		/// <response code="200">Teacher assigned successfully</response>
		/// <response code="400">Invalid request or teacher already assigned to this classroom</response>
		/// <response code="403">Not authorized to assign teachers to classrooms</response>
		/// <response code="404">Teacher or classroom not found</response>
		[HttpPost("AssignTeacherToClassroom")]
		[Authorize(Roles = "SuperAdministrator,Administrator")]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		[ProducesResponseType(typeof(BaseResponse), 400)]
		[ProducesResponseType(typeof(BaseResponse), 403)]
		[ProducesResponseType(typeof(BaseResponse), 404)]
		public async Task<ActionResult<BaseResponse>> AssignTeacherToClassroom([FromBody] AssignTeacherToClassroomViewModel model)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _userService.AssignTeacherToClassroom(model, userClaims);
			return Ok(result);
		}


		[HttpPut("teacher/{teacherId}/subject")]
		[Authorize(Roles = "Administrator,SuperAdministrator")]
		[ProducesResponseType(typeof(BaseResponse), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> UpdateTeacherSubject([FromRoute] Guid teacherId,[FromBody] UpdateTeacherSubjectViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			if (claims == null || string.IsNullOrEmpty(claims.UserId))
				return Unauthorized(new BaseResponse
				{
					ResponseCode = ResponseCode.Unauthorized,
					ResponseMessage = "Invalid user claims",
					Status = "failed"
				});

			var result = await _userService.UpdateTeacherSubject(teacherId, model, claims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				ResponseCode.Forbidden => StatusCode(403, result),
				ResponseCode.Unauthorized => Unauthorized(result),
				_ => BadRequest(result)
			};
		}



	}

}

