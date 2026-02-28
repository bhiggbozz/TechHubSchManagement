using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
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
using TechHub.Core.Models;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Service.Extension;
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
		private readonly ICommandRespository<Classroom> _classroomCommandRespository;

		private readonly IQueryRepository<School> _queryrepositorySchool;
		private readonly IQueryRepository<SchoolCode> _schCodeQueryRespository;
		private readonly IQueryRepository<Classroom> _classroomQueryRespository;
		private readonly IQueryRepository<StudentCourses> _studentCoursesQueryRespository;


		private readonly IConfiguration _configuration;
		private readonly IMapper _mapper;
		private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;
		private readonly ILogger _logger;
		private readonly string? _connString;

		public UserService(IQueryRepository<LoginHistory> queryRepositoryLoginHistory, IQueryRepository<Users> queryrepositoryUser,
			ICommandRespository<LoginHistory> commandRepositoryLoginHistory, ICommandRespository<Users> commandRepositoryUser,
			IQueryRepository<School> queryrepositorySchool, IQueryRepository<SchoolCode> schCodeQueryRespository, ICommandRespository<StudentCourses> studentCourseCommandRepository,
			ICommandRespository<Classroom> classroomCommandRespository, IQueryRepository<Classroom> classroomQueryRespository,
			IDbTransactionScopeFactory dbTransactionScopeFactory, IConfiguration configuration, ICommandRespository<StudentClassroom> commandRepositoryStudentClassroom, ICommandRespository<TeacherClassroom> commandRepositoryTeacherClassroom,
			ICommandRespository<TeacherSubject> commandRepositoryTeacherSubject, ICommandRespository<StudentMinorSubject> commandRepositoryMinorSubject, IMapper mapper, ILogger logger)
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
			_classroomCommandRespository = classroomCommandRespository;
			_classroomQueryRespository = classroomQueryRespository;
			_logger = logger;
			_configuration = configuration;

			_mapper = mapper;
			_connString = _configuration.GetConnectionString("DbConnectionString") ?? throw new ArgumentNullException("Db COnfig is null");

		}


		public async Task<BaseResponse> LoginUser(LoginViewModel loginViewModel, TenantInfo? tenant)
		{
			try
			{
				if (loginViewModel is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Login credentials cannot be empty",
						Status = "failed"
					};
				}

				if (tenant == null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid tenant information",
						Status = "failed"
					};
				}

				string schQuery = $"select * from SchoolCode where Code = '{loginViewModel.Inst}'";

				if (schQuery == null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Wrong institution code",
						Status = "failed"
					};
				}

				var loginUserInput = new Dictionary<string, object>
				{
					{ "UserName", loginViewModel.Username },
					{ "SchoolCode", loginViewModel.Inst }
				};

				var user = await _queryrepositoryUser.GetBy(loginUserInput);

				if (user is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Incorrect credentials",
						Status = "failed"
					};
				}

				var loginUser = _mapper.Map<LoginHistory>(loginViewModel);
				loginUser.UserId = user.Id;
				loginUser.RoleId = user.RoleId;

				var lastThreeLogins = await LastLoginHistorys(user.Id);

				// First time login
				if (!lastThreeLogins.Any())
				{
					loginUser.PasswordFailed = false;
					await _commandRepositoryLoginHistory.Create(loginUser);

					var schoolResponse = new SchoolResponseModel
					{
						Id = user.SchoolId
					};

					return new UserLoginResponse
					{
						SchoolInfo = schoolResponse,
						FirstTimeLogin = true,
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "First time login",
						Status = "successful"
					};
				}

				// Check for 3 consecutive failed logins
				var lstThreeLoginsFailed = lastThreeLogins.Where(c => c.PasswordFailed == true).ToList();

				if (lstThreeLoginsFailed.Count() == 3)
				{
					await _commandRepositoryUser.UpdateTableColumnById(nameof(user.IsActive), nameof(user.Id), false, user.Id);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "This account is locked, contact your administrator",
						Status = "failed"
					};
				}

				// Check if account is active
				if (!user.IsActive)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "This account is locked, contact your administrator",
						Status = "failed"
					};
				}

				// Verify password
				if (loginViewModel.HashPassword != user.HashPassword)
				{
					loginUser.PasswordFailed = true;
					await _commandRepositoryLoginHistory.Create(loginUser);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Incorrect credentials",
						Status = "failed"
					};
				}

				loginUser.PasswordFailed = false;
				await _commandRepositoryLoginHistory.Create(loginUser);

				var schInfo = await _queryrepositorySchool.Get(tenant.SchoolId);
				var mappedSchInfo = _mapper.Map<SchoolResponseModel>(schInfo);

				var token = GenerateJwtToken(user, tenant.SchoolId.ToString(), mappedSchInfo);

				return new UserLoginResponse
				{
					FirstName = user.FirstName,
					LastName = user.LastName,
					RoleId = user.RoleId,
					Id = user.Id,
					EmailAddress = user.EmailAddress,
					IsActive = user.IsActive,
					SchoolInfo = mappedSchInfo,
					Token = token,
					TokenExpiresIn = 3600,
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Login successful",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				// TODO: Log exception here
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Server error occurred",
					Status = "failed"
				};
			}
		}
		public async Task<BaseResponse> CreateUser(UserViewModel userViewModel, AuthenticatedUserClaims userClaims)
		{
			try
			{
				// ===== VALIDATION SECTION =====

				// Validation 1: Check if model is null
				if (userViewModel is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "User data cannot be empty",
						Status = "failed"
					};
				}

				// Validation 2: Validate user claims
				if (string.IsNullOrEmpty(userClaims.SchoolId) || string.IsNullOrEmpty(userClaims.UserId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "User authentication information is missing",
						Status = "failed"
					};
				}

				// Validation 3: Parse claims
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId format in token",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.UserId, out var createdBy))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid UserId format in token",
						Status = "failed"
					};
				}

				if (!int.TryParse(userClaims.Role, out int userRole))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid role format in token",
						Status = "failed"
					};
				}

				if (userRole != (int)UserRole.Administrator && userRole != (int)UserRole.SuperAdministrator)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You are not authorized to create users",
						Status = "failed"
					};
				}

				var creator = await _queryrepositoryUser.Get(createdBy);
				if (creator is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Creator user does not exist",
						Status = "failed"
					};
				}

				if (!creator.IsActive)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "Your account is not active",
						Status = "failed"
					};
				}

				// Validation 7: Role-specific validations
				var roleValidation = ValidateUserRoleRequirements(userViewModel);
				if (!roleValidation.IsValid)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = roleValidation.ErrorMessage,
						Status = "failed"
					};
				}

				// Validation 8: Check if user already exists
				var existingUser = await CheckUserExists(userViewModel.UserName, userViewModel.EmailAddress, schoolId);
				if (existingUser.Exists)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = existingUser.Message,
						Status = "failed"
					};
				}

				// ===== USER CREATION SECTION =====

				// Create the main user entity
				var newUser = new Users
				{
					Id = Guid.NewGuid(),
					FirstName = userViewModel.FirstName.Trim(),
					LastName = userViewModel.LastName.Trim(),
					UserName = userViewModel.UserName.Trim(),
					EmailAddress = userViewModel.EmailAddress.Trim().ToLower(),
					HashPassword = userViewModel.HashPassword,
					RoleId = (int)userViewModel.Role,
					SchoolId = schoolId,
					CreatedBy = createdBy,
					IsActive = true

				};

				var userDict = new Dictionary<string, object>
				{
					{ "Id", newUser.Id },
					{ "FirstName", newUser.FirstName },
					{ "LastName", newUser.LastName },
					{ "UserName", newUser.UserName },
					{ "Email", newUser.EmailAddress },
					{ "HashPassword", newUser.HashPassword },
					{ "RoleId", newUser.RoleId },
					{ "SchoolId", newUser.SchoolId },
					{ "CreatedBy", newUser.CreatedBy },
					{ "IsActive", newUser.IsActive },
					{ "CreationDate", newUser.CreationDate },
					{ "ModifiedDate", newUser.ModifiedDate}
				};


				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");

				await _commandRepositoryUser.Create(scope.Transaction, scope.Connection, userDict);

				switch (userViewModel.Role)
				{
					case UserRole.Student:
						await CreateStudentAssociations(scope, newUser.Id, userViewModel, schoolId, createdBy);
						break;

					case UserRole.SubjectTeacher:
					case UserRole.HeadTeacher:
					case UserRole.Administrator:
					case UserRole.SuperAdministrator:
						await CreateTeacherAssociations(scope, newUser.Id, userViewModel, schoolId, createdBy);
						break;

					default:
						await scope.RollbackAsync();
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Invalid user role: {userViewModel.Role}",
							Status = "failed"
						};
				}

				await scope.CommitAsync();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = $"{userViewModel.Role} user created successfully",
					Status = "successful",
					Data = new
					{
						UserId = newUser.Id,
						UserName = newUser.UserName,
						Email = newUser.EmailAddress,
						Role = userViewModel.Role.ToString(),
						ClassroomsAssigned = userViewModel.UserClassroomsId.Count,
						SubjectsAssigned = userViewModel.UserSubjects.Count
					}
				};
			}
			catch (SqlException ex)
			{
				if (ex.Message.ToLower().Contains("duplicate"))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = "User with this username or email already exists",
						Status = "failed"
					};
				}

				// Log exception
				// _logger.LogError(ex, "SQL error occurred while creating user");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Database error occurred while creating user",
					Status = "failed"
				};
			}
			catch (Exception ex)
			{
				// Log exception
				// _logger.LogError(ex, "Unexpected error occurred while creating user");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred while creating user",
					Status = "failed"
				};
			}
		}


		//public async Task<BaseResponse> SaveClassTeacherUser(UserViewModel userViewModel)
		//{

		//}

		//public async Task ValidateIfUserHasAccessToRemovedClasses(List<Guid> classes, )

		public async Task<BaseResponse> updatePassword(UpdatePasswordViewModel updatePasswordViewModel, AuthenticatedUserClaims claims)
		{
			try
			{
				if (updatePasswordViewModel is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Password update request cannot be empty",
						Status = "failed"
					};
				}

				if (string.IsNullOrEmpty(claims.SchoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "SchoolId not found in authentication token",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId format in token",
						Status = "failed"
					};
				}

				var selectQuery = $"SELECT * FROM Users WHERE SchoolId = @SchoolId AND UserName = @UserName";
				var selectParams = new Dictionary<string, object>
				{
					{ "SchoolId", schoolId },
					{ "UserName", updatePasswordViewModel.username }
				};

				// Find user
				var user = await _queryrepositoryUser.SelectByColumns(selectQuery, selectParams);
				if (user is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "User not found or does not belong to your school",
						Status = "failed"
					};
				}

				if (!string.IsNullOrEmpty(claims.UserId))
				{
					if (Guid.TryParse(claims.UserId, out var currentUserId))
					{
						if (user.Id != currentUserId && claims.Role != "Admin")
						{
							return new BaseResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = "You can only update your own password",
								Status = "failed"
							};
						}
					}
				}

				// Update password query
				var updateQuery = $"UPDATE Users SET HashPassword = @HashPassword, ModifiedDate = @ModifiedDate " +
								  $"WHERE Id = @Id AND SchoolId = @SchoolId";

				var updateParams = new Dictionary<string, object>
				{
					{ "HashPassword", updatePasswordViewModel.HashPassword },
					{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
					{ "SchoolId", schoolId },  // From claims
					{ "UserName", updatePasswordViewModel.username }
				};

				var updateKeyValue = new KeyValuePair<string, object>("Id", user.Id);

				// Create login history record
				var loginHistory = new LoginHistory
				{
					Id = Guid.NewGuid(),
					UserId = user.Id,
					RoleId = user.RoleId,
					PasswordFailed = false,
					DeviceType = updatePasswordViewModel.DeviceType,
					DeviceIp = updatePasswordViewModel.DeviceIp
				};

				var loginHistoryDict = new Dictionary<string, object>
				{
					{ "Id", loginHistory.Id },
					{ "CreationDate", loginHistory.CreationDate },
					{ "ModifiedDate", loginHistory.ModifiedDate },
					{ "UserId", loginHistory.UserId },
					{ "RoleId", loginHistory.RoleId },
					{ "PasswordFailed", loginHistory.PasswordFailed },
					{ "DeviceType", loginHistory.DeviceType },
					{ "DeviceIp", loginHistory.DeviceIp }
				};

				// Execute transaction
				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				await _commandRepositoryUser.UpdateAsync(scope.Transaction, scope.Connection, updateQuery, updateParams, updateKeyValue);
				await _commandRepositoryLoginHistory.Create(scope.Transaction, scope.Connection, loginHistoryDict);
				await scope.CommitAsync();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Password changed successfully",
					Status = "successful"
				};
			}
			catch (SqlException ex)
			{
				// Log exception here
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Database error occurred while updating password",
					Status = "failed"
				};
			}
			catch (Exception ex)
			{
				// Log exception here
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred while updating password",
					Status = "failed"
				};
			}
		}
		private async Task<IEnumerable<LoginHistory?>> LastLoginHistorys(Guid userId)
		{
			//string tableName
			string query = $"select top 3 * from LoginHistory where UserId = '{userId}'";
			var lastLoginHistory = await _queryrepositoryLoginHistory.GetByQuery(query);
			return lastLoginHistory;
		}
		public async Task<BaseResponse> RegisterToClass(RegisterStudentClassViewModel registerStudentClassViewModel, AuthenticatedUserClaims userInfo)
		{
			try
			{

				if (registerStudentClassViewModel is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Registration data cannot be empty",
						Status = "failed"
					};
				}

				if (string.IsNullOrEmpty(userInfo.SchoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "SchoolId not found in authentication token",
						Status = "failed"
					};
				}

				if (string.IsNullOrEmpty(userInfo.UserId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "UserId not found in authentication token",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userInfo.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId format in token",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userInfo.UserId, out var createdBy))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid UserId format in token",
						Status = "failed"
					};
				}

				var studentValidation = await ValidateStudent(registerStudentClassViewModel.StudentId, schoolId);
				if (!studentValidation.IsValid)
				{
					return new BaseResponse
					{
						ResponseCode = studentValidation.ResponseCode,
						ResponseMessage = studentValidation.Message,
						Status = "failed"
					};
				}

				var classroomValidation = await ValidateClassroom(registerStudentClassViewModel.ClassId, schoolId);
				if (!classroomValidation.IsValid)
				{
					return new BaseResponse
					{
						ResponseCode = classroomValidation.ResponseCode,
						ResponseMessage = classroomValidation.Message,
						Status = "failed"
					};
				}

				// Check if student is already registered in this classroom
				var isAlreadyRegistered = await CheckExistingRegistration(
					registerStudentClassViewModel.StudentId,
					registerStudentClassViewModel.ClassId
				);

				if (isAlreadyRegistered)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = "Student is already registered in this classroom",
						Status = "failed"
					};
				}

				var existingActiveClass = await GetStudentActiveClassroom(registerStudentClassViewModel.StudentId);
				if (existingActiveClass.HasValue)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = "Student is already enrolled in another classroom. Please transfer the student first.",
						Status = "failed",
						Data = new { CurrentClassroomId = existingActiveClass.Value }
					};
				}


				var studentCourse = new Dictionary<string, object>
				{
					{ "Id", Guid.NewGuid() },
					{ "CreationDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
					{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
					{ "ClassroomId", registerStudentClassViewModel.ClassId },
					{ "StudentId", registerStudentClassViewModel.StudentId },
					{ "SchoolId", schoolId },
					{ "CreatedBy", createdBy },
					{ "Status", (int)StudentClassroomStatus.Active }
				};


				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				await _studentCourseCommandRepository.Create(scope.Transaction, scope.Connection, studentCourse);
				await scope.CommitAsync();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Student registered to classroom successfully",
					Status = "successful",
					Data = new
					{
						RegistrationId = studentCourse["Id"],
						StudentId = registerStudentClassViewModel.StudentId,
						ClassroomId = registerStudentClassViewModel.ClassId,
						Status = StudentClassroomStatus.Active.ToString()
					}
				};
			}
			catch (SqlException ex)
			{
				if (ex.Message.ToLower().Contains("duplicate"))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = "Student is already registered in this classroom",
						Status = "failed"
					};
				}

				// Log exception
				// _logger.LogError(ex, "SQL error occurred while registering student to class");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Database error occurred while registering student",
					Status = "failed"
				};
			}
			catch (Exception ex)
			{
				// Log exception
				// _logger.LogError(ex, "Unexpected error occurred while registering student to class");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred while registering student",
					Status = "failed"
				};
			}
		}


		public async Task<BaseResponse> EditUser(UpdateUserView updateUserViewModel, AuthenticatedUserClaims? userClaims)
		{
			try
			{
				if (updateUserViewModel is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "User data cannot be empty",
						Status = "failed"
					};
				}

				if (string.IsNullOrEmpty(userClaims?.SchoolId) || string.IsNullOrEmpty(userClaims?.UserId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "User authentication information is missing",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var claimSchoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId format in token",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.UserId, out var modifiedBy))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid UserId format in token",
						Status = "failed"
					};
				}

				if (!int.TryParse(userClaims.Role, out int userRole))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid role format in token",
						Status = "failed"
					};
				}

				var modifier = await _queryrepositoryUser.Get(modifiedBy);
				if (modifier is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Modifier user does not exist",
						Status = "failed"
					};
				}

				if (!modifier.IsActive)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "Your account is not active",
						Status = "failed"
					};
				}


				var existingUser = await _queryrepositoryUser.Get(updateUserViewModel.Id);
				if (existingUser is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "User not found",
						Status = "failed"
					};
				}

				// Validation 6: Multi-tenancy check - ensure user belongs to same school
				if (existingUser.SchoolId != claimSchoolId)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You cannot update users from a different school",
						Status = "failed"
					};
				}

				// Validation 7: Role-based authorization
				// Only Admin and SuperAdmin can update users
				if (userRole != (int)UserRole.Administrator && userRole != (int)UserRole.SuperAdministrator)
				{
					// Allow users to update their own profile (limited fields)
					if (updateUserViewModel.Id != modifiedBy)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You are not authorized to update other users",
							Status = "failed"
						};
					}

					// Users can only update their own limited fields
					//return await UpdateOwnProfile(updateUserViewModel, existingUser, modifiedBy);
				}

				// Validation 8: SuperAdmin restrictions
				// Only SuperAdmin can update other SuperAdmins
				if (existingUser.RoleId == (int)UserRole.SuperAdministrator &&
					userRole != (int)UserRole.SuperAdministrator)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "Only SuperAdministrator can update SuperAdministrator accounts",
						Status = "failed"
					};
				}

				// Validation 9: Check if email is being changed and if it already exists
				if (!string.IsNullOrWhiteSpace(updateUserViewModel.EmailAddress) &&
					updateUserViewModel.EmailAddress.Trim().ToLower() != existingUser.EmailAddress?.ToLower())
				{
					var emailExists = await CheckEmailExists(
						updateUserViewModel.EmailAddress,
						claimSchoolId,
						updateUserViewModel.Id);

					if (emailExists)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Conflict,
							ResponseMessage = "Email address is already in use by another user",
							Status = "failed"
						};
					}
				}

				// ✅ Build update dictionary with ONLY provided values
				var updateDict = new Dictionary<string, object>
				{
					{ "ModifiedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
				};

				var updatedFields = new List<string>();

				// ✅ Check each field individually
				if (!string.IsNullOrWhiteSpace(updateUserViewModel.FirstName))
				{
					updateDict["FirstName"] = updateUserViewModel.FirstName.Trim();
					updatedFields.Add("FirstName");
				}

				if (!string.IsNullOrWhiteSpace(updateUserViewModel.LastName))
				{
					updateDict["LastName"] = updateUserViewModel.LastName.Trim();
					updatedFields.Add("LastName");
				}

				if (!string.IsNullOrWhiteSpace(updateUserViewModel.EmailAddress))
				{
					updateDict["EmailAddress"] = updateUserViewModel.EmailAddress.Trim().ToLower();
					updatedFields.Add("EmailAddress");
				}

				if (!string.IsNullOrWhiteSpace(updateUserViewModel.HashPassword))
				{
					// TODO: Hash password before storing (use BCrypt)
					// var hashedPassword = BCrypt.Net.BCrypt.HashPassword(updateUserViewModel.HashPassword);
					updateDict["HashPassword"] = updateUserViewModel.HashPassword;
					updatedFields.Add("Password");
				}

				// ✅ Use .HasValue for nullable bool
				if (updateUserViewModel.IsActive.HasValue)
				{
					updateDict["IsActive"] = updateUserViewModel.IsActive.Value;
					updatedFields.Add("IsActive");
				}

				if (updateUserViewModel.HasAccess.HasValue)
				{
					updateDict["HasAccess"] = updateUserViewModel.HasAccess.Value;
					updatedFields.Add("HasAccess");
				}

				if (updateUserViewModel.RoleId.HasValue &&
					updateUserViewModel.RoleId.Value != existingUser.RoleId)
				{
					// Validate role change permissions
					if (updateUserViewModel.RoleId.Value == (int)UserRole.SuperAdministrator &&
						userRole != (int)UserRole.SuperAdministrator)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Only SuperAdministrator can promote to SuperAdministrator role",
							Status = "failed"
						};
					}

					updateDict["RoleId"] = updateUserViewModel.RoleId.Value;
					updatedFields.Add("Role");
				}

				if (!string.IsNullOrWhiteSpace(updateUserViewModel.ProfileImage))
				{
					updateDict["ProfileImage"] = updateUserViewModel.ProfileImage;
					updatedFields.Add("ProfileImage");
				}

				if (!string.IsNullOrWhiteSpace(updateUserViewModel.GuardianName))
				{
					updateDict["GuardianName"] = updateUserViewModel.GuardianName.Trim();
					updatedFields.Add("GuardianName");
				}

				if (updateDict.Count == 1) // Only ModifiedDate
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "No fields to update",
						Status = "failed"
					};
				}
				var whereClause = new KeyValuePair<string, object>("Id", updateUserViewModel.Id);
				await _commandRepositoryUser.UpdateTableColumnById(updateDict, whereClause);


				//await scope.CommitAsync();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "User updated successfully",
					Status = "successful",
					Data = new
					{
						UserId = updateUserViewModel.Id,
						UpdatedFields = updatedFields.ToArray(),
						UpdatedBy = modifiedBy,
						UpdatedAt = updateDict["ModifiedDate"]
					}
				};
			}
			catch (SqlException ex)
			{
				if (ex.Message.ToLower().Contains("duplicate"))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = "Email address is already in use",
						Status = "failed"
					};
				}

				// Log exception
				// _logger.LogError(ex, "SQL error occurred while updating user: {UserId}", updateUserViewModel.Id);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Database error occurred while updating user",
					Status = "failed"
				};
			}
			catch (Exception ex)
			{
				// Log exception

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred while updating user",
					Status = "failed"
				};
			}
		}

		/// <summary>
		/// Allow users to update their own profile with limited fields
		/// </summary>
		private async Task<BaseResponse> UpdateOwnProfile(UpdateUserView updateUserViewModel, Users existingUser, Guid userId)
		{
			try
			{
				// Users can only update: FirstName, LastName, ProfileImage, Password
				var updateDict = new Dictionary<string, object>
				{
					{ "ModifiedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
				};

				var updatedFields = new List<string>();

				if (!string.IsNullOrWhiteSpace(updateUserViewModel.FirstName))
				{
					updateDict["FirstName"] = updateUserViewModel.FirstName.Trim();
					updatedFields.Add("FirstName");
				}

				if (!string.IsNullOrWhiteSpace(updateUserViewModel.LastName))
				{
					updateDict["LastName"] = updateUserViewModel.LastName.Trim();
					updatedFields.Add("LastName");
				}

				if (!string.IsNullOrWhiteSpace(updateUserViewModel.ProfileImage))
				{
					updateDict["ProfileImage"] = updateUserViewModel.ProfileImage;
					updatedFields.Add("ProfileImage");
				}

				if (!string.IsNullOrWhiteSpace(updateUserViewModel.HashPassword))
				{
					// TODO: Hash password before storing
					updateDict["HashPassword"] = updateUserViewModel.HashPassword;
					updatedFields.Add("Password");
				}

				// Users CANNOT update: Email, Role, IsActive, HasAccess, GuardianName

				// Check if anything was updated
				if (updateDict.Count == 1) // Only ModifiedDate
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "No fields to update",
						Status = "failed"
					};
				}

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				var whereClause = new KeyValuePair<string, object>("Id", updateUserViewModel.Id);
				await _commandRepositoryUser.UpdateTableColumnById(updateDict, whereClause);


				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Profile updated successfully",
					Status = "successful",
					Data = new
					{
						UserId = updateUserViewModel.Id,
						UpdatedFields = updatedFields.ToArray(),
						UpdatedAt = updateDict["ModifiedDate"]
					}
				};
			}
			catch (SqlException ex)
			{
				// Log exception
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Database error occurred while updating profile",
					Status = "failed"
				};
			}
			catch (Exception ex)
			{
				// Log exception
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while updating profile",
					Status = "failed"
				};
			}
		}

		/// <summary>
		/// Check if email already exists for another user
		/// </summary>
		private async Task<bool> CheckEmailExists(string email, Guid schoolId, Guid excludeUserId)
		{
			try
			{
				// Assuming you have a method to get user by email
				//var users = await _queryrepositoryUser.GetAll(schoolId);
				var query = "SELECT * FROM Users WHERE EmailAddress = @EmailAddress";
				var parameters = new Dictionary<string, object>
					{
						{ "EmailAddress", email }
					};

				var user = await _queryrepositoryUser.SelectByColumns(query, parameters);

				//var existingUser = users.FirstOrDefault(u =>
				//	u.EmailAddress?.ToLower() == email.Trim().ToLower() &&
				//	u.Id != excludeUserId);

				return user != null;
			}
			catch (Exception ex)
			{
				// Log exception
				// _logger.LogError(ex, "Error checking email existence: {Email}", email);

				// Return true to be safe (prevent duplicate)
				return true;
			}
		}

		public async Task<BaseResponse> GetUsersByRole(AuthenticatedUserClaims userClaims,int? roleId,int pageNumber,int pageSize)
		{
			try
			{
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId format",
						Status = "failed"
					};
				}

				if (pageNumber < 1) pageNumber = 1;
				if (pageSize < 1 || pageSize > 100) pageSize = 50;

				string query;
				if (roleId.HasValue && roleId.Value >= 0)
				{
					query = $@"
                    SELECT * FROM Users 
                    WHERE SchoolId = '{schoolId}' 
                    AND RoleId = {roleId.Value} 
                    ORDER BY FirstName, LastName";
				}
				else
				{
					query = $@"
                    SELECT * FROM Users 
                    WHERE SchoolId = '{schoolId}' 
                    ORDER BY FirstName, LastName";
				}

				var allUsers = await _queryrepositoryUser.GetByQuery(query);
				var usersList = allUsers.Where(u => u != null).ToList();

				// Pagination
				var totalCount = usersList.Count;
				var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

				var paginatedUsers = usersList
					.Skip((pageNumber - 1) * pageSize)
					.Take(pageSize)
					.ToList();

				var userDtos = paginatedUsers.Select(u => new UserDto
				{
					Id = u!.Id,
					FirstName = u.FirstName,
					LastName = u.LastName,
					UserName = u.UserName,
					EmailAddress = u.EmailAddress,
					RoleId = u.RoleId,
					RoleName = ((UserRole)u.RoleId).ToString(),
					IsActive = u.IsActive,
					HasAccess = u.HasAccess,
					ProfileImage = u.ProfileImage,
					GuardianName = u.GuardianName,
					CreatedDate = u.CreationDate,
					ModifiedDate = u.ModifiedDate
				}).ToList();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Users retrieved successfully",
					Status = "successful",
					Data = new UsersListData
					{
						Users = userDtos,
						TotalCount = totalCount,
						PageNumber = pageNumber,
						PageSize = pageSize,
						TotalPages = totalPages,
						HasPreviousPage = pageNumber > 1,
						HasNextPage = pageNumber < totalPages,
						RoleFilter = roleId.HasValue ? ((UserRole)roleId.Value).ToString() : "All"
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching users by role");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching users",
					Status = "failed"
				};
			}
		}

		public async Task<BaseResponse> GetTeachers(AuthenticatedUserClaims userClaims,int pageNumber,int pageSize)
		{
			try
			{
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId",
						Status = "failed"
					};
				}

				if (pageNumber < 1) pageNumber = 1;
				if (pageSize < 1 || pageSize > 100) pageSize = 50;

				// ✅ Use GetByQuery with IN clause
				var query = $@"
                SELECT * FROM Users 
                WHERE SchoolId = '{schoolId}' 
                AND RoleId IN (1, 4)
                ORDER BY FirstName, LastName";

				var allTeachers = await _queryrepositoryUser.GetByQuery(query);
				var teachersList = allTeachers.Where(u => u != null).ToList();

				var totalCount = teachersList.Count;
				var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

				var paginatedTeachers = teachersList
					.Skip((pageNumber - 1) * pageSize)
					.Take(pageSize)
					.ToList();

				var teacherDtos = paginatedTeachers.Select(u => new UserDto
				{
					Id = u!.Id,
					FirstName = u.FirstName,
					LastName = u.LastName,
					UserName = u.UserName,
					EmailAddress = u.EmailAddress,
					RoleId = u.RoleId,
					RoleName = ((UserRole)u.RoleId).ToString(),
					IsActive = u.IsActive,
					HasAccess = u.HasAccess,
					ProfileImage = u.ProfileImage,
					CreatedDate = u.CreationDate,
					ModifiedDate = u.ModifiedDate
				}).ToList();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Teachers retrieved successfully",
					Status = "successful",
					Data = new UsersListData
					{
						Users = teacherDtos,
						TotalCount = totalCount,
						PageNumber = pageNumber,
						PageSize = pageSize,
						TotalPages = totalPages,
						HasPreviousPage = pageNumber > 1,
						HasNextPage = pageNumber < totalPages,
						RoleFilter = "Teachers"
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching teachers");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching teachers",
					Status = "failed"
				};
			}
		}

		public async Task<BaseResponse> GetAdministrators(
			AuthenticatedUserClaims userClaims,
			int pageNumber,
			int pageSize)
		{
			try
			{
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId",
						Status = "failed"
					};
				}

				if (pageNumber < 1) pageNumber = 1;
				if (pageSize < 1 || pageSize > 100) pageSize = 50;

				// ✅ Use GetByQuery
				var query = $@"
                SELECT * FROM Users 
                WHERE SchoolId = '{schoolId}' 
                AND RoleId IN (2, 3)
                ORDER BY FirstName, LastName";

				var allAdmins = await _queryrepositoryUser.GetByQuery(query);
				var adminsList = allAdmins.Where(u => u != null).ToList();

				var totalCount = adminsList.Count;
				var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

				var paginatedAdmins = adminsList
					.Skip((pageNumber - 1) * pageSize)
					.Take(pageSize)
					.ToList();

				var adminDtos = paginatedAdmins.Select(u => new UserDto
				{
					Id = u!.Id,
					FirstName = u.FirstName,
					LastName = u.LastName,
					UserName = u.UserName,
					EmailAddress = u.EmailAddress,
					RoleId = u.RoleId,
					RoleName = ((UserRole)u.RoleId).ToString(),
					IsActive = u.IsActive,
					HasAccess = u.HasAccess,
					ProfileImage = u.ProfileImage,
					CreatedDate = u.CreationDate,
					ModifiedDate = u.ModifiedDate
				}).ToList();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Administrators retrieved successfully",
					Status = "successful",
					Data = new UsersListData
					{
						Users = adminDtos,
						TotalCount = totalCount,
						PageNumber = pageNumber,
						PageSize = pageSize,
						TotalPages = totalPages,
						HasPreviousPage = pageNumber > 1,
						HasNextPage = pageNumber < totalPages,
						RoleFilter = "Administrators"
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching administrators");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching administrators",
					Status = "failed"
				};
			}
		}

		// TechHub.Service.Service/UserService.cs

		public async Task<BaseResponse> GetUserById(Guid userId, AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			//using (LogContext.PushProperty("TenantId", userClaims.TenantIdentifier))
			{
				try
				{
					// Validation 1: Check if user claims are valid
					if (string.IsNullOrEmpty(userClaims?.SchoolId) || string.IsNullOrEmpty(userClaims?.UserId))
					{
						_logger.Warning("Get user request with invalid claims");

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "User authentication information is missing",
							Status = "failed"
						};
					}

					// Validation 2: Parse SchoolId
					if (!Guid.TryParse(userClaims.SchoolId, out var claimSchoolId))
					{
						_logger.Warning("Invalid SchoolId format in token - SchoolId: {SchoolId}", userClaims.SchoolId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format in token",
							Status = "failed"
						};
					}

					_logger.Information("Fetching user by ID - TargetUserId: {TargetUserId}", userId);

					// Get user from repository
					var user = await _queryrepositoryUser.Get(userId);

					if (user == null)
					{
						_logger.Warning("User not found - TargetUserId: {TargetUserId}", userId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "User not found",
							Status = "failed"
						};
					}

					// Validation 3: Multi-tenancy check - Ensure user belongs to same school
					if (user.SchoolId != claimSchoolId)
					{
						_logger.Warning(
							"Unauthorized access attempt - User {RequestedBy} tried to access user {TargetUserId} from different school",
							userClaims.UserId,
							userId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You cannot access users from a different school",
							Status = "failed"
						};
					}

					_logger.Information(
						"User retrieved successfully - TargetUserId: {TargetUserId}, Email: {Email}",
						user.Id,
						user.EmailAddress);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "User retrieved successfully",
						Status = "successful",
						Data = new UserDto
						{
							Id = user.Id,
							FirstName = user.FirstName,
							LastName = user.LastName,
							UserName = user.UserName,
							EmailAddress = user.EmailAddress,
							RoleId = user.RoleId,
							RoleName = ((UserRole)user.RoleId).ToString(),
							IsActive = user.IsActive,
							HasAccess = user.HasAccess,
							ProfileImage = user.ProfileImage,
							GuardianName = user.GuardianName,
							CreatedDate = user.CreationDate,
							ModifiedDate = user.ModifiedDate
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error fetching user by ID - TargetUserId: {TargetUserId}", userId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching user",
						Status = "failed"
					};
				}
			}
		}

		// ✅ Email exists check using generic methods
		//private async Task<bool> CheckEmailExists(string email, Guid schoolId, Guid excludeUserId)
		//{
		//	try
		//	{
		//		var query = $@"
		//              SELECT COUNT(1) 
		//              FROM Users 
		//              WHERE LOWER(EmailAddress) = '{email.ToLower()}' 
		//              AND SchoolId = '{schoolId}' 
		//              AND Id != '{excludeUserId}'";

		//		var count = await _queryRepository.CountAsync(query, new Dictionary<string, object>());
		//		return count > 0;
		//	}
		//	catch (Exception ex)
		//	{
		//		_logger.LogError(ex, "Error checking email existence");
		//		return true; // Safe default
		//	}
		//}


		/// <summary>
		/// Validate student exists, belongs to school, and is active
		/// </summary>
		private async Task<(bool IsValid, string Message, string ResponseCode)> ValidateStudent(Guid studentId, Guid schoolId)
		{
			try
			{
				var query = "SELECT * FROM Users WHERE Id = @StudentId AND SchoolId = @SchoolId AND RoleId = @RoleId";
				var parameters = new Dictionary<string, object>
				{
					{ "StudentId", studentId },
					{ "SchoolId", schoolId },
					{ "RoleId", (int)UserRole.Student }
				};

				var student = await _queryrepositoryUser.SelectByColumns(query, parameters);

				if (student == null)
				{
					return (false, "Student not found or does not belong to your school", ResponseCode.NotFound);
				}

				if (!student.IsActive)
				{
					return (false, "Student account is not active", ResponseCode.Forbidden);
				}

				return (true, string.Empty, ResponseCode.successful);
			}
			catch
			{
				return (false, "Error validating student", ResponseCode.ErrorOccured);
			}
		}

		/// <summary>
		/// Validate classroom exists, belongs to school, and is active
		/// </summary>
		private async Task<(bool IsValid, string Message, string ResponseCode)> ValidateClassroom(Guid classroomId, Guid schoolId)
		{
			try
			{
				var query = "SELECT * FROM Classroom WHERE Id = @ClassroomId AND SchoolId = @SchoolId";
				var parameters = new Dictionary<string, object>
				{
					{ "ClassroomId", classroomId },
					{ "SchoolId", schoolId }
				};

				var classroom = await _classroomQueryRespository.SelectByColumns(query, parameters);

				if (classroom == null)
				{
					return (false, "Classroom not found or does not belong to your school", ResponseCode.NotFound);
				}

				if (!classroom.IsActive)
				{
					return (false, "Classroom is not active", ResponseCode.Forbidden);
				}

				return (true, string.Empty, ResponseCode.successful);
			}
			catch
			{
				return (false, "Error validating classroom", ResponseCode.ErrorOccured);
			}
		}

		/// <summary>
		/// Check if student is already registered in the classroom
		/// </summary>
		private async Task<bool> CheckExistingRegistration(Guid studentId, Guid classroomId)
		{
			try
			{
				var query = "SELECT COUNT(*) FROM StudentCourses WHERE StudentId = @StudentId AND ClassroomId = @ClassroomId AND Status = @Status";
				var parameters = new Dictionary<string, object>
		{
			{ "StudentId", studentId },
			{ "ClassroomId", classroomId },
			{ "Status", (int)StudentClassroomStatus.Active }
		};

				var count = await _studentCoursesQueryRespository.CountAsync(query, parameters);
				return count > 0;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Get student's current active classroom (if any)
		/// </summary>
		private async Task<Guid?> GetStudentActiveClassroom(Guid studentId)
		{
			try
			{
				var query = "SELECT ClassroomId FROM StudentCourses WHERE StudentId = @StudentId AND Status = @Status";
				var parameters = new Dictionary<string, object>
		{
			{ "StudentId", studentId },
			{ "Status", (int)StudentClassroomStatus.Active }
		};

				var registration = await _studentCoursesQueryRespository.SelectByColumns(query, parameters);
				return registration?.ClassroomId;
			}
			catch
			{
				return null;
			}
		}

		private string GenerateJwtToken(Users user, string tenantId, SchoolResponseModel schoolInfo)
		{
			var claims = new List<Claim>
			{
				new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
				new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
				new Claim(ClaimTypes.Email, user.EmailAddress ?? string.Empty),
				new Claim(ClaimTypes.Role, user.RoleId.ToString()),
				new Claim("SchoolId", user.SchoolId.ToString()),
				new Claim("TenantId", tenantId ?? string.Empty),
				new Claim("SchoolName", schoolInfo?.SchoolName ?? string.Empty),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
				new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
			};

			if (!string.IsNullOrEmpty(user.FirstName))
			{
				claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
			}

			if (!string.IsNullOrEmpty(user.LastName))
			{
				claims.Add(new Claim(ClaimTypes.Surname, user.LastName));
			}

			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]));
			var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
			var expires = DateTime.UtcNow.AddHours(1); // Token valid for 1 hour

			var token = new JwtSecurityToken(
				issuer: _configuration["Jwt:Issuer"],
				audience: _configuration["Jwt:Audience"],
				claims: claims,
				expires: expires,
				signingCredentials: credentials
			);

			return new JwtSecurityTokenHandler().WriteToken(token);
		}




		/// <summary>
		/// Validate role-specific requirements
		/// </summary>
		private (bool IsValid, string ErrorMessage) ValidateUserRoleRequirements(UserViewModel userViewModel)
		{
			switch (userViewModel.Role)
			{
				case UserRole.Student:
					if (!userViewModel.UserClassroomsId.Any())
					{
						return (false, "Students must be assigned to a classroom");
					}
					if (userViewModel.UserClassroomsId.Count > 1)
					{
						return (false, "Students can only be assigned to one classroom");
					}
					// Students can have 0 or more minor subjects (optional)
					break;

				case UserRole.SubjectTeacher:
				case UserRole.HeadTeacher:
					if (!userViewModel.UserClassroomsId.Any())
					{
						return (false, "Teachers must be assigned to at least one classroom");
					}
					if (!userViewModel.UserSubjects.Any())
					{
						return (false, "Teachers must be assigned to at least one subject");
					}
					break;

				case UserRole.Administrator:
				case UserRole.SuperAdministrator:
					// Admins don't require classrooms or subjects
					break;

				default:
					return (false, $"Invalid user role: {userViewModel.Role}");
			}

			return (true, string.Empty);
		}

		/// <summary>
		/// Check if user already exists by username or email
		/// </summary>
		private async Task<(bool Exists, string Message)> CheckUserExists(string userName, string email, Guid schoolId)
		{
			try
			{
				var query = "SELECT COUNT(*) FROM Users WHERE (LOWER(UserName) = @UserName OR LOWER(Email) = @Email) AND SchoolId = @SchoolId";
				var parameters = new Dictionary<string, object>
				{
					{ "UserName", userName.Trim().ToLower() },
					{ "Email", email.Trim().ToLower() },
					{ "SchoolId", schoolId }
				};

				var count = await _queryrepositoryUser.CountAsync(query, parameters);

				if (count > 0)
				{
					return (true, "User with this username or email already exists in your school");
				}

				return (false, string.Empty);
			}
			catch
			{
				return (false, string.Empty);
			}
		}

		/// <summary>
		/// Create student-specific associations (classroom and minor subjects)
		/// </summary>
		private async Task CreateStudentAssociations(
			IDbTransactionScope scope,
			Guid studentId,
			UserViewModel userViewModel,
			Guid schoolId,
			Guid createdBy)
		{
			var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

			// Create student-classroom association
			var studentClassroom = new Dictionary<string, object>
			{
				{ "Id", Guid.NewGuid() },
				{ "CreationDate", now },
				{ "ModifiedDate", now },
				{ "StudentId", studentId },
				{ "ClassroomId", userViewModel.UserClassroomsId[0] },
				{ "SchoolId", schoolId },
				{ "IsActive", true },
				{ "CreatedBy", createdBy }
			};

			await _commandRepositoryStudentClassroom.Create(scope.Transaction, scope.Connection, studentClassroom);

			if (userViewModel.UserSubjects.Any())
			{
				var studentSubjects = userViewModel.UserSubjects.Select(subjectId => new Dictionary<string, object>
				{
					{ "Id", Guid.NewGuid() },
					{ "CreationDate", now },
					{ "ModifiedDate", now },
					{ "StudentId", studentId },
					{ "SubjectId", subjectId },
					{ "SchoolId", schoolId },
					{ "IsActive", true },
					{ "CreatedBy", createdBy }
				}).ToList();

				await _commandRepositoryMinorSubject.CreateBatchAsync(scope.Transaction, scope.Connection, studentSubjects);
			}
		}

		/// <summary>
		/// Create teacher-specific associations (classrooms and subjects)
		/// </summary>
		private async Task CreateTeacherAssociations(
			IDbTransactionScope scope,
			Guid teacherId,
			UserViewModel userViewModel,
			Guid schoolId,
			Guid createdBy)
		{
			var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

			// Create teacher-classroom associations
			if (userViewModel.UserClassroomsId.Any())
			{
				var teacherClassrooms = userViewModel.UserClassroomsId.Select(classroomId => new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "TeacherId", teacherId },
						{ "ClassroomId", classroomId },
						{ "SchoolId", schoolId },
						{ "IsActive", true },
						{ "CreatedBy", createdBy }
					}).ToList();

				await _commandRepositoryTeacherClassroom.CreateBatchAsync(scope.Transaction, scope.Connection, teacherClassrooms);
			}

			// Create teacher-subject associations
			if (userViewModel.UserSubjects.Any())
			{
				var teacherSubjects = userViewModel.UserSubjects.Select(subjectId => new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "TeacherId", teacherId },
						{ "SubjectId", subjectId },
						{ "SchoolId", schoolId },
						{ "IsActive", true },
						{ "CreatedBy", createdBy }
					}).ToList();

				await _commandRepositoryTeacherSubject.CreateBatchAsync(scope.Transaction, scope.Connection, teacherSubjects);
			}
		}

		public Task<BaseResponse> GetStudents(AuthenticatedUserClaims? claims, int pageNumber, int pageSize)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Hash password using BCrypt
		/// </summary>
		//private string HashPassword(string password)
		//{
		//	return BCrypt.Net.BCrypt.HashPassword(password);
		//}
	}

	internal class UsersListData
	{
		public object Users { get; set; }
		public object TotalCount { get; set; }
		public int PageNumber { get; set; }
		public int PageSize { get; set; }
		public int TotalPages { get; set; }
		public bool HasPreviousPage { get; set; }
		public bool HasNextPage { get; set; }
		public string RoleFilter { get; set; }
	}

	internal class UserDto
	{
		public object Id { get; set; }
		public object FirstName { get; set; }
		public object LastName { get; set; }
		public object UserName { get; set; }
		public object EmailAddress { get; set; }
		public object RoleId { get; set; }
		public string RoleName { get; set; }
		public object IsActive { get; set; }
		public object HasAccess { get; set; }
		public object ProfileImage { get; set; }
		public object GuardianName { get; set; }
		public object CreatedDate { get; set; }
		public object ModifiedDate { get; set; }
	}
}
