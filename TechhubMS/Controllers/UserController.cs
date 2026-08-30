using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.Platform;
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

		/// <summary>
		/// Requests a password reset link. Tenant-aware, same as login — the
		/// frontend must send X-Tenant-ID (or use the school's subdomain).
		/// Students are not sent a reset link; they're told to contact staff.
		/// </summary>
		[HttpPost("forgot-password")]
		[AllowAnonymous]
		public async Task<ActionResult<BaseResponse>> ForgotPassword(ForgotPasswordViewModel model)
		{
			var tenant = _tenantService.GetCurrentTenant();
			var result = await _userService.ForgotPassword(model, tenant);
			return Ok(result);
		}

		/// <summary>
		/// Completes a password reset from the emailed link. Anonymous and not
		/// tenant-scoped by the middleware — the token itself resolves the user
		/// and school server-side.
		/// </summary>
		[HttpPost("reset-password")]
		[AllowAnonymous]
		public async Task<ActionResult<BaseResponse>> ResetPassword(ResetPasswordViewModel model)
		{
			var result = await _userService.ResetPassword(model);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		/// <summary>
		/// Profiles a parent to up to 10 students. If a parent with the given
		/// email already exists in this school, the students are linked to
		/// that existing account instead of creating a duplicate.
		/// </summary>
		[HttpPost("profileParent")]
		[Authorize]
		public async Task<ActionResult<BaseResponse>> ProfileParent(ProfileParentViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.ProfileParent(model, claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		/// <summary>
		/// Links one or more existing students to an existing parent account.
		/// The parent must already exist (use profileParent to create a new one).
		/// </summary>
		[HttpPost("attachStudents")]
		[Authorize]
		public async Task<ActionResult<BaseResponse>> AttachStudentsToParent(AttachStudentsToParentViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.AttachStudentsToParent(model, claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		/// <summary>Unlinks one student from one parent (the parent account itself is untouched).</summary>
		[HttpPost("removeStudentParent")]
		[Authorize]
		public async Task<ActionResult<BaseResponse>> RemoveStudentParent(RemoveStudentParentViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.RemoveStudentParent(model, claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		/// <summary>Deactivates a parent account (blocks login). Their student links are left as-is.</summary>
		[HttpPost("deactivateParent/{parentId:guid}")]
		[Authorize]
		public async Task<ActionResult<BaseResponse>> DeactivateParent(Guid parentId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.DeactivateParent(parentId, claims);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		/// <summary>
		/// Lists a parent's children (Id, name, classroom). A Parent caller always
		/// gets their own children. An Administrator (with CreateUsers) or
		/// SuperAdministrator must pass <paramref name="parentId"/> to look up
		/// any parent in their school.
		/// </summary>
		[HttpGet("my-children")]
		[Authorize(Roles = "Parent,Administrator,SuperAdministrator")]
		public async Task<ActionResult<BaseResponse>> GetMyChildren([FromQuery] Guid? parentId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.GetMyChildren(claims, parentId);
			return result.ResponseCode == ResponseCode.successful ? Ok(result) : BadRequest(result);
		}

		/// <summary>
		/// Searches parents in the caller's school by name/email (<paramref name="q"/>),
		/// by parent email or surname specifically (<paramref name="parentEmail"/>,
		/// <paramref name="parentSurname"/>), by a linked student's name
		/// (<paramref name="studentName"/>), or by a known studentId to jump straight
		/// to their parent(s). All filters are optional and combine with AND — use one
		/// alone or any combination; omitting everything returns the full paginated
		/// parent list.
		/// </summary>
		[HttpGet("parents/search")]
		[Authorize(Roles = "Administrator,SuperAdministrator")]
		public async Task<ActionResult<BaseResponse>> SearchParents(
			[FromQuery] string? q,
			[FromQuery] string? studentName,
			[FromQuery] Guid? studentId,
			[FromQuery] string? parentEmail,
			[FromQuery] string? parentSurname,
			[FromQuery] int page = 1,
			[FromQuery] int pageSize = 20)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.SearchParents(claims, q, studentName, studentId, parentEmail, parentSurname, page, pageSize);
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

		[HttpGet("student/lesson/{subjectId}")]
		[Authorize]
		public async Task<IActionResult> GetLessonsBySubjectForStudent(Guid subjectId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var response = await _userService.GetLessonsBySubjectForStudent(subjectId, claims);
			return Ok(response);

		}


		[HttpGet("admin-permissions/{adminUserId}")]
		[Authorize]
		public async Task<IActionResult> GetAdminPermissionsById(Guid adminUserId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.GetAdminPermissionsById(adminUserId, claims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				ResponseCode.Forbidden => StatusCode(403, result),
				ResponseCode.Unauthorized => Unauthorized(result),
				_ => BadRequest(result)
			};
		}


		[HttpGet("{studentId}/subjects")]
		[Authorize]
		public async Task<IActionResult> GetAllStudentSubjects(Guid studentId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.GetAllStudentSubjects(studentId, claims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				ResponseCode.Forbidden => StatusCode(403, result),
				ResponseCode.Unauthorized => Unauthorized(result),
				_ => BadRequest(result)
			};
		}

		[HttpGet("{studentId}/minor-subjects")]
		[Authorize]
		public async Task<IActionResult> GetStudentMinorSubjects(Guid studentId,[FromQuery] Guid? classroomId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.GetStudentMinorSubjects(studentId, classroomId, claims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				ResponseCode.Forbidden => StatusCode(403, result),
				ResponseCode.Unauthorized => Unauthorized(result),
				_ => BadRequest(result)
			};
		}

		[HttpPut("{studentId}/minor-subjects")]
		[Authorize]
		public async Task<IActionResult> UpdateStudentMinorSubjects(Guid studentId, [FromQuery] Guid? classroomId, [FromBody] UpdateStudentMinorSubjectViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.UpdateStudentMinorSubjects(studentId, model, claims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				ResponseCode.Forbidden => StatusCode(403, result),
				ResponseCode.Unauthorized => Unauthorized(result),
				_ => BadRequest(result)
			};
		}

		[HttpPut("{studentId}/assignment")]
		[Authorize]
		public async Task<IActionResult> UpdateStudentAssignment(Guid studentId,[FromBody] UpdateStudentAssignmentViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.UpdateStudentAssignment(studentId, model, claims);


			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				ResponseCode.Forbidden => StatusCode(403, result),
				ResponseCode.Unauthorized => Unauthorized(result),
				_ => BadRequest(result)
			};
		}

		/// <summary>
		/// Get students in a specific classroom (with roleData)
		/// </summary>
		[HttpGet("students")]
		[Authorize]
		public async Task<IActionResult> GetStudentsByClassroom([FromQuery] Guid? classroomId, [FromQuery] Guid? subjectId)
		{
			var claims = User.GetAuthenticatedUserClaims();

			if (classroomId.HasValue)
			{
				var result = await _userService.GetStudentsByClassroom(classroomId.Value, claims);
				return Ok(result);
			}

			if (subjectId.HasValue)
			{
				var result = await _userService.GetStudentsBySubject(subjectId.Value, claims);
				return Ok(result);
			}

			return BadRequest(new BaseResponse
			{
				ResponseCode = ResponseCode.BadRequest,
				ResponseMessage = "Provide either classroomId or subjectId query parameter",
				Status = "failed"
			});
		}

		/// <summary>
		/// Unlock a user account and clear failed login attempts
		/// </summary>
		[HttpPost("{userId}/unlock")]
		[Authorize(Roles = "SuperAdministrator,Administrator")]
		[ProducesResponseType(typeof(BaseResponse), 200)]
		[ProducesResponseType(typeof(BaseResponse), 403)]
		[ProducesResponseType(typeof(BaseResponse), 404)]
		public async Task<IActionResult> UnlockUser(Guid userId)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.UnlockUserAccount(userId, claims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				ResponseCode.Forbidden => StatusCode(403, result),
				ResponseCode.Unauthorized => Unauthorized(result),
				_ => BadRequest(result)
			};
		}

		/// <summary>
		/// Get students for the logged-in teacher (auto-detected from JWT)
		/// </summary>
		[HttpGet("teacher/students")]
		[Authorize]
		public async Task<IActionResult> GetTeacherStudents()
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.GetTeacherStudents(claims);
			return Ok(result);
		}

		/// <summary>
		/// Create a school SuperAdministrator user by school code (Platform Admin only)
		/// POST /api/User/create-school-admin
		/// </summary>
		[HttpPost("create-school-admin")]
		[Authorize(Roles = "PlatformAdmin,PlatformSuperAdmin")]
		public async Task<IActionResult> CreateSchoolAdmin([FromBody] CreateSchoolAdminViewModel model)
		{
			var claims = User.GetAuthenticatedUserClaims();
			var result = await _userService.CreateSchoolAdmin(model, claims);
			return result.ResponseCode switch
			{
				"99000" => Ok(result),
				"99134" => NotFound(result),
				"99161" => StatusCode(StatusCodes.Status409Conflict, result),
				_ => BadRequest(result)
			};
		}
	}

}

