using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TechHub.Core;
using TechHub.Core.Constant;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Helper;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;
using TechhubMS.util;

namespace TechHub.Service.Service
{
	public class UserService : IUserService
	{
		private readonly IQueryRepository<LoginHistory> _queryrepositoryLoginHistory;
		private readonly IQueryRepository<Users> _queryrepositoryUser;
		private readonly ICommandRespository<LoginHistory> _commandRepositoryLoginHistory;
		private readonly ICommandRespository<Users> _commandRepositoryUser;
		private readonly ICommandRespository<StudentClassroom> _commandRepositoryStudentClassroom;
        private readonly ICommandRespository<TeacherClassroom> _commandRepositoryTeacherClassroom;
        private readonly ICommandRespository<TeacherSubject> _commandRepositoryTeacherSubject;
        private readonly ICommandRespository<StudentMinorSubject> _commandRepositoryMinorSubject;
        private readonly ICommandRespository<StudentCourses> _studentCourseCommandRepository;
		private readonly IQueryRepository<School> _queryrepositorySchool;
		private readonly IQueryRepository<SchoolCode> _schCodeQueryRespository;
		private readonly IConfiguration _configuration;
		private readonly IMapper _mapper;
		private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;
		//private readonly ILogger _logger;
		private readonly string? _connString;

		public UserService(IQueryRepository<LoginHistory> queryRepositoryLoginHistory, IQueryRepository<Users> queryrepositoryUser,
			ICommandRespository<LoginHistory> commandRepositoryLoginHistory, ICommandRespository<Users> commandRepositoryUser,
			IQueryRepository<School> queryrepositorySchool, IQueryRepository<SchoolCode> schCodeQueryRespository, ICommandRespository<StudentCourses> studentCourseCommandRepository,
			IDbTransactionScopeFactory dbTransactionScopeFactory, IConfiguration configuration, ICommandRespository<StudentClassroom> commandRepositoryStudentClassroom, ICommandRespository<TeacherClassroom> commandRepositoryTeacherClassroom,
            ICommandRespository<TeacherSubject> commandRepositoryTeacherSubject, ICommandRespository<StudentMinorSubject> commandRepositoryMinorSubject, IMapper mapper)
		{
			_queryrepositoryLoginHistory = queryRepositoryLoginHistory;
			_queryrepositoryUser = queryrepositoryUser;
			_commandRepositoryLoginHistory = commandRepositoryLoginHistory;
			_commandRepositoryUser = commandRepositoryUser;
			_studentCourseCommandRepository = studentCourseCommandRepository;
			_queryrepositorySchool = queryrepositorySchool;
			_schCodeQueryRespository = schCodeQueryRespository;
			_dbTransactionScopeFactory = dbTransactionScopeFactory;
			_commandRepositoryStudentClassroom = commandRepositoryStudentClassroom;
			_commandRepositoryTeacherClassroom = commandRepositoryTeacherClassroom;
			_commandRepositoryTeacherSubject = commandRepositoryTeacherSubject;
			_commandRepositoryMinorSubject = commandRepositoryMinorSubject;
			_mapper = mapper;
			_connString = _configuration.GetConnectionString("DbConnectionString") ?? null;

		}


