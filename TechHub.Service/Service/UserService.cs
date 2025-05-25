using AutoMapper;
using Microsoft.Data.SqlClient;
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
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;
using TechhubMS.util;

namespace TechHub.Service.Service
{
	public class UserService : IUserService
	{
		private readonly IQueryRepository<LoginHistory> _queryrepositoryLoginHistory;
		private readonly IQueryRepository<User> _queryrepositoryUser;
		private readonly ICommandRespository<LoginHistory> _commandRepositoryLoginHistory;
		private readonly ICommandRespository<User> _commandRepositoryUser;
		private readonly IQueryRepository<School> _queryrepositorySchool;
		private readonly IQueryRepository<SchoolCode> _schCodeQueryRespository;




		private readonly IMapper _mapper;

		public UserService(IQueryRepository<LoginHistory> queryRepositoryLoginHistory, IQueryRepository<User> queryrepositoryUser, 
			ICommandRespository<LoginHistory> commandRepositoryLoginHistory, ICommandRespository<User> commandRepositoryUser,
			IQueryRepository<School> queryrepositorySchool, IQueryRepository<SchoolCode> schCodeQueryRespository, IMapper mapper)
		{
			_queryrepositoryLoginHistory = queryRepositoryLoginHistory;
			_queryrepositoryUser = queryrepositoryUser;
			_commandRepositoryLoginHistory = commandRepositoryLoginHistory;
			_commandRepositoryUser = commandRepositoryUser;
			_queryrepositorySchool = queryrepositorySchool;
			_schCodeQueryRespository = schCodeQueryRespository;
			_mapper = mapper;
		}
		

		public async Task<BaseResponse> LoginUser(LoginViewModel loginViewModel)
		{
			try
			{

				if (loginViewModel is null)
				{
					throw new ArgumentNullException(nameof(loginViewModel));
				}
				var nullProp = HelperUtil.GetNullPorpertiesName(loginViewModel);
				if (string.IsNullOrEmpty(nullProp))
				{
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "one of object properties is null or empty", Status = "failed" };
				}
				string schQuery = $"select * from SchoolCode where Code = {loginViewModel.Inst}";
				var schoolId = await _schCodeQueryRespository.Get(schQuery);
				if(schQuery == null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "wrong Inst Code ", Status = "failed" };
				}
				var user = await _queryrepositoryUser.GetByPropertyName(nameof(loginViewModel.Username), loginViewModel.Username);
				if (user is null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "incorrect credentials", Status = "failed" };
				}
				var lastThreeLogins = await LastLoginHistorys(user.Id);
				var lstThreeLoginsFailed = lastThreeLogins.Select(c => c.PasswordFailed == true).ToList();
				if(lstThreeLoginsFailed.Count() == 3)
				{
					await _commandRepositoryUser.UpdateTableColumnById(nameof(user.IsActive), nameof(user.Id), false, user.Id);
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This Account is Locked, contact your Administrator", Status = "failed" };

				}
				var loginUser = _mapper.Map<LoginHistory>(loginViewModel);
				loginUser.UserId = user.Id;
				loginUser.RoleId = user.RoleId;
				if (!user.IsActive)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This Account is Locked, contact your Administrator", Status = "failed" };

				}
				if (loginViewModel.HashPassword != user.HashPassword)
				{
					loginUser.PasswordFailed = true;
					await _commandRepositoryLoginHistory.Create(loginUser);
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "incorrect credentials", Status = "failed" };
				}
				loginUser.PasswordFailed = false;
				await _commandRepositoryLoginHistory.Create(loginUser);
				var schInfo = await _queryrepositorySchool.Get(schoolId.SchoolId);
				var mappedSchInfo = _mapper.Map<SchoolResponseModel>(schInfo);
				return new UserLoginResponse { FirstName = user.FirstName, LastName = user.LastName, RoleId = user.RoleId, Id = user.Id, EmailAddress = user.EmailAddress,
					SchoolInfo = mappedSchInfo, ResponseCode = ResponseCode.successful, ResponseMessage = "successful", Status = "successful" };
			}
			
			catch(Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "Server Error", Status = "failed" };
			}


		}
		public async Task<BaseResponse> CreateUser(UserViewModel userViewModel)
		{
			try
			{
				if (userViewModel is null)
				{
					throw new ArgumentNullException(nameof(userViewModel));
				}
				var nullProp = HelperUtil.GetNullPorpertiesName(userViewModel);
				if (string.IsNullOrEmpty(nullProp))
				{
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "one of object properties is null or empty", Status = "failed" };
				}
				var createdByUser = await _queryrepositoryUser.Get(userViewModel.Createdby);
				if(createdByUser == null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "The Admin user doesn't exist", Status = "failed" };
				}
				var mappedUser = _mapper.Map<User>(userViewModel);
				await _commandRepositoryUser.Create(mappedUser);
				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "object created successfully", Status = "successful" };
			}
			catch (SqlException ex)
			{
				if (ex.Message.ToLower().Contains("duplicate"))
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "username exists", Status = "failed" };
				}
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };


			}
			catch (Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
			}

		}
		private async Task<IEnumerable<LoginHistory?>> LastLoginHistorys(Guid userId)
		{
			//string tableName
			string query = $"select top 3 from LoginHistory where userId = '{userId}'";
			var lastLoginHistory = await _queryrepositoryLoginHistory.GetByQuery(query);
			return lastLoginHistory;
		}
	}
}
