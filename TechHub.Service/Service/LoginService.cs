using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.Helper;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;
using TechhubMS.util;

namespace TechHub.Service.Service
{
	public class LoginService
	{
		private readonly IQueryRepository<LoginHistory> _queryrepositoryLoginHistory;
		private readonly IQueryRepository<User> _queryrepositoryUser;
		private readonly CommandRepositoryService<LoginHistory> _commandRepositoryLoginHistory;
		private readonly IMapper _mapper;

		public LoginService(IQueryRepository<LoginHistory> queryRepositoryLoginHistory, IQueryRepository<User> queryrepositoryUser, 
			CommandRepositoryService<LoginHistory> commandRepositoryLoginHistory, IMapper mapper)
		{
			_queryrepositoryLoginHistory = queryRepositoryLoginHistory;
			_queryrepositoryUser = queryrepositoryUser;
			_commandRepositoryLoginHistory = commandRepositoryLoginHistory;
			_mapper = mapper;
		}
		

		public async Task<BaseResponse> LoginUser(LoginViewModel loginViewModel)
		{
			if(loginViewModel is null)
			{
				throw new ArgumentNullException(nameof(loginViewModel));
			}
			var nullProp = HelperUtil.GetNullPorpertiesName(loginViewModel);
			if (string.IsNullOrEmpty(nullProp))
			{
			    return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "one of object properties is null or empty", Status = "failed" };
			}
			var user = await _queryrepositoryUser.GetByPropertyName(nameof(loginViewModel.Username), loginViewModel.Username);
			if(user is null)
			{
				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "incorrect credentials", Status = "failed" };
			}
			var loginUser = _mapper.Map<LoginHistory>(loginViewModel);
			loginUser.UserId = user.Id;
			loginUser.RoleId = user.RoleId;
			if (loginViewModel.HashPassword != user.HashPassword)
			{
				loginUser.PasswordFailed = true;
				await _commandRepositoryLoginHistory.Create(loginUser);
				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "incorrect credentials", Status = "failed" };
			}
			loginUser.PasswordFailed = false;
			await _commandRepositoryLoginHistory.Create(loginUser);
			return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "successful", Status = "successful" };


		}
		private async Task<LoginHistory?> LastLoginHistory(Guid userId)
		{
			//string tableName
			string query = $"select top 1 from LoginHistory where userId = '{userId}'";
			var lastLoginHistory = await _queryrepositoryLoginHistory.Get(query);
			return lastLoginHistory;
		}
	}
}