		public async Task<BaseResponse> LoginUser(LoginViewModel loginViewModel)
		{
			try
			{

				if (loginViewModel is null)
				{
					throw new ArgumentNullException(nameof(loginViewModel));
				}
				//var nullProp = HelperUtil.GetNullPorpertiesName(loginViewModel);
				//if (string.IsNullOrEmpty(nullProp))
				//{
				//	return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "one of object properties is null or empty", Status = "failed" };
				//}
				string schQuery = $"select * from SchoolCode where Code = '{loginViewModel.Inst}'";
				var schoolId = await _schCodeQueryRespository.Get(schQuery);
				if (schQuery == null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "wrong Inst Code ", Status = "failed" };
				}
				var loginUserInput = new Dictionary<string, object> { { "UserName", loginViewModel.Username }, { "SchoolCode", loginViewModel.Inst } };
				var user = await _queryrepositoryUser.GetBy(loginUserInput);
				//var user = await _queryrepositoryUser.GetByPropertyName(nameof(loginViewModel.Username), loginViewModel.Username);
				if (user is null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "incorrect credentials", Status = "failed" };
				}
				var loginUser = _mapper.Map<LoginHistory>(loginViewModel);
				loginUser.UserId = user.Id;
				loginUser.RoleId = user.RoleId;
				var lastThreeLogins = await LastLoginHistorys(user.Id);
				if (!lastThreeLogins.Any())
				{
					loginUser.PasswordFailed = false;
					await _commandRepositoryLoginHistory.Create(loginUser);
					var schoolResponse = new SchoolResponseModel
					{
						Id = user.SchoolId
					};
					return new UserLoginResponse { SchoolInfo = schoolResponse, FirstTimeLogin = true, ResponseCode = ResponseCode.successful, ResponseMessage = "first time login", Status = "successful" };
				}
				var lstThreeLoginsFailed = lastThreeLogins.Where(c => c.PasswordFailed == true).ToList();
				if (lstThreeLoginsFailed.Count() == 3)
				{
					await _commandRepositoryUser.UpdateTableColumnById(nameof(user.IsActive), nameof(user.Id), false, user.Id);
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This Account is Locked, contact your Administrator", Status = "failed" };
				}

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
				return new UserLoginResponse { FirstName = user.FirstName, LastName = user.LastName, RoleId = user.RoleId, Id = user.Id, EmailAddress = user.EmailAddress, IsActive = user.IsActive,
					SchoolInfo = mappedSchInfo, ResponseCode = ResponseCode.successful, ResponseMessage = "successful", Status = "successful" };
			}

