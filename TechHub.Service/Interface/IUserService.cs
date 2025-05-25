using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.ViewModel;
using TechHub.Core;

namespace TechHub.Service.Interface
{
	public interface IUserService
	{
		Task<BaseResponse> LoginUser(LoginViewModel loginViewModel);
		Task<BaseResponse> CreateUser(UserViewModel userViewModel);
	}
}
