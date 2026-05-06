using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.DTO;
using TechHub.Core.Enum;
using TechHub.Core.Model;
using TechHub.Core.Models;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.Users;

namespace TechHub.Service.Interface
{
	public interface IUserService
	{
		Task<BaseResponse> LoginUser(LoginViewModel loginViewModel, TenantInfo? tenantInfo);
		Task<BaseResponse> CreateUser(UserViewModelV2 userViewModel, AuthenticatedUserClaims? claims);

		Task<BaseResponse> GetStudents(AuthenticatedUserClaims? claims, int pageNumber, int pageSize);

		Task<BaseResponse> EditUser(UpdateUserView userViewModel, AuthenticatedUserClaims? claims);

		Task<BaseResponse> updatePassword(UpdatePasswordViewModel updatePasswordViewModel, AuthenticatedUserClaims claims);
		Task<BaseResponse> UpdatePasswordFirstTime(UpdatePasswordViewModelV2 updatePasswordViewModel);
		//Task<BaseResponse> UpdatePasswordFirstTime(UpdatePasswordViewModelV2 updatePasswordViewModel, TenantInfo tenant);


		Task<BaseResponse> GetUserById(Guid userId, AuthenticatedUserClaims? claims);
		Task<BaseResponse> AssignAdminPermissions( AssignAdminPermissionsViewModel model,AuthenticatedUserClaims userClaims);
		Task<BaseResponse> GetAdminPermissions(Guid adminUserId,AuthenticatedUserClaims userClaims);
		Task<BaseResponse> GetAllAdminPermissions(AuthenticatedUserClaims userClaims,int pageNumber = 1,int pageSize = 50);

		
		Task<BaseResponse> RevokeAdminPermissions(Guid adminUserId,AuthenticatedUserClaims userClaims);

		
		Task<bool> HasPermission(Guid adminUserId,Guid schoolId,AdminPermission permission);
		Task<BaseResponse> GetTeachersBySchool(AuthenticatedUserClaims userClaims);
		Task<BaseResponse> RefreshToken(string incomingToken);
		Task<BaseResponse> GetUsersByRole(AuthenticatedUserClaims userClaims, int? roleId, int pageNumber, int pageSize);
		Task<BaseResponse> RespondToApproval(Guid approvalId, ApprovalRespondViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> GetPendingApprovalsForUser(AuthenticatedUserClaims claims);
	}
}