			catch (Exception ex)
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
				var user = await _queryrepositoryUser.Get(userViewModel.Createdby);
				if (user is null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "user does not exist", Status = "failed" };
				}
				if (!(user.RoleId == (int)UserRole.Administrator) || !(user.RoleId == (int)UserRole.SuperAdministrator))
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "Unauthorised", Status = "failed" };
				}
				if(userViewModel.Role == UserRole.Student && userViewModel.UserClassroomsId.Count > 1)
				{
                    return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "student cannot be register to more than one classroom", Status = "failed" };
                }
                var mappedUser = _mapper.Map<Users>(user);
				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");

				await _commandRepositoryUser.Create(scope.Transaction, scope.Connection, user);
				//var nullProp = HelperUtil.GetNullPorpertiesName(userViewModel);
				//if (string.IsNullOrEmpty(nullProp))
				//{
				//	return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "one of object properties is null or empty", Status = "failed" };
				//}
				//var createdByUser = await _queryrepositoryUser.Get(userViewModel.Createdby);
				//if(createdByUser == null)
				//{
				//	return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "The Admin user doesn't exist", Status = "failed" };
				//}
				var InsertDIct = new Dictionary<string, object>();
                var listDictSubject = new List<Dictionary<string, object>>();
                switch (userViewModel.Role)
				{
                    case UserRole.Student:
						var studentClassRoom = new StudentClassroom
						{
							StudentId = mappedUser.Id,
							ClassroomId = userViewModel.UserClassroomsId[0],
							CreatedBy = userViewModel.Createdby,
							IsActive = true,
							SchoolId = userViewModel.SchoolId
						};
						var InsertDIct2 = new Dictionary<string, object>{{"Id",studentClassRoom.Id },{ "CreationDate", studentClassRoom.CreationDate}, { "ModiFiedDate", studentClassRoom.ModifiedDate},
						{"StudentId", mappedUser.Id }, { "ClassroomId", studentClassRoom.ClassroomId}, { "SchoolId", studentClassRoom.SchoolId}, {"IsActive", studentClassRoom.IsActive}, { "CreatedBy", studentClassRoom.CreatedBy } };

						await _commandRepositoryStudentClassroom.Create(scope.Transaction, scope.Connection, InsertDIct2);
						foreach(var studentMinorSubject in userViewModel.UserSubjects)
						{
                            var subjects = new StudentMinorSubject
                            {
                                StudentId = mappedUser.Id,
                                SubjectId = studentMinorSubject,
                                CreatedBy = userViewModel.Createdby,
                                IsActive = true,
                                SchoolId = userViewModel.SchoolId
                            };
                            var InsertDictSubj = new Dictionary<string, object>{{"Id",subjects.Id },{ "CreationDate", subjects.CreationDate}, { "ModiFiedDate",subjects.ModifiedDate},
							 {"StudentId", mappedUser.Id }, { "SubjectId", subjects.SubjectId}, { "SchoolId", subjects.SchoolId}, {"IsActive", subjects.IsActive}, { "CreatedBy", subjects.CreatedBy } };
							listDictSubject.Add(InsertDictSubj);
                        }
                        if (listDictSubject.Count > 0)
                        {
                            await _commandRepositoryMinorSubject.CreateBatchAsync(scope.Transaction, scope.Connection, listDictSubject);
                        }
						break;
                    case UserRole.SubjectTeacher:
					case UserRole.HeadTeacher:
					case UserRole.Administrator:
					case UserRole.SuperAdministrator:
						var listDict = new List<Dictionary<string, object>>();

                        foreach (var classroom in userViewModel.UserClassroomsId)
						{

                            var teacherClassRoom = new TeacherClassroom
                            {
                                TeacherId = user.Id,
                                ClassroomId = classroom,
                                CreatedBy = userViewModel.Createdby,
                                IsActive = true,
                                SchoolId = userViewModel.SchoolId
                            };
                            var InsertDict = new Dictionary<string, object>{{"Id",teacherClassRoom.Id },{ "CreationDate", teacherClassRoom.CreationDate}, { "ModiFiedDate", teacherClassRoom.ModifiedDate},
                                {"TeacherId", mappedUser.Id }, { "ClassroomId", teacherClassRoom.ClassroomId}, { "SchoolId", teacherClassRoom.SchoolId}, {"IsActive", teacherClassRoom.IsActive}, { "CreatedBy", teacherClassRoom.CreatedBy } };
							listDict.Add(InsertDict);
                        }
						if(listDict.Count > 0)
						{
                            await _commandRepositoryTeacherClassroom.CreateBatchAsync(scope.Transaction, scope.Connection, listDict);
                        }
                        foreach (var subject in userViewModel.UserSubjects)
						{
                            var teacherSubject = new TeacherSubject
                            {
                                TeacherId = mappedUser.Id,
                                SubjectId = subject,
                                CreatedBy = userViewModel.Createdby,
                                IsActive = true,
                                SchoolId = userViewModel.SchoolId
                            };
                            var InsertDict = new Dictionary<string, object>{{"Id",teacherSubject.Id },{ "CreationDate", teacherSubject.CreationDate}, { "ModiFiedDate", teacherSubject.ModifiedDate},
                                {"TeacherId", mappedUser.Id }, { "SubjectId", teacherSubject.SubjectId}, { "SchoolId", teacherSubject.SchoolId}, {"IsActive", teacherSubject.IsActive}, { "CreatedBy", teacherSubject.CreatedBy } };
                            listDictSubject.Add(InsertDict);
                        }
						if(listDictSubject.Count > 0)
						{
                            await _commandRepositoryTeacherSubject.CreateBatchAsync(scope.Transaction, scope.Connection, listDictSubject);
                        }
						break;
					default:
                        Console.WriteLine($"Unexpected role encountered: {userViewModel.Role}");
                        break;
                }
                await scope.CommitAsync();

                //            var mappedUser = _mapper.Map<Users>(userViewModel);
                //await _commandRepositoryUser.Create(mappedUser);
                return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "object created successfully", Status = "successful" };
			}
			catch (SqlException ex)
			{
				if (ex.Message.ToLower().Contains("duplicate"))
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "some user details exists", Status = "failed" };
				}
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };


			}
			catch (Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
			}

		}


		//public async Task<BaseResponse> SaveClassTeacherUser(UserViewModel userViewModel)
		//{

		//}

		//public async Task ValidateIfUserHasAccessToRemovedClasses(List<Guid> classes, )

		public async Task<BaseResponse> updatePassword(UpdatePasswordViewModel updatePasswordViewModel)
		{
			try
			{
				if(updatePasswordViewModel is null)
				{
					throw new ArgumentNullException(nameof(updatePasswordViewModel));
				}
				var selectQuery = $"select * from Users where SchoolId = @{nameof(updatePasswordViewModel.SchoolId)} and UserName = @{nameof(updatePasswordViewModel.username)}";
				var inputValue = new Dictionary<string, object> { { "SchoolId", updatePasswordViewModel.SchoolId }, { "UserName", updatePasswordViewModel.username } };
				var updateQuery = $"Update Users set HashPassword = @{nameof(updatePasswordViewModel.HashPassword)} where UserName = @{updatePasswordViewModel.username} " +
					$"and SchoolId = @{nameof(updatePasswordViewModel.SchoolId)}";


				var user = await _queryrepositoryUser.SelectByColumns(selectQuery, inputValue);
				if(user is null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = ResponseMessage.BadCredential, Status = "failed" };
				}


				var updateQueryValue = new Dictionary<string, object> { { "SchoolId", updatePasswordViewModel.SchoolId }, { "UserName", updatePasswordViewModel.username },
					{ "HashPassword", updatePasswordViewModel.HashPassword  }};
				var updateQueryKeyValue = new KeyValuePair<string, object> ("Id", user.Id );
				//var loginHistory = _mapper.Map<LoginHistory>(user);
				var loginHistory = new LoginHistory();
				loginHistory.UserId = user.Id;
				loginHistory.RoleId = user.RoleId;
				loginHistory.PasswordFailed = false;
				loginHistory.DeviceIp = updatePasswordViewModel.DeviceIp;
				loginHistory.DeviceType = updatePasswordViewModel.DeviceType;

				var loginHistoryDict = new Dictionary<string, object> { { "Id", loginHistory.Id }, { "CreationDate", loginHistory.CreationDate }, { "ModifiedDate", loginHistory.ModifiedDate},
					{"UserId", loginHistory.UserId },{ "RoleId", loginHistory.RoleId},{ "PasswordFailed",true },{"DeviceType", loginHistory.DeviceType }, {"DeviceIp" , loginHistory.DeviceIp} };
				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");

				await _commandRepositoryUser.UpdateAsync(scope.Transaction, scope.Connection,updateQuery, updateQueryValue, updateQueryKeyValue);
				await _commandRepositoryLoginHistory.Create(scope.Transaction, scope.Connection,loginHistoryDict);
				await scope.CommitAsync();
				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "Password Changed Successfully", Status = "successful" };

			}
			catch (ArgumentNullException ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage =ex.Message , Status = "falied" };
			}
			catch (Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };

			}
		}
		private async Task<IEnumerable<LoginHistory?>> LastLoginHistorys(Guid userId)
		{
			//string tableName
			string query = $"select top 3 * from LoginHistory where UserId = '{userId}'";
			var lastLoginHistory = await _queryrepositoryLoginHistory.GetByQuery(query);
			return lastLoginHistory;
		}
		private async Task<BaseResponse> RegisterToClass(RegisterStudentClassViewModel registerStudentClassViewModel)
		{
			try
			{
				if (registerStudentClassViewModel is null)
				{
					throw new ArgumentNullException(nameof(registerStudentClassViewModel));
				}
				var userInputValues = new Dictionary<string, object> { { "SchoolId", registerStudentClassViewModel.SchoolId }, { "Id", registerStudentClassViewModel.StudentId } };
				var user = await _queryrepositoryUser.GetBy(userInputValues);
				if (user == null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = ResponseMessage.UserDoesNotExist, Status = "failed" };
				}
				if (!user.IsActive)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = ResponseMessage.UserNotActive, Status = "failed" };
				}
				var mappedstudentCourse = _mapper.Map<StudentCourses>(registerStudentClassViewModel);
				await _studentCourseCommandRepository.Create(mappedstudentCourse);
				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = ResponseMessage.CreatedSuccessfully, Status = "successful" };
			}
			catch (ArgumentNullException ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = ex.Message, Status = "falied" };
			}
			catch (SqlException ex)
			{
				if (ex.Message.ToLower().Contains("duplicate"))
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "Student already registered", Status = "failed" };
				}
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
			}
			catch (Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };

			}

		}
	}
}
