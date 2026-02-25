using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.ViewModel;
using TechHub.Core;
using TechHub.Core.Models;
using System.Security.Claims;
using TechHub.Core.Model;

namespace TechHub.Service.Interface
{
	public interface IUserService
	{
		Task<BaseResponse> LoginUser(LoginViewModel loginViewModel, TenantInfo? tenantInfo);
		Task<BaseResponse> CreateUser(UserViewModel userViewModel, AuthenticatedUserClaims? claims);

		Task<BaseResponse> GetStudents(AuthenticatedUserClaims? claims, int pageNumber, int pageSize);

		Task<BaseResponse> EditUser(UpdateUserView userViewModel, AuthenticatedUserClaims? claims);

		Task<BaseResponse> updatePassword(UpdatePasswordViewModel updatePasswordViewModel, AuthenticatedUserClaims? claims);
		Task<BaseResponse> GetUserById(Guid userId, AuthenticatedUserClaims? claims);

	}
}
