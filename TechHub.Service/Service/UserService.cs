using AutoMapper;
using Azure;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using TechHub.Core;
using TechHub.Core.Constant;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Helper;
using TechHub.Core.Model;
using TechHub.Core.Models;
using TechHub.Core.ResponseModel;
using TechHub.Core.Utilities;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.Platform;
using TechHub.Core.ViewModel.school;
using TechHub.Core.ViewModel.Users;
using TechHub.Service.Extension;
using TechHub.Service.Interface;
using TechHub.Service.Service;
using TechHub.Service.Service.DatabaseService;
using TechHub.Service.util;
using TechhubMS;
using TechhubMS.util;
using static System.Net.Mime.MediaTypeNames;

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
		private readonly ICommandRespository<AdminPermissions> _adminPermissionsCommandRepository;
		private readonly ICommandRespository<RefreshTokens> _commandRepoRefreshToken;

		private readonly IQueryRepository<School> _queryrepositorySchool;
		private readonly IQueryRepository<SchoolCode> _schCodeQueryRespository;
		private readonly IQueryRepository<TenantInfo> _tenantQueryRespository;
		private readonly IQueryRepository<Classroom> _classroomQueryRespository;
		private readonly IQueryRepository<StudentCourses> _studentCoursesQueryRespository;
		private readonly IQueryRepository<AdminPermissions> _adminPermissionsQueryRespository;
		private readonly IQueryRepository<RefreshTokens> _queryRepoRefreshToken;
		private readonly IQueryRepository<ApprovalRequests> _queryApprovalRequests;
		private readonly IQueryRepository<TeacherClassroom> _teacherClassroomQueryRespository;
		private readonly IQueryRepository<TeacherSubject> _teacherSubjectQueryRespository;
		private readonly IQueryRepository<Subjects> _subjectQueryRespository;
		private readonly IQueryRepository<StudentMinorSubject> _studentMinorSubjectQueryRespository;
		private readonly IQueryRepository<ClassroomSubject> _classroomSubjectQueryRespository;






		private readonly JwtTokenGenerator _jwtTokenGenerator;
		private readonly IConfiguration _configuration;
		private readonly IMapper _mapper;
		private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;
		private readonly ILogger _logger;
		private readonly IEmailService _emailService;
		private readonly ITenantService _tenantService;
		private readonly string? _connString;

		public UserService(IQueryRepository<LoginHistory> queryRepositoryLoginHistory, IQueryRepository<Users> queryrepositoryUser,
			ICommandRespository<LoginHistory> commandRepositoryLoginHistory, ICommandRespository<Users> commandRepositoryUser,
			IQueryRepository<School> queryrepositorySchool, IQueryRepository<SchoolCode> schCodeQueryRespository, ICommandRespository<StudentCourses> studentCourseCommandRepository,
			ICommandRespository<Classroom> classroomCommandRespository, IQueryRepository<Classroom> classroomQueryRespository,
			IDbTransactionScopeFactory dbTransactionScopeFactory, IConfiguration configuration, ICommandRespository<StudentClassroom> commandRepositoryStudentClassroom, ICommandRespository<TeacherClassroom> commandRepositoryTeacherClassroom,
			ICommandRespository<TeacherSubject> commandRepositoryTeacherSubject, IQueryRepository<TenantInfo> tenantQueryRespository,
			ICommandRespository<StudentMinorSubject> commandRepositoryMinorSubject, ICommandRespository<RefreshTokens> commandRepoRefreshToken, IQueryRepository<ApprovalRequests> queryApprovalRequests,
			ICommandRespository<AdminPermissions> adminPermissionsCommandRepository, IQueryRepository<RefreshTokens> queryRepoRefreshToken,ITenantService tenantService, 
			IQueryRepository<TeacherClassroom> teacherClassroomQueryRespository, IQueryRepository<TeacherSubject> teacherSubjectQueryRespository,
			IQueryRepository<AdminPermissions> adminPermissionsQueryRespository, IQueryRepository<Subjects> subjectQueryRespository, IQueryRepository<ClassroomSubject> classroomSubjectQueryRespository,
			IQueryRepository<StudentMinorSubject> studentMinorSubjectQueryRespository,
			IMapper mapper, ILogger logger, IEmailService emailService, JwtTokenGenerator jwtTokenGenerator)
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

			_tenantQueryRespository = tenantQueryRespository;
			_commandRepositoryMinorSubject = commandRepositoryMinorSubject;
			_classroomCommandRespository = classroomCommandRespository;
			_classroomQueryRespository = classroomQueryRespository;
			_adminPermissionsCommandRepository = adminPermissionsCommandRepository;
			_adminPermissionsQueryRespository = adminPermissionsQueryRespository;
			_commandRepoRefreshToken = commandRepoRefreshToken;
			_queryRepoRefreshToken = queryRepoRefreshToken;
			_queryApprovalRequests = queryApprovalRequests;
			_teacherClassroomQueryRespository = teacherClassroomQueryRespository;
			_teacherSubjectQueryRespository = teacherSubjectQueryRespository;
			_subjectQueryRespository = subjectQueryRespository;
			_studentMinorSubjectQueryRespository = studentMinorSubjectQueryRespository;
			_classroomSubjectQueryRespository = classroomSubjectQueryRespository;
			

			_emailService = emailService;
			_logger = logger;
			_configuration = configuration;
			_tenantService = tenantService;
			_jwtTokenGenerator = jwtTokenGenerator;

			_mapper = mapper;
			_connString = _configuration.GetConnectionString("DbConnectionString") ?? throw new ArgumentNullException("Db Config is null");

		}


		public async Task<BaseResponse> LoginUser(LoginViewModel loginViewModel, TenantInfo? tenant)
		{
			try
			{
				// ===== VALIDATION =====

				if (loginViewModel is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Login credentials cannot be empty",
						Status = "failed"
					};
				}

				if (tenant is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid tenant information",
						Status = "failed"
					};
				}

				if (string.IsNullOrWhiteSpace(loginViewModel.Username) ||
					string.IsNullOrWhiteSpace(loginViewModel.HashPassword))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Username and password are required",
						Status = "failed"
					};
				}

				//if (string.IsNullOrWhiteSpace(loginViewModel.Inst))
				//{
				//	return new BaseResponse
				//	{
				//		ResponseCode = ResponseCode.BadRequest,
				//		ResponseMessage = "Institution code is required",
				//		Status = "failed"
				//	};
				//}

				// ===== VALIDATE INSTITUTION CODE AGAINST DB =====

				//var schoolCode = await  _tenantQueryRespository.GetByPropertyName("Identifier", loginViewModel.Inst, DatabaseTarget.Core);
				//if (schoolCode is null)
				//{
				//	_logger.Warning(
				//		"Login attempt with invalid institution code - Inst: {Inst}",
				//		loginViewModel.Inst);
				//	return new BaseResponse
				//	{
				//		ResponseCode = ResponseCode.BadRequest,
				//		ResponseMessage = "Invalid institution code",
				//		Status = "failed"
				//	};
				//}

				// ===== FETCH USER =====

				var loginUserInput = new Dictionary<string, object>
				{
					{ "UserName",   loginViewModel.Username },
					{ "SchoolId", tenant.SchoolId }
				};

				var user = await _queryrepositoryUser.GetBy(loginUserInput);
				if (user is null)
				{
					_logger.Warning(
						"Login attempt for non-existent user - Username: {Username}, Inst: {Inst}",
						loginViewModel.Username, loginViewModel.Inst);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Incorrect credentials",
						Status = "failed"
					};
				}

				// ===== FIRST TIME LOGIN =====

				var lastThreeLogins = await LastLoginHistorys(user.Id);
				if (!lastThreeLogins.Any())
				{
					_logger.Information(
						"First time login - UserId: {UserId}", user.Id);

					var loginHistoryFirst = new LoginHistory
					{
						UserId = user.Id,
						RoleId = user.RoleId,
						PasswordFailed = false
					};

					await _commandRepositoryLoginHistory.Create(loginHistoryFirst);

					return new UserLoginResponse
					{
						SchoolInfo = new SchoolResponseModel { Id = user.SchoolId },
						FirstTimeLogin = true,
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "First time login",
						Status = "successful"
					};
				}

				// ===== ACCOUNT LOCK CHECK (before password verification) =====

				var consecutiveFailures = lastThreeLogins
					.Where(c => c.PasswordFailed == true)
					.ToList();

				if (consecutiveFailures.Count >= 3)
				{
					// Ensure account is locked in DB if not already
					if (user.IsActive)
					{
						await _commandRepositoryUser.UpdateTableColumnById(
							nameof(user.IsActive), nameof(user.Id), false, user.Id);

						_logger.Warning(
							"Account locked due to 3 consecutive failed logins - UserId: {UserId}",
							user.Id);
					}

					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "This account is locked, contact your administrator",
						Status = "failed"
					};
				}

				if (!user.IsActive)
				{
					_logger.Warning(
						"Login attempt on inactive account - UserId: {UserId}", user.Id);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "This account is locked, contact your administrator",
						Status = "failed"
					};
				}

				// ===== PASSWORD VERIFICATION =====

				var loginHistory = new LoginHistory
				{
					UserId = user.Id,
					RoleId = user.RoleId,
					PasswordFailed = false
				};

				if (loginViewModel.HashPassword != user.HashPassword)
				{
					loginHistory.PasswordFailed = true;
					await _commandRepositoryLoginHistory.Create(loginHistory);

					_logger.Warning(
						"Failed login attempt - UserId: {UserId}, Username: {Username}",
						user.Id, user.UserName);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Incorrect credentials",
						Status = "failed"
					};
				}

				// ===== SUCCESSFUL LOGIN — issue tokens atomically =====

				var schInfo = await _queryrepositorySchool.Get(tenant.SchoolId);
				var mappedSchInfo = _mapper.Map<SchoolResponseModel>(schInfo);
				//var accessToken = GenerateJwtToken(user, tenant.SchoolId.ToString(), mappedSchInfo);
				var accessToken = _jwtTokenGenerator.Generate(user, tenant.SchoolId.ToString(), mappedSchInfo);

				string refreshTokenValue;

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				try
				{
					// Write login history
					await _commandRepositoryLoginHistory.Create(
						scope.Transaction, scope.Connection, loginHistory);

					// Issue and store refresh token
					refreshTokenValue = await GenerateAndStoreRefreshToken(scope.Transaction, scope.Connection,user.Id, user.SchoolId);

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Failed to commit login transaction - UserId: {UserId}", user.Id);
					try { await scope.RollbackAsync(); }
					catch (Exception rbEx)
					{
						_logger.Error(rbEx,
							"Rollback failed during login - UserId: {UserId}", user.Id);
					}
					throw;
				}

				_logger.Information(
					"Login successful - UserId: {UserId}, Username: {Username}, Role: {RoleId}",
					user.Id, user.UserName, user.RoleId);

				return new UserLoginResponse
				{
					FirstName = user.FirstName,
					LastName = user.LastName,
					RoleId = user.RoleId,
					Id = user.Id,
					EmailAddress = user.EmailAddress,
					IsActive = user.IsActive,
					SchoolInfo = mappedSchInfo,
					Token = accessToken,
					RefreshToken = refreshTokenValue,          
					TokenExpiresIn = 3600,
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Login successful",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Unexpected error during login - Username: {Username}",
					loginViewModel?.Username);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Server error occurred",
					Status = "failed"
				};
			}
		}

		private async Task<string> GenerateAndStoreRefreshToken(SqlTransaction transaction, SqlConnection connection,Guid userId, Guid schoolId)
		{
			var tokenValue = GenerateSecureRefreshToken();
			var expiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

			var refreshToken = new RefreshTokens
			{
				Id = Guid.NewGuid(),
				UserId = userId,
				SchoolId = schoolId,
				Token = tokenValue,
				ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
				CreatedAt = DateTime.UtcNow,
				IsRevoked = false
			};

			await _commandRepoRefreshToken.Create(transaction, connection, refreshToken);
			return tokenValue;
		}

		//private static string GenerateSecureRefreshToken()
		//{
		//	var bytes = new byte[64];
		//	RandomNumberGenerator.Fill(bytes);
		//	return Convert.ToBase64String(bytes);
		//}
		//6197fe01-c562-496c-a271-cd62bf067525 --- headTeacher
		//55b0a703-3fc0-45ce-aa5f-72d94023aa4a  -- teacher
		public async Task<BaseResponse> CreateUser(UserViewModelV2 userViewModel, AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			{
				try
				{
					// ===== VALIDATION SECTION =====
					if (userViewModel is null)
					{
						_logger.Warning("user details cannot be null");
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
						_logger.Warning("Create User called with missing authentication information");
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
						_logger.Warning("Invalid SchoolId format in token - SchoolId: {SchoolId}", userClaims.SchoolId);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format in token",
							Status = "failed"
						};
					}

					if (!Guid.TryParse(userClaims.UserId, out var createdBy))
					{
						_logger.Warning("Invalid UserId format in token - UserId: {UserId}", userClaims.UserId);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId format in token",
							Status = "failed"
						};
					}

					if (!Enum.TryParse<UserRole>(userClaims.Role, ignoreCase: true, out UserRole userRole))
					{
						_logger.Warning("Invalid role format in token - Role: {Role}", userClaims.Role);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid role format in token",
							Status = "failed"
						};
					}

					// Validation 4: Check user role (must be Admin or SuperAdmin)
					if (userRole != UserRole.Administrator && userRole != UserRole.SuperAdministrator)
					{
						_logger.Warning(
							"Unauthorized user creation attempt - UserId: {UserId}, Role: {Role}",
							createdBy,
							userRole.ToString());
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You are not authorized to create users",
							Status = "failed"
						};
					}

					// Validation 5: Check creator exists and is active
					var creator = await _queryrepositoryUser.Get(createdBy);
					if (creator is null)
					{
						_logger.Warning("Creator user does not exist - UserId: {UserId}", createdBy);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Creator user does not exist",
							Status = "failed"
						};
					}

					if (!creator.IsActive)
					{
						_logger.Warning("Inactive user attempted to create user - UserId: {UserId}", createdBy);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Your account is not active",
							Status = "failed"
						};
					}

					if ((userViewModel.Role == UserRole.SubjectTeacher || userViewModel.Role == UserRole.ClassTeacher) && userViewModel.LineManagerId.HasValue)
					{
						var lineManager = await _queryrepositoryUser.Get(userViewModel.LineManagerId.Value);
						if (lineManager is null)
						{
							_logger.Warning(
								"Line manager does not exist - LineManagerId: {LineManagerId}",
								userViewModel.LineManagerId.Value);
							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = "Assigned line manager does not exist",
								Status = "failed"
							};
						}

						if (!lineManager.IsActive)
						{
							_logger.Warning(
								"Line manager is inactive - LineManagerId: {LineManagerId}",
								userViewModel.LineManagerId.Value);
							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = "Assigned line manager is not active",
								Status = "failed"
							};
						}

						// Ensure line manager is actually a HeadTeacher
						if (lineManager.RoleId != (int)UserRole.HeadTeacher)
						{
							_logger.Warning(
								"Invalid line manager role - LineManagerId: {LineManagerId}, Role: {Role}",
								userViewModel.LineManagerId.Value,
								lineManager.RoleId);
							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = "Line manager must be a Head Teacher",
								Status = "failed"
							};
						}

						// Ensure line manager belongs to same school
						if (lineManager.SchoolId != schoolId)
						{
							_logger.Warning(
								"Line manager belongs to different school - LineManagerId: {LineManagerId}",
								userViewModel.LineManagerId.Value);
							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = "Line manager must belong to the same school",
								Status = "failed"
							};
						}
					}

					// Validation 6: Check Admin permission
					if (userRole == UserRole.Administrator)
					{
						var hasPermission = await this.HasPermission(createdBy, schoolId, AdminPermission.CreateUsers);
						if (!hasPermission)
						{
							_logger.Warning(
								"Admin lacks CreateUsers permission - AdminId: {AdminId}, TargetRole: {TargetRole}",
								createdBy,
								userViewModel.Role.ToString());
							return new BaseResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = "You don't have permission to create users. Contact your SuperAdministrator.",
								Status = "failed"
							};
						}

						_logger.Information("Admin has Create Users permission - AdminId: {AdminId}", createdBy);
					}

					_logger.Information(
						"Creating user - TargetRole: {Role}, CreatedBy: {CreatedBy}",
						userViewModel.Role.ToString(),
						createdBy);

					// Validation 7: Role-specific validations
					var roleValidation = ValidateUserRoleRequirements(userViewModel);
					if (!roleValidation.IsValid)
					{
						_logger.Warning(
							"Role validation failed - Role: {Role}, Error: {Error}",
							userViewModel.Role.ToString(),
							roleValidation.ErrorMessage);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = roleValidation.ErrorMessage,
							Status = "failed"
						};
					}

					// Validation 8: Check if user already exists
					var existingUser = userViewModel.Role == (int)UserRole.Student ? await CheckStudentExists(userViewModel.UserName, userViewModel.EmailAddress, schoolId) :
						await CheckUserExists(userViewModel.UserName, userViewModel.EmailAddress, schoolId);
					if (existingUser.Exists)
					{
						_logger.Warning(
							"User already exists - Username: {Username}, Email: {Email}",
							userViewModel.UserName,
							userViewModel.EmailAddress);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Conflict,
							ResponseMessage = existingUser.Message,
							Status = "failed"
						};
					}

					// ===== USER CREATION SECTION =====

					var tempPassword = GenerateTempPassword();

					var newUser = new Users
					{
						Id = Guid.NewGuid(),
						FirstName = userViewModel.FirstName.Trim(),
						LastName = userViewModel.LastName.Trim(),
						UserName = userViewModel.UserName.Trim(),
						EmailAddress = userViewModel.EmailAddress.Trim().ToLower(),
						HashPassword = userViewModel.Role == (int)UserRole.Student ? userViewModel.HashPassword : HashPassword(tempPassword),
						RoleId = (int)userViewModel.Role,
						SchoolId = schoolId,
						CreatedBy = createdBy,
						IsActive = true, 
						DOB = userViewModel.DOB,
						LineManagerId = userViewModel.LineManagerId
						
					};

					var userDict = new Dictionary<string, object>
					{
						{ "Id",             newUser.Id },
						{ "FirstName",      newUser.FirstName },
						{ "LastName",       newUser.LastName },
						{ "UserName",       newUser.UserName },
						{ "EmailAddress",   newUser.EmailAddress },
						{ "HashPassword",   newUser.HashPassword },
						{ "RoleId",         newUser.RoleId },
						{ "SchoolId",       newUser.SchoolId },
						{ "CreatedBy",      newUser.CreatedBy },
						{ "IsActive",       newUser.IsActive },
						{ "CreationDate",   newUser.CreationDate },
						{ "ModifiedDate",   newUser.ModifiedDate },
						{ "HasAccess",      false },
						{ "DOB",      newUser.DOB },
						{ "LineManagerId", newUser.LineManagerId.HasValue ? (object)newUser.LineManagerId.Value: DBNull.Value }

					};

					// ===== TRANSACTIONAL SECTION =====

					// Declared outside the inner try so they are accessible after commit
					Dictionary<string, string> placeholders = null;
					string emailTemplate = null;

					using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");

					try
					{
						await _commandRepositoryUser.Create(scope.Transaction, scope.Connection, userDict);

						switch (userViewModel.Role)
						{
							case UserRole.Student:
								await CreateStudentAssociations(scope, newUser.Id, userViewModel, schoolId, createdBy);
								break;

							case UserRole.SubjectTeacher:
							case UserRole.ClassTeacher:
								

							case UserRole.HeadTeacher:
							case UserRole.Administrator:
							case UserRole.SuperAdministrator:
								await CreateTeacherAssociations(scope, newUser.Id, userViewModel, schoolId, createdBy);
								break;

							default:
								_logger.Warning("Invalid user role - Role: {Role}", userViewModel.Role);
								await scope.RollbackAsync();
								return new BaseResponse
								{
									ResponseCode = ResponseCode.BadRequest,
									ResponseMessage = $"Invalid user role: {userViewModel.Role}",
									Status = "failed"
								};
						}

						// Build placeholders before commit so any failure here still triggers rollback
						var code = await GetSchoolCode(schoolId);
						placeholders = new Dictionary<string, string>
						{
							{ "@@Name",     $"{newUser.FirstName} {newUser.LastName}" },
							{ "@@UserName", newUser.UserName },
							{ "@@Password", tempPassword },
							{ "@@Link",     _configuration["App:BaseUrl"] + "/" + code }
						};

						await scope.CommitAsync();
					}
					catch (Exception ex)
					{
						_logger.Error(
							ex,
							"Rolling back transaction due to error during user creation - Username: {Username}",
							newUser.UserName);

						try
						{
							await scope.RollbackAsync();
						}
						catch (Exception rollbackEx)
						{
							// Log rollback failure but do not rethrow —
							// the original exception is what we surface to the outer handler
							_logger.Error(
								rollbackEx,
								"Rollback failed - Username: {Username}",
								newUser.UserName);
						}

						throw; // Bubble up to outer SqlException / Exception handlers
					}

					// ===== POST-COMMIT SECTION =====
					// Only reached when commit succeeded
					if(userViewModel.Role != (int)UserRole.Student)
					{
						emailTemplate = await _emailService.GetRenderedTemplate(
						(int)EmailTemplateKey.WelcomeUser, placeholders);

						if (emailTemplate != null)
						{
							// Fire-and-forget — email failure must never affect the success response
							_ = Task.Run(async () =>
								await _emailService.SendAsync(
									newUser.EmailAddress,
									$"{newUser.FirstName} {newUser.LastName}",
									"User Profiled",
									emailTemplate));

							_logger.Information(
								"User created successfully - UserId: {UserId}, Username: {Username}, Role: {Role}, CreatedBy: {CreatedBy}",
								newUser.Id,
								newUser.UserName,
								userViewModel.Role.ToString(),
								createdBy);
						}
					}
					

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
						SubjectsAssigned = userViewModel.UserSubjectClassrooms.Any()
							? userViewModel.UserSubjectClassrooms.Count
							: userViewModel.UserSubjects.Count
					}
					};
				}
				catch (SqlException ex)
				{
					_logger.Error(
						ex,
						"SQL error occurred while creating user - Username: {Username}",
						userViewModel?.UserName);

					if (ex.Message.ToLower().Contains("duplicate"))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Conflict,
							ResponseMessage = "User with this username or email already exists",
							Status = "failed"
						};
					}

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "Database error occurred while creating user",
						Status = "failed"
					};
				}
				catch (Exception ex)
				{
					_logger.Error(
						ex,
						"Unexpected error occurred while creating user - Username: {Username}",
						userViewModel?.UserName);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An unexpected error occurred while creating user",
						Status = "failed"
					};
				}
			}
		}

		public async Task<BaseResponse> RefreshToken(string incomingToken)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(incomingToken))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Refresh token is required",
						Status = "failed"
					};

				var stored = await _queryRepoRefreshToken.GetByToken(incomingToken);
				if (stored is null)
				{
					_logger.Warning("Refresh token not found - Token: {Token}", incomingToken);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Invalid refresh token",
						Status = "failed"
					};
				}

				if (stored.IsRevoked)
				{
					_logger.Warning(
						"Revoked token reuse detected - UserId: {UserId}", stored.UserId);

					using var revokeScope = _dbTransactionScopeFactory.Create("DbConnectionString");
					try
					{
						await _commandRepoRefreshToken.RevokeAllTokensForUser(revokeScope.Transaction, revokeScope.Connection, stored.UserId);
						await revokeScope.CommitAsync();
					}
					catch (Exception ex)
					{
						_logger.Error(ex, "Failed to revoke token family - UserId: {UserId}", stored.UserId);
						try { await revokeScope.RollbackAsync(); }
						catch (Exception rbEx) { _logger.Error(rbEx, "Rollback failed"); }
					}

					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Refresh token has already been used or revoked",
						Status = "failed"
					};
				}
				
				if (stored.ExpiresAt <= DateTime.Now)
				{
					_logger.Warning(
						"Expired refresh token - UserId: {UserId}", stored.UserId);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Refresh token has expired, please log in again",
						Status = "failed"
					};
				}

				var user = await _queryrepositoryUser.Get(stored.UserId);
				if (user is null || !user.IsActive)
				{
					_logger.Warning("Refresh for inactive/missing user - UserId: {UserId}", stored.UserId);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "User account is inactive or does not exist",
						Status = "failed"
					};
				}

				// Load school info for token generation — same as login
				var schInfo = await _queryrepositorySchool.Get(user.SchoolId);
				var mappedSchInfo = _mapper.Map<SchoolResponseModel>(schInfo);

				// ✅ Reuse the existing private method directly — no IJwtService needed
				var newAccessToken = GenerateJwtToken(user, user.SchoolId.ToString(), mappedSchInfo);
				var newRefreshValue = GenerateSecureRefreshToken();

				var expiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");

				var newRefreshToken = new RefreshTokens
				{
					Id = Guid.NewGuid(),
					UserId = user.Id,
					SchoolId = stored.SchoolId,
					Token = newRefreshValue,
					ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
					CreatedAt = DateTime.UtcNow,
					IsRevoked = false
				};

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				try
				{
					await _commandRepoRefreshToken.RevokeToken(
						scope.Transaction, scope.Connection,
						stored.Id, replacedByToken: newRefreshValue);

					await _commandRepoRefreshToken.Create(
						scope.Transaction, scope.Connection, newRefreshToken);

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Failed to rotate refresh token - UserId: {UserId}", user.Id);
					try { await scope.RollbackAsync(); }
					catch (Exception rbEx) { _logger.Error(rbEx, "Rollback failed during rotation"); }
					throw;
				}

				_logger.Information(
					"Token refreshed - UserId: {UserId}", user.Id);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Token refreshed successfully",
					Status = "successful",
					Data = new
					{
						AccessToken = newAccessToken,
						RefreshToken = newRefreshValue,
						RefreshTokenExpiry = newRefreshToken.ExpiresAt,
						TokenExpiresIn = 3600
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Unexpected error during token refresh");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred during token refresh",
					Status = "failed"
				};
			}
		}

		private static string GenerateSecureRefreshToken()
		{
			var bytes = new byte[64];
			RandomNumberGenerator.Fill(bytes);
			return Convert.ToBase64String(bytes);
		}

		//public async Task<BaseResponse> SaveClassTeacherUser(UserViewModel userViewModel)
		//{

		//}

		//public async Task ValidateIfUserHasAccessToRemovedClasses(List<Guid> classes, )

		public async Task<BaseResponse> updatePassword(UpdatePasswordViewModel model, AuthenticatedUserClaims claims)
		{
			try
			{
				if (model is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Request cannot be empty",
						Status = "failed"
					};

				if (!Guid.TryParse(claims.UserId, out var userId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Invalid authentication",
						Status = "failed"
					};

				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Invalid authentication",
						Status = "failed"
					};

				if (string.IsNullOrWhiteSpace(model.HashPassword))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "New password is required",
						Status = "failed"
					};

				if (string.IsNullOrWhiteSpace(model.CurrentHashPassword))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Current password is required",
						Status = "failed"
					};

				var user = await _queryrepositoryUser.Get(userId);
				if (user is null || !user.IsActive)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "Account not found or is inactive",
						Status = "failed"
					};

				var loginHistory = await LastLoginHistorys(user.Id);
				if (loginHistory.Count() <= 1)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "Please use the first time login password update",
						Status = "failed"
					};

				if (model.CurrentHashPassword != user.HashPassword)
				{
					_logger.Warning(
						"Password update failed - wrong old password - UserId: {UserId}",
						user.Id);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Current password is incorrect",
						Status = "failed"
					};
				}

				// ── Prevent reusing same password ────────────────────────────────────
				if (model.HashPassword == user.HashPassword)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "New password cannot be the same as your current password",
						Status = "failed"
					};

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				var updateQuery = @"
					UPDATE Users 
					SET HashPassword = @HashPassword,
						ModifiedDate = @ModifiedDate
					WHERE Id       = @Id
					AND   SchoolId = @SchoolId";

				var updateParams = new Dictionary<string, object>
				{
					{ "HashPassword", model.HashPassword },
					{ "ModifiedDate", now },
					{ "SchoolId",     schoolId }
				};

				var updateKeyValue = new KeyValuePair<string, object>("Id", user.Id);

				var loginHistoryDict = new Dictionary<string, object>
				{
					{ "Id",            Guid.NewGuid() },
					{ "CreationDate",  now },
					{ "ModifiedDate",  now },
					{ "UserId",        user.Id },
					{ "RoleId",        user.RoleId },
					{ "PasswordFailed", false },
					{ "DeviceType",    model.DeviceType ?? string.Empty },
					{ "DeviceIp",      model.DeviceIp   ?? string.Empty }
				};

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				try
				{
					await _commandRepositoryUser.UpdateAsync(
						scope.Transaction, scope.Connection,
						updateQuery, updateParams, updateKeyValue);

					await _commandRepositoryLoginHistory.Create(
						scope.Transaction, scope.Connection, loginHistoryDict);

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Rollback during password update - UserId: {UserId}", user.Id);
					try { await scope.RollbackAsync(); }
					catch (Exception rbEx)
					{
						_logger.Error(rbEx,
							"Rollback failed - UserId: {UserId}", user.Id);
					}
					throw;
				}

				_logger.Information("Password updated successfully - UserId: {UserId}", user.Id);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Password updated successfully",
					Status = "successful"
				};
			}
			catch (SqlException ex)
			{
				_logger.Error(ex, "SQL error during password update");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Database error occurred",
					Status = "failed"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Unexpected error during password update");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred",
					Status = "failed"
				};
			}
		}

		public async Task<BaseResponse> UpdatePasswordFirstTime(UpdatePasswordViewModelV2 model)
		{
			try
			{
				if (model is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Request cannot be empty",
						Status = "failed"
					};

				//if (tenant is null)
				//	return new BaseResponse
				//	{
				//		ResponseCode = ResponseCode.BadRequest,
				//		ResponseMessage = "Invalid tenant information",
				//		Status = "failed"
				//	};

				if (string.IsNullOrWhiteSpace(model.username))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Username is required",
						Status = "failed"
					};

				if (string.IsNullOrWhiteSpace(model.HashPassword))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "New password is required",
						Status = "failed"
					};

				if (string.IsNullOrWhiteSpace(model.CurrentHashPassword))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Current password is required",
						Status = "failed"
					};

				// ── Fetch user ───────────────────────────────────────────────────────
				var loginUserInput = new Dictionary<string, object>
				{
					{ "UserName", model.username },
					{ "SchoolId", model.SchoolId }
				};

				var user = await _queryrepositoryUser.GetBy(loginUserInput);
				if (user is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Incorrect credentials",
						Status = "failed"
					};

				var loginHistory = await LastLoginHistorys(user.Id);
				if (loginHistory.Count() != 1)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "This endpoint is only for first time login",
						Status = "failed"
					};

				// ── Verify old password matches ──────────────────────────────────────
				if (model.CurrentHashPassword != user.HashPassword)
				{
					_logger.Warning(
						"First time password update failed - wrong old password - UserId: {UserId}",
						user.Id);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Current password is incorrect",
						Status = "failed"
					};
				}

				// ── Prevent reusing same password ────────────────────────────────────
				if (model.HashPassword == user.HashPassword)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "New password cannot be the same as your current password",
						Status = "failed"
					};

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				// ── Update password + log history + issue tokens atomically ──────────
				var updateQuery = @"
					UPDATE Users 
					SET HashPassword = @HashPassword,
						ModifiedDate = @ModifiedDate
					WHERE Id       = @Id
					AND   SchoolId = @SchoolId";

				var updateParams = new Dictionary<string, object>
				{
					{ "HashPassword", model.HashPassword },
					{ "ModifiedDate", now },
					{ "SchoolId",     model.SchoolId }
				};

				var updateKeyValue = new KeyValuePair<string, object>("Id", user.Id);

				var loginHistoryDict = new Dictionary<string, object>
				{
					{ "Id",            Guid.NewGuid() },
					{ "CreationDate",  now },
					{ "ModifiedDate",  now },
					{ "UserId",        user.Id },
					{ "RoleId",        user.RoleId },
					{ "PasswordFailed", false },
					{ "DeviceType",    model.DeviceType ?? string.Empty },
					{ "DeviceIp",      model.DeviceIp   ?? string.Empty }
				};

				// Generate tokens to log them in immediately after password update
				var schInfo = await _queryrepositorySchool.Get(model.SchoolId);
				var mappedSchInfo = _mapper.Map<SchoolResponseModel>(schInfo);
				var accessToken = _jwtTokenGenerator.Generate(
					user, model.SchoolId.ToString(), mappedSchInfo);

				string refreshTokenValue;

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				try
				{
					// Update password
					await _commandRepositoryUser.UpdateAsync(
						scope.Transaction, scope.Connection,
						updateQuery, updateParams, updateKeyValue);

					// Log history
					await _commandRepositoryLoginHistory.Create(
						scope.Transaction, scope.Connection, loginHistoryDict);

					// Issue refresh token
					refreshTokenValue = await GenerateAndStoreRefreshToken(
						scope.Transaction, scope.Connection,
						user.Id, user.SchoolId);

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Rollback during first time password update - UserId: {UserId}",
						user.Id);
					try { await scope.RollbackAsync(); }
					catch (Exception rbEx)
					{
						_logger.Error(rbEx,
							"Rollback failed - UserId: {UserId}", user.Id);
					}
					throw;
				}

				_logger.Information(
					"First time password updated successfully - UserId: {UserId}",
					user.Id);

				// ── Return full login response — user is now logged in ───────────────
				return new UserLoginResponse
				{
					FirstName = user.FirstName,
					LastName = user.LastName,
					RoleId = user.RoleId,
					Id = user.Id,
					EmailAddress = user.EmailAddress,
					IsActive = user.IsActive,
					SchoolInfo = mappedSchInfo,
					Token = accessToken,
					RefreshToken = refreshTokenValue,
					TokenExpiresIn = 3600,
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Password updated successfully. You are now logged in.",
					Status = "successful"
				};
			}
			catch (SqlException ex)
			{
				_logger.Error(ex, "SQL error during first time password update");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Database error occurred",
					Status = "failed"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Unexpected error during first time password update");
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred",
					Status = "failed"
				};
			}
		}

		public async Task<BaseResponse> UnlockUserAccount(Guid userId, AuthenticatedUserClaims claims)
		{
			try
			{
				if (claims is null || string.IsNullOrWhiteSpace(claims.UserId) || string.IsNullOrWhiteSpace(claims.SchoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Invalid user claims",
						Status = "failed"
					};
				}

				var requesterId = Guid.Parse(claims.UserId);
				var schoolId = Guid.Parse(claims.SchoolId);

				var requester = await _queryrepositoryUser.Get(requesterId);
				if (requester is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Requester not found",
						Status = "failed"
					};
				}

				if (requester.SchoolId != schoolId)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "Access denied",
						Status = "failed"
					};
				}

				if (requester.RoleId != (int)UserRole.SuperAdministrator)
				{
					var canManageUsers =
						await HasPermission(requesterId, schoolId, AdminPermission.ManageStudents) ||
						await HasPermission(requesterId, schoolId, AdminPermission.ManageTeachers) ||
						await HasPermission(requesterId, schoolId, AdminPermission.CreateUsers);

					if (!canManageUsers)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You do not have permission to unlock user accounts",
							Status = "failed"
						};
					}
				}

				var targetUser = await _queryrepositoryUser.Get(userId);
				if (targetUser is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "User not found",
						Status = "failed"
					};
				}

				if (targetUser.SchoolId != schoolId)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "Cannot unlock user from a different school",
						Status = "failed"
					};
				}

				if (targetUser.IsActive)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "User account is already active",
						Status = "successful"
					};
				}

				var lastThree = await LastLoginHistorys(targetUser.Id);
				var consecutiveFailures = lastThree?
					.Where(h => h?.PasswordFailed == true)
					.ToList();

				if (consecutiveFailures is null || consecutiveFailures.Count < 3)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "User was not locked due to incorrect password attempts.",
						Status = "failed"
					};
				}

				await _commandRepositoryUser.UpdateTableColumnById(
					nameof(targetUser.IsActive), nameof(targetUser.Id), true, targetUser.Id);

				using var conn = new SqlConnection(_connString);
				await conn.OpenAsync();

				const string insertAudit = @"
					INSERT INTO LoginHistory (Id, CreationDate, ModifiedDate, UserId, RoleId, PasswordFailed, DeviceType)
					VALUES (@Id, @CreationDate, @ModifiedDate, @UserId, @RoleId, 0, @DeviceType)";

				await conn.ExecuteAsync(insertAudit, new
				{
					Id = Guid.NewGuid(),
					CreationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
					ModifiedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
					UserId = targetUser.Id,
					RoleId = targetUser.RoleId,
					DeviceType = "AdminUnlock"
				});

				_logger.Information(
					"Account unlocked by admin - TargetUserId: {TargetUserId}, UnlockedBy: {UnlockedBy}",
					targetUser.Id, requesterId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "User account has been unlocked successfully",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to unlock user account - UserId: {UserId}", userId);
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred",
					Status = "failed"
				};
			}
		}

		private async Task<IEnumerable<LoginHistory?>> LastLoginHistorys(Guid userId)
		{
			string query = $"select top 3 * from LoginHistory where UserId = '{userId}' order by CreationDate DESC";
			var lastLoginHistory = await _queryrepositoryLoginHistory.GetByQuery(query);
			return lastLoginHistory;
		}
		public async Task<BaseResponse> RegisterToClass(RegisterStudentClassViewModel registerStudentClassViewModel,AuthenticatedUserClaims userInfo)
		{
			#region
			using (LogContext.PushProperty("RequestedBy", userInfo.UserId))
			//using (LogContext.PushProperty("TenantId", userInfo.TenantIdentifier))
			{
				try
				{
					// ===== VALIDATION SECTION =====

					if (registerStudentClassViewModel is null)
					{
						_logger.Warning("RegisterToClass called with null model");

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Registration data cannot be empty",
							Status = "failed"
						};
					}

					if (string.IsNullOrEmpty(userInfo.SchoolId))
					{
						_logger.Warning("RegisterToClass called with missing SchoolId");

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "SchoolId not found in authentication token",
							Status = "failed"
						};
					}

					if (string.IsNullOrEmpty(userInfo.UserId))
					{
						_logger.Warning("RegisterToClass called with missing UserId");

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "UserId not found in authentication token",
							Status = "failed"
						};
					}

					if (!Guid.TryParse(userInfo.SchoolId, out var schoolId))
					{
						_logger.Warning("Invalid SchoolId format - SchoolId: {SchoolId}", userInfo.SchoolId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format in token",
							Status = "failed"
						};
					}

					if (!Guid.TryParse(userInfo.UserId, out var createdBy))
					{
						_logger.Warning("Invalid UserId format - UserId: {UserId}", userInfo.UserId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId format in token",
							Status = "failed"
						};
					}

					if (!Enum.TryParse<UserRole>(userInfo.Role, ignoreCase: true, out UserRole userRole))
					{
						_logger.Warning("Invalid role format in token - Role: {Role}", userInfo.Role);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid role format in token",
							Status = "failed"
						};
					}

					// Only Admins and SuperAdmins can do this
					if (userRole != UserRole.Administrator &&
						userRole != UserRole.SuperAdministrator)
					{
						_logger.Warning(
							"Unauthorized class registration attempt - UserId: {UserId}, Role: {Role}",
							createdBy,
							((UserRole)userRole).ToString());

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You are not authorized to register students to classes",
							Status = "failed"
						};
					}

					// SuperAdministrators always have permission
					if (userRole == UserRole.Administrator)
					{
						var hasPermission = await this.HasPermission(createdBy,schoolId,AdminPermission.CreateLessons);

						if (!hasPermission)
						{
							_logger.Warning(
								"Admin lacks CreateLessons permission - AdminId: {AdminId}, StudentId: {StudentId}, ClassroomId: {ClassroomId}",
								createdBy,
								registerStudentClassViewModel.StudentId,
								registerStudentClassViewModel.ClassId);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = "You don't have permission to register students to classes. Contact your SuperAdministrator.",
								Status = "failed"
							};
						}

						_logger.Information(
							"Admin has CreateLessons permission - AdminId: {AdminId}",
							createdBy);
					}

					_logger.Information(
						"Registering student to class - StudentId: {StudentId}, ClassroomId: {ClassroomId}, RegisteredBy: {RegisteredBy}",
						registerStudentClassViewModel.StudentId,
						registerStudentClassViewModel.ClassId,
						createdBy);

					// Validation 4: Validate student
					var studentValidation = await ValidateStudent(
						registerStudentClassViewModel.StudentId,
						schoolId);

					if (!studentValidation.IsValid)
					{
						_logger.Warning(
							"Student validation failed - StudentId: {StudentId}, Error: {Error}",
							registerStudentClassViewModel.StudentId,
							studentValidation.Message);

						return new BaseResponse
						{
							ResponseCode = studentValidation.ResponseCode,
							ResponseMessage = studentValidation.Message,
							Status = "failed"
						};
					}

					// Validation 5: Validate classroom
					var classroomValidation = await ValidateClassroom(
						registerStudentClassViewModel.ClassId,
						schoolId);

					if (!classroomValidation.IsValid)
					{
						_logger.Warning(
							"Classroom validation failed - ClassroomId: {ClassroomId}, Error: {Error}",
							registerStudentClassViewModel.ClassId,
							classroomValidation.Message);

						return new BaseResponse
						{
							ResponseCode = classroomValidation.ResponseCode,
							ResponseMessage = classroomValidation.Message,
							Status = "failed"
						};
					}

					var isAlreadyRegistered = await CheckExistingRegistration(
						registerStudentClassViewModel.StudentId,
						registerStudentClassViewModel.ClassId
					);

					if (isAlreadyRegistered)
					{
						_logger.Warning(
							"Student already registered in classroom - StudentId: {StudentId}, ClassroomId: {ClassroomId}",
							registerStudentClassViewModel.StudentId,
							registerStudentClassViewModel.ClassId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Conflict,
							ResponseMessage = "Student is already registered in this classroom",
							Status = "failed"
						};
					}

					var existingActiveClass = await GetStudentActiveClassroom(
						registerStudentClassViewModel.StudentId);

					if (existingActiveClass.HasValue)
					{
						_logger.Warning(
							"Student already enrolled in another classroom - StudentId: {StudentId}, CurrentClassroomId: {CurrentClassroomId}, AttemptedClassroomId: {AttemptedClassroomId}",
							registerStudentClassViewModel.StudentId,
							existingActiveClass.Value,
							registerStudentClassViewModel.ClassId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Conflict,
							ResponseMessage = "Student is already enrolled in another classroom. Please transfer the student first.",
							Status = "failed",
							Data = new { CurrentClassroomId = existingActiveClass.Value }
						};
					}

					// ===== REGISTRATION SECTION =====

					var registrationId = Guid.NewGuid();
					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

					var studentCourse = new Dictionary<string, object>
					{
						{ "Id", registrationId },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "ClassroomId", registerStudentClassViewModel.ClassId },
						{ "StudentId", registerStudentClassViewModel.StudentId },
						{ "SchoolId", schoolId },
						{ "CreatedBy", createdBy },
						{ "Status", (int)StudentClassroomStatus.Active }
					};

					using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");

					await _studentCourseCommandRepository.Create(
						scope.Transaction,
						scope.Connection,
						studentCourse);

					await scope.CommitAsync();

					_logger.Information(
						"Student registered to classroom successfully - RegistrationId: {RegistrationId}, StudentId: {StudentId}, ClassroomId: {ClassroomId}, RegisteredBy: {RegisteredBy}",
						registrationId,
						registerStudentClassViewModel.StudentId,
						registerStudentClassViewModel.ClassId,
						createdBy);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Student registered to classroom successfully",
						Status = "successful",
						Data = new
						{
							RegistrationId = registrationId,
							StudentId = registerStudentClassViewModel.StudentId,
							ClassroomId = registerStudentClassViewModel.ClassId,
							Status = StudentClassroomStatus.Active.ToString()
						}
					};
				}
				catch (SqlException ex)
				{
					_logger.Error(
						ex,
						"SQL error occurred while registering student - StudentId: {StudentId}, ClassroomId: {ClassroomId}",
						registerStudentClassViewModel?.StudentId,
						registerStudentClassViewModel?.ClassId);

					if (ex.Message.ToLower().Contains("duplicate"))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Conflict,
							ResponseMessage = "Student is already registered in this classroom",
							Status = "failed"
						};
					}

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "Database error occurred while registering student",
						Status = "failed"
					};
				}
				catch (Exception ex)
				{
					_logger.Error(
						ex,
						"Unexpected error occurred while registering student - StudentId: {StudentId}, ClassroomId: {ClassroomId}",
						registerStudentClassViewModel?.StudentId,
						registerStudentClassViewModel?.ClassId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An unexpected error occurred while registering student",
						Status = "failed"
					};
				}
			}
			#endregion
		}


		public async Task<BaseResponse> EditUser(UpdateUserView updateUserViewModel, AuthenticatedUserClaims? userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims?.UserId))
			{
				try
				{
					if (updateUserViewModel is null)
					{
						_logger.Warning("EditUser called with null model");
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "User data cannot be empty",
							Status = "failed"
						};
					}

					if (string.IsNullOrEmpty(userClaims?.SchoolId) || string.IsNullOrEmpty(userClaims?.UserId))
					{
						_logger.Warning("EditUser called with missing authentication information");
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "User authentication information is missing",
							Status = "failed"
						};
					}

					if (!Guid.TryParse(userClaims.SchoolId, out var claimSchoolId))
					{
						_logger.Warning("Invalid SchoolId format - SchoolId: {SchoolId}", userClaims.SchoolId);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format in token",
							Status = "failed"
						};
					}

					if (!Guid.TryParse(userClaims.UserId, out var modifiedBy))
					{
						_logger.Warning("Invalid UserId format - UserId: {UserId}", userClaims.UserId);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId format in token",
							Status = "failed"
						};
					}

					if (!Enum.TryParse<UserRole>(userClaims.Role, ignoreCase: true, out UserRole userRole))
					{
						_logger.Warning("Invalid role format - Role: {Role}", userClaims.Role);
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
						_logger.Warning("Modifier user does not exist - UserId: {UserId}", modifiedBy);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Modifier user does not exist",
							Status = "failed"
						};
					}

					if (!modifier.IsActive)
					{
						_logger.Warning("Inactive user attempted to edit user - UserId: {UserId}", modifiedBy);
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
						_logger.Warning("Target user not found - UserId: {UserId}", updateUserViewModel.Id);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "User not found",
							Status = "failed"
						};
					}

					if (existingUser.SchoolId != claimSchoolId)
					{
						_logger.Warning(
							"Cross-school user edit attempt - ModifiedBy: {ModifiedBy}, TargetUserId: {TargetUserId}",
							modifiedBy, updateUserViewModel.Id);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You cannot update users from a different school",
							Status = "failed"
						};
					}

					// ===== AUTHORIZATION =====
					// Only SuperAdmin or Administrator with CreateUsers permission may use this
					// endpoint. Self-edit (users editing their own profile) is not permitted here;
					// activating an inactive user likewise requires CreateUsers permission.

					if (userRole == UserRole.SuperAdministrator)
					{
						// SuperAdmin always allowed
						_logger.Information(
							"SuperAdmin editing user - AdminId: {AdminId}, TargetUserId: {TargetUserId}",
							modifiedBy, updateUserViewModel.Id);
					}
					else if (userRole == UserRole.Administrator)
					{
						var hasPermission = await this.HasPermission(
							modifiedBy,
							claimSchoolId,
							AdminPermission.CreateUsers
						);

						if (!hasPermission)
						{
							_logger.Warning(
								"Admin lacks CreateUsers permission - AdminId: {AdminId}, TargetUserId: {TargetUserId}",
								modifiedBy, updateUserViewModel.Id);
							return new BaseResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = "You don't have permission to edit users. Contact your SuperAdministrator.",
								Status = "failed"
							};
						}

						_logger.Information(
							"Admin with CreateUsers permission editing user - AdminId: {AdminId}, TargetUserId: {TargetUserId}",
							modifiedBy, updateUserViewModel.Id);
					}
					else
					{
						// Teachers, students and other roles cannot edit users
						_logger.Warning(
							"Unauthorized edit attempt - ModifiedBy: {ModifiedBy}, Role: {Role}, TargetUserId: {TargetUserId}",
							modifiedBy, userRole.ToString(), updateUserViewModel.Id);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You are not authorized to update users",
							Status = "failed"
						};
					}

					// SuperAdmin protection — only SuperAdmin can edit another SuperAdmin
					if (existingUser.RoleId == (int)UserRole.SuperAdministrator && userRole != UserRole.SuperAdministrator)
					{
						_logger.Warning(
							"Non-SuperAdmin attempted to edit SuperAdmin - ModifiedBy: {ModifiedBy}, TargetUserId: {TargetUserId}",
							modifiedBy, updateUserViewModel.Id);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Only SuperAdministrator can update SuperAdministrator accounts",
							Status = "failed"
						};
					}

					// Email uniqueness check
					if (!string.IsNullOrWhiteSpace(updateUserViewModel.EmailAddress) &&
						updateUserViewModel.EmailAddress.Trim().ToLower() != existingUser.EmailAddress?.ToLower())
					{
						var emailExists = await CheckEmailExists(
							updateUserViewModel.EmailAddress,
							claimSchoolId,
							updateUserViewModel.Id);

						if (emailExists)
						{
							_logger.Warning(
								"Email already in use - Email: {Email}, TargetUserId: {TargetUserId}",
								updateUserViewModel.EmailAddress, updateUserViewModel.Id);
							return new BaseResponse
							{
								ResponseCode = ResponseCode.Conflict,
								ResponseMessage = "Email address is already in use by another user",
								Status = "failed"
							};
						}
					}

					// ===== BUILD UPDATE =====

					var updateDict = new Dictionary<string, object>
					{
						{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
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

					if (!string.IsNullOrWhiteSpace(updateUserViewModel.EmailAddress))
					{
						updateDict["EmailAddress"] = updateUserViewModel.EmailAddress.Trim().ToLower();
						updatedFields.Add("EmailAddress");
					}

					if (!string.IsNullOrWhiteSpace(updateUserViewModel.HashPassword))
					{
						// TODO: Hash password before storing
						updateDict["HashPassword"] = updateUserViewModel.HashPassword;
						updatedFields.Add("Password");
					}

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
						if (updateUserViewModel.RoleId.Value == (int)UserRole.SuperAdministrator &&
							userRole != UserRole.SuperAdministrator)
						{
							_logger.Warning(
								"Unauthorized SuperAdmin promotion attempt - ModifiedBy: {ModifiedBy}, TargetUserId: {TargetUserId}",
								modifiedBy, updateUserViewModel.Id);
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

					if (updateDict.Count == 1)
					{
						_logger.Warning("No fields to update - UserId: {UserId}", updateUserViewModel.Id);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "No fields to update",
							Status = "failed"
						};
					}

					var whereClause = new KeyValuePair<string, object>("Id", updateUserViewModel.Id);
					await _commandRepositoryUser.UpdateTableColumnById(updateDict, whereClause);

					_logger.Information(
						"User updated successfully - UserId: {UserId}, UpdatedFields: [{UpdatedFields}], ModifiedBy: {ModifiedBy}",
						updateUserViewModel.Id,
						string.Join(", ", updatedFields),
						modifiedBy);

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
					_logger.Error(ex, "SQL error occurred while updating user - UserId: {UserId}", updateUserViewModel?.Id);

					if (ex.Message.ToLower().Contains("duplicate"))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Conflict,
							ResponseMessage = "Email address is already in use",
							Status = "failed"
						};
					}

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "Database error occurred while updating user",
						Status = "failed"
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Unexpected error occurred while updating user - UserId: {UserId}", updateUserViewModel?.Id);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An unexpected error occurred while updating user",
						Status = "failed"
					};
				}
			}
		}


		public async Task<BaseResponse> GetTeachersBySchool(AuthenticatedUserClaims userClaims)
		{
			try
			{
				// Validate claims
				if (string.IsNullOrEmpty(userClaims.SchoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Authentication information is missing",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId format in token",
						Status = "failed"
					};
				}

				// Validate role
				if (!Enum.TryParse<UserRole>(userClaims.Role, ignoreCase: true,
					out UserRole userRole))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid role format in token",
						Status = "failed"
					};
				}

				// Only Admin and SuperAdmin can view all teachers
				if (userRole != UserRole.Administrator &&
					userRole != UserRole.SuperAdministrator)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You are not authorized to view teachers",
						Status = "failed"
					};
				}

				var teachers = await _queryrepositoryUser.GetTeachersBySchoolAsync(schoolId, DatabaseTarget.Core);

				var teacherList = teachers.ToList();

				_logger.Information(
					"Teachers fetched - SchoolId: {SchoolId}, Count: {Count}",
					schoolId,
					teacherList.Count);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = teacherList.Any()
						? $"{teacherList.Count} teacher(s) found"
						: "No teachers found for this school",
					Status = "successful",
					Data = teacherList
				};
			}
			catch (SqlException ex)
			{
				_logger.Error(ex,
					"SQL error fetching teachers - SchoolId: {SchoolId}",
					userClaims.SchoolId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Database error occurred while fetching teachers",
					Status = "failed"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Unexpected error fetching teachers - SchoolId: {SchoolId}",
					userClaims.SchoolId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred",
					Status = "failed"
				};
			}
		}


		private async Task<string> GetSchoolCode(Guid schoolId)
		{
			try
			{
				// which is your school code
				var tenant = await _tenantService.GetTenantBySchoolIdAsync(schoolId);

				if (tenant != null && !string.IsNullOrEmpty(tenant.Identifier))
				{
					return tenant.Identifier;
				}

				// Fallback to SchoolCode table
				var query = $@"SELECT Code FROM SchoolCode WHERE SchoolId = '{schoolId}'";

				var result = await _schCodeQueryRespository.GetByQuery(query);

				return result.FirstOrDefault()?.Code ?? schoolId.ToString();
			}
			catch (Exception ex)
			{
				_logger.Error(
					ex,
					"Error fetching school code - SchoolId: {SchoolId}",schoolId);

				return schoolId.ToString();
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

				// Populate roleData for students
				if (roleId.GetValueOrDefault() == (int)UserRole.Student)
				{
					var studentIds = paginatedUsers.Select(u => u!.Id).ToList();
					var roleDataMap = await GetStudentRoleDataBatch(studentIds, schoolId);
					foreach (var dto in userDtos)
					{
						dto.RoleData = roleDataMap.GetValueOrDefault((Guid)dto.Id);
					}
				}

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

		public async Task<BaseResponse> GetStudentsByClassroom(Guid classroomId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims.UserId))
			{
				try
				{
					if (!Guid.TryParse(claims.SchoolId, out var schoolId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "Invalid school context",
							Status = "failed"
						};

					var sql = $@"
						SELECT
							u.Id,
							u.FirstName,
							u.LastName,
							u.UserName,
							u.EmailAddress,
							u.IsActive,
							u.CreationDate
						FROM   StudentClassroom sc
						JOIN   Users            u  ON u.Id = sc.StudentId
						WHERE  sc.ClassroomId = '{classroomId}'
						AND    sc.SchoolId    = '{schoolId}'
						AND    sc.IsActive    = 1
						AND    u.IsActive     = 1
						ORDER  BY u.FirstName ASC";

					var rows = await _queryrepositoryUser.QueryAsync<StudentRowDto>(sql, new Dictionary<string, object>());
					var students = rows.ToList();
					var studentIds = students.Select(s => s.Id).ToList();

					// Fetch roleData for all returned students
					var roleDataMap = await GetStudentRoleDataBatch(studentIds, schoolId);

					var result = students.Select(s => new
					{
						s.Id,
						s.FirstName,
						s.LastName,
						s.UserName,
						s.EmailAddress,
						s.IsActive,
						RoleData = roleDataMap.GetValueOrDefault(s.Id)
					}).ToList();

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = students.Any()
							? $"{students.Count} student(s) found"
							: "No students found in this classroom",
						Status = "success",
						Data = result
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error fetching students by classroom - ClassroomId: {ClassroomId}", classroomId);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching students",
						Status = "failed"
					};
				}
			}
		}

		public async Task<BaseResponse> GetStudentsBySubject(Guid subjectId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims.UserId))
			{
				try
				{
					if (!Guid.TryParse(claims.SchoolId, out var schoolId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "Invalid school context",
							Status = "failed"
						};

					// Students can take a subject as major (via classroom) or minor
					var sql = $@"
						SELECT DISTINCT
							u.Id,
							u.FirstName,
							u.LastName,
							u.UserName,
							u.EmailAddress,
							u.IsActive,
							u.CreationDate
						FROM   (
							SELECT sc.StudentId
							FROM   StudentClassroom sc
							JOIN   ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId
							WHERE  cs.SubjectId = '{subjectId}'
							AND    sc.SchoolId  = '{schoolId}'
							AND    sc.IsActive  = 1
							AND    cs.IsActive  = 1
							UNION
							SELECT sms.StudentId
							FROM   StudentMinorSubject sms
							WHERE  sms.SubjectId = '{subjectId}'
							AND    sms.SchoolId  = '{schoolId}'
						) src
						JOIN   Users u ON u.Id = src.StudentId
						WHERE  u.IsActive = 1
						ORDER  BY u.FirstName ASC";

					var rows = await _queryrepositoryUser.QueryAsync<StudentRowDto>(sql, new Dictionary<string, object>());
					var students = rows.ToList();
					var studentIds = students.Select(s => s.Id).ToList();

					var roleDataMap = await GetStudentRoleDataBatch(studentIds, schoolId);

					var result = students.Select(s => new
					{
						s.Id,
						s.FirstName,
						s.LastName,
						s.UserName,
						s.EmailAddress,
						s.IsActive,
						RoleData = roleDataMap.GetValueOrDefault(s.Id)
					}).ToList();

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = students.Any()
							? $"{students.Count} student(s) found"
							: "No students found for this subject",
						Status = "success",
						Data = result
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error fetching students by subject - SubjectId: {SubjectId}", subjectId);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching students",
						Status = "failed"
					};
				}
			}
		}

		public async Task<BaseResponse> GetTeacherStudents(AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims.UserId))
			{
				try
				{
					if (!Guid.TryParse(claims.SchoolId, out var schoolId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "Invalid school context",
							Status = "failed"
						};

					if (!Guid.TryParse(claims.UserId, out var teacherId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "Invalid user identity",
							Status = "failed"
						};

					if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var role) ||
						(role != UserRole.SubjectTeacher && role != UserRole.ClassTeacher && role != UserRole.HeadTeacher))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Only teachers can access this endpoint",
							Status = "failed"
						};
					}

					// Get teacher's classrooms and subjects
					var classroomIds = await _teacherClassroomQueryRespository
						.QueryAsync<Guid>($@"SELECT ClassroomId FROM ClassroomTeacher WHERE TeacherId = '{teacherId}' AND IsActive = 1",
							new Dictionary<string, object>());
					var classroomIdList = classroomIds.ToList();

					var subjectIds = await _teacherSubjectQueryRespository
						.QueryAsync<Guid>($@"SELECT SubjectId FROM TeacherSubject WHERE TeacherId = '{teacherId}'",
							new Dictionary<string, object>());
					var subjectIdList = subjectIds.ToList();

					if (!classroomIdList.Any() && !subjectIdList.Any())
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.successful,
							ResponseMessage = "No students found",
							Status = "success",
							Data = new List<object>()
						};
					}

					// Build a query to get students in teacher's classrooms OR taking teacher's subjects
					var conditions = new List<string>();
					if (classroomIdList.Any())
					{
						var cIds = string.Join(",", classroomIdList.Select(id => $"'{id}'"));
						conditions.Add($"(sc.ClassroomId IN ({cIds}) AND sc.IsActive = 1)");
					}
					if (subjectIdList.Any())
					{
						var sIds = string.Join(",", subjectIdList.Select(id => $"'{id}'"));
						conditions.Add($"(sms.SubjectId IN ({sIds}))");
					}

					var whereClause = string.Join(" OR ", conditions);

					var sql = $@"
						SELECT DISTINCT
							u.Id,
							u.FirstName,
							u.LastName,
							u.UserName,
							u.EmailAddress,
							u.IsActive,
							u.CreationDate
						FROM   Users u
						LEFT JOIN StudentClassroom sc ON sc.StudentId = u.Id
						LEFT JOIN StudentMinorSubject sms ON sms.StudentId = u.Id
						WHERE  u.SchoolId = '{schoolId}'
						AND    u.RoleId  = {(int)UserRole.Student}
						AND    u.IsActive = 1
						AND    ({whereClause})
						ORDER  BY u.FirstName ASC";

					var rows = await _queryrepositoryUser.QueryAsync<StudentRowDto>(sql, new Dictionary<string, object>());
					var students = rows.ToList();
					var studentIds = students.Select(s => s.Id).ToList();

					var roleDataMap = await GetStudentRoleDataBatch(studentIds, schoolId);

					var result = students.Select(s => new
					{
						s.Id,
						s.FirstName,
						s.LastName,
						s.UserName,
						s.EmailAddress,
						s.IsActive,
						RoleData = roleDataMap.GetValueOrDefault(s.Id)
					}).ToList();

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = students.Any()
							? $"{students.Count} student(s) found"
							: "No students found",
						Status = "success",
						Data = result
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error fetching teacher's students - TeacherId: {TeacherId}", claims.UserId);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching students",
						Status = "failed"
					};
				}
			}
		}

		private async Task<Dictionary<Guid, StudentRoleDataDto?>> GetStudentRoleDataBatch(List<Guid> studentIds, Guid schoolId)
		{
			var result = new Dictionary<Guid, StudentRoleDataDto?>();
			if (!studentIds.Any()) return result;

			foreach (var id in studentIds) result[id] = null;

			var studentIdStr = string.Join(",", studentIds.Select(id => $"'{id}'"));

			var classroomQuery = $@"
				SELECT sc.StudentId, sc.ClassroomId, c.Name AS ClassName
				FROM StudentClassroom sc
				JOIN Classroom c ON c.Id = sc.ClassroomId
				WHERE sc.StudentId IN ({studentIdStr})
				AND sc.SchoolId = '{schoolId}'
				AND sc.IsActive = 1";
			var classroomLookup = (await _queryrepositoryUser.QueryAsync<StudentClassroomRow>(classroomQuery, new Dictionary<string, object>()))
				.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => g.First());

			var majorQuery = $@"
				SELECT sc.StudentId, s.Id AS SubjectId, s.Subject AS SubjectName
				FROM StudentClassroom sc
				JOIN ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId
				JOIN Subjects s ON s.Id = cs.SubjectId
				WHERE sc.StudentId IN ({studentIdStr})
				AND sc.SchoolId = '{schoolId}'
				AND sc.IsActive = 1
				AND cs.IsActive = 1";
			var majorLookup = (await _queryrepositoryUser.QueryAsync<StudentMajorSubjectRow>(majorQuery, new Dictionary<string, object>()))
				.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => g.ToList());

			var minorQuery = $@"
				SELECT sms.StudentId, s.Id AS SubjectId, s.Subject AS SubjectName
				FROM StudentMinorSubject sms
				JOIN Subjects s ON s.Id = sms.SubjectId
				WHERE sms.StudentId IN ({studentIdStr})
				AND sms.SchoolId = '{schoolId}'";
			var minorLookup = (await _queryrepositoryUser.QueryAsync<StudentMajorSubjectRow>(minorQuery, new Dictionary<string, object>()))
				.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => g.ToList());

			foreach (var sid in studentIds)
			{
				StudentClassroomRow? classroom = null;
				if (classroomLookup.TryGetValue(sid, out var cr)) classroom = cr;

				var majors = majorLookup.TryGetValue(sid, out var mj) ? mj : new List<StudentMajorSubjectRow>();
				var minors = minorLookup.TryGetValue(sid, out var mn) ? mn : new List<StudentMajorSubjectRow>();

				if (classroom == null && majors.Count == 0 && minors.Count == 0) continue;

				result[sid] = new StudentRoleDataDto
				{
					Classroom = classroom != null ? new ClassroomInfo
					{
						ClassroomId = classroom.ClassroomId,
						ClassName = classroom.ClassName
					} : null,
					MajorSubjects = majors.Select(m => new SubjectInfo
					{
						SubjectId = m.SubjectId,
						SubjectName = m.SubjectName
					}).ToList(),
					MinorSubjects = minors.Select(m => new SubjectInfo
					{
						SubjectId = m.SubjectId,
						SubjectName = m.SubjectName
					}).ToList()
				};
			}

			return result;
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

		public async Task<BaseResponse> GetAdministrators(AuthenticatedUserClaims userClaims,int pageNumber,int pageSize)
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
			{
				try
				{
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

					if (user.SchoolId != claimSchoolId)
					{
						_logger.Warning(
							"Unauthorized access attempt - RequestedBy: {RequestedBy}, TargetUserId: {TargetUserId}",
							userClaims.UserId, userId);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You cannot access users from a different school",
							Status = "failed"
						};
					}

					var role = (UserRole)user.RoleId;
					object roleData = null;

					switch (role)
					{
						case UserRole.SubjectTeacher:
							var subjectTeacherQuery = $@"
								SELECT
									c.Id       AS ClassroomId,
									c.Name     AS Name,
									s.Id       AS SubjectId,
									s.Subject  AS SubjectName,
									s.Category AS SubjectCategory
								FROM   TeacherClassroom tc
								JOIN   Classroom        c  ON c.Id = tc.ClassroomId
								JOIN   TeacherSubject   ts ON ts.TeacherId = tc.TeacherId
								JOIN   Subjects         s  ON s.Id = ts.SubjectId
								WHERE  tc.TeacherId = '{userId}'
								AND    tc.SchoolId  = '{userClaims.SchoolId}'
								AND    tc.IsActive  = 1
								AND    ts.IsActive  = 1
								ORDER  BY c.Name, s.Subject";

							var subjectTeacherRows = await _queryrepositoryUser.QueryAsync<SubjectTeacherAssignmentRow>(subjectTeacherQuery, new Dictionary<string, object>());

							var classroomsWithSubjects = subjectTeacherRows
								.GroupBy(r => new { r.ClassroomId, r.Name })
								.Select(g => new
								{
									ClassroomId = g.Key.ClassroomId,
									ClassName = g.Key.Name,
									Subjects = g.Select(r => new
									{
										r.SubjectId,
										r.SubjectName,
										r.SubjectCategory
									}).ToList()
								}).ToList();

							roleData = new { Classrooms = classroomsWithSubjects };
							break;

						case UserRole.HeadTeacher:
						case UserRole.ClassTeacher:
							var classTeacherQuery = $@"
								SELECT
									c.Id       AS ClassroomId,
									c.Name     AS ClassName,
									c.IsActive AS ClassroomIsActive
								FROM   TeacherClassroom tc
								JOIN   Classroom        c ON c.Id = tc.ClassroomId
								WHERE  tc.TeacherId = '{userId}'
								AND    tc.SchoolId  = '{userClaims.SchoolId}'
								AND    tc.IsActive  = 1
								ORDER  BY c.Name";

							var classTeacherRows = await _queryrepositoryUser
								.QueryAsync<ClassroomRow>(
									classTeacherQuery, new Dictionary<string, object>());

							roleData = new
							{
								Classrooms = classTeacherRows.Select(r => new
								{
									r.ClassroomId,
									r.ClassName,
									r.ClassroomIsActive
								}).ToList()
							};
							break;

						case UserRole.Student:
							var studentClassroomQuery = $@"
								SELECT
									c.Id       AS ClassroomId,
									c.Name     AS ClassName,
									c.IsActive AS ClassroomIsActive
								FROM   StudentClassroom sc
								JOIN   Classroom        c ON c.Id = sc.ClassroomId
								WHERE  sc.StudentId = '{userId}'
								AND    sc.SchoolId  = '{userClaims.SchoolId}'
								AND    sc.IsActive  = 1";

							var studentClassroom = await _queryrepositoryUser
								.QueryAsync<ClassroomRow>(
									studentClassroomQuery, new Dictionary<string, object>());

							// Major subjects via classroom
							var majorSubjectQuery = $@"
								SELECT
									s.Id       AS SubjectId,
									s.Subject  AS SubjectName,
									s.Category AS SubjectCategory,
									'Major'    AS SubjectType
								FROM   StudentClassroom sc
								JOIN   ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId
								JOIN   Subjects         s  ON s.Id = cs.SubjectId
								WHERE  sc.StudentId = '{userId}'
								AND    sc.SchoolId  = '{claimSchoolId}'
								AND    sc.IsActive  = 1
								AND    cs.IsActive  = 1";

							// Minor subjects
							var minorSubjectQuery = $@"
								SELECT
									s.Id       AS SubjectId,
									s.Subject  AS SubjectName,
									s.Category AS SubjectCategory,
									'Minor'    AS SubjectType
								FROM   StudentMinorSubject sms
								JOIN   Subjects            s ON s.Id = sms.SubjectId
								WHERE  sms.StudentId = '{userId}'
								AND    sms.SchoolId  = '{claimSchoolId}'
								AND    sms.IsActive  = 1";

							var majorSubjects = await _queryrepositoryUser
								.QueryAsync<SubjectRow>(majorSubjectQuery, new Dictionary<string, object>());

							var minorSubjects = await _queryrepositoryUser
								.QueryAsync<SubjectRow>(minorSubjectQuery, new Dictionary<string, object>());

							roleData = new
							{
								Classroom = studentClassroom.FirstOrDefault(),
								MajorSubjects = majorSubjects.ToList(),
								MinorSubjects = minorSubjects.ToList(),
								TotalSubjects = majorSubjects.Count() + minorSubjects.Count()
							};
							break;

						case UserRole.Administrator:
						case UserRole.SuperAdministrator:
							// Fetch permissions for admin roles
							var adminPermissions = await GetExistingPermissions(userId, claimSchoolId);
							if (adminPermissions != null)
							{
								var permissionEnum = (AdminPermission)adminPermissions.Permissions;
								var permissionNames = Enum.GetValues<AdminPermission>()
									.Where(p => p != AdminPermission.None
											 && p != AdminPermission.BasicAdmin
											 && p != AdminPermission.FullAdmin
											 && permissionEnum.HasFlag(p))
									.Select(p => new
									{
										Value = (int)p,
										Name = p.ToString()
									}).ToList();

								roleData = new
								{
									PermissionsValue = adminPermissions.Permissions,
									Permissions = permissionNames
								};
							}
							break;

						default:
							roleData = null;
							break;
					}

					_logger.Information(
						"User retrieved successfully - TargetUserId: {TargetUserId}, Role: {Role}",
						user.Id, role.ToString());

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "User retrieved successfully",
						Status = "successful",
						Data = new
						{
							Id = user.Id,
							FirstName = user.FirstName,
							LastName = user.LastName,
							UserName = user.UserName,
							EmailAddress = user.EmailAddress,
							RoleId = user.RoleId,
							RoleName = role.ToString(),
							IsActive = user.IsActive,
							HasAccess = user.HasAccess,
							ProfileImage = user.ProfileImage,
							GuardianName = user.GuardianName,
							CreatedDate = user.CreationDate,
							ModifiedDate = user.ModifiedDate,
							RoleData = roleData
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

		// ── GET: approval items pending for this HeadTeacher ────────────────────────
		public async Task<BaseResponse> GetPendingApprovalsForUser(AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims?.UserId, out var approverId) || !Guid.TryParse(claims?.SchoolId, out var schoolId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user session",
						Status = "failed"
					};

				var query = $@"
					SELECT
						ar.Id,
						ar.OperationType,
						ar.EntityType,
						ar.EntityId,
						ar.Status,
						ar.Payload,
						ar.CreatedAt,
						ar.ExpiresAt,
						u.FirstName + ' ' + u.LastName AS RequestedByName,
						u.EmailAddress                  AS RequestedByEmail
					FROM   ApprovalRequests ar
					JOIN   Users            u ON u.Id = ar.RequestedBy
					WHERE  ar.ApproverId = '{approverId}'
					AND    ar.SchoolId   = '{schoolId}'
					AND    ar.Status     = '{ApprovalStatus.Pending}'
					AND    ar.ExpiresAt  > GETUTCDATE()
					ORDER  BY ar.CreatedAt ASC";

				var rows = await _queryApprovalRequests.QueryAsync<ApprovalBaseRow>(query, new Dictionary<string, object>());

				var items = rows.Select(r => MapToApprovalItemDto(r)).ToList();

				_logger.Information(
					"Pending approvals fetched - ApproverId: {ApproverId}, Count: {Count}",
					approverId, items.Count);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = items.Any()
						? $"{items.Count} pending approval(s)"
						: "No pending approvals",
					Status = "successful",
					Data = new { Count = items.Count, Items = items }
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Error fetching pending approvals for ApproverId: {Id}", claims?.UserId);
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching approvals",
					Status = "failed"
				};
			}
		}

		// ── Private mapper — one place to add new types ──────────────────────────────
		private ApprovalItemDto MapToApprovalItemDto(ApprovalBaseRow r)
		{
			var dto = new ApprovalItemDto
			{
				Id = r.Id,
				OperationType = r.OperationType,
				EntityType = r.EntityType,
				EntityId = r.EntityId,
				Status = r.Status,
				CreatedAt = r.CreatedAt,
				ExpiresAt = r.ExpiresAt,
				RequestedByName = r.RequestedByName,
				RequestedByEmail = r.RequestedByEmail
			};

			if (string.IsNullOrWhiteSpace(r.Payload)) return dto;

			try
			{
				if (r.OperationType == OperationType.SubmitLesson)
				{
					var lessonPayload = JsonSerializer.Deserialize<LessonApprovalPayload>(r.Payload);
					dto.Payload = lessonPayload;
					dto.Lesson = new LessonSummaryDto
					{
						LessonId = lessonPayload.LessonId,
						Aim = lessonPayload.Aim,
						Description = lessonPayload.Description,
						SubjectName = lessonPayload.SubjectName,
						TopicName = lessonPayload.TopicName,
						ClassName = lessonPayload.ClassName,
						MediaCount = lessonPayload.MediaCount
					};
				}
				else
				{
					var element = JsonSerializer.Deserialize<JsonElement>(r.Payload);
					dto.Payload = element;

					if (element.TryGetProperty("subjectName", out var sn) ||
					    element.TryGetProperty("SubjectName", out sn))
					{
						dto.Summary = JsonSerializer.Deserialize<ApprovalPayloadSummary>(r.Payload);
					}
				}
			}
			catch
			{
				_logger.Warning(
					"Failed to deserialize payload - ApprovalId: {Id}, OperationType: {Op}",
					r.Id, r.OperationType);
			}

			return dto;
		}


		// ── POST: HeadTeacher approves or rejects an item ───────────────────────────
		public async Task<BaseResponse> RespondToApproval(Guid approvalId, ApprovalRespondViewModel model, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims?.UserId, out var approverId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user session",
						Status = "failed"
					};

				if (!model.Approved && string.IsNullOrWhiteSpace(model.RejectionReason))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "A rejection reason is required",
						Status = "failed"
					};

				// Fetch and validate approval belongs to this approver
				var fetchQuery = $@"
					SELECT * FROM ApprovalRequests
					WHERE  Id         = '{approvalId}'
					AND    ApproverId = '{approverId}'
					AND    Status     = '{ApprovalStatus.Pending}'";

				var approvals = await _queryApprovalRequests
					.QueryAsync<ApprovalRequests>(fetchQuery, new Dictionary<string, object>());

				var approval = approvals.FirstOrDefault();
				if (approval is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Approval request not found or already actioned",
						Status = "failed"
					};

				var newStatus = model.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
				var respondedAt = DateTime.UtcNow;

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				try
				{
					// 1. Update ApprovalRequests status
					var updateApproval = $@"
						UPDATE ApprovalRequests
						SET    Status          = '{newStatus}',
							   RespondedAt     = '{respondedAt:yyyy-MM-dd HH:mm:ss}',
							   RejectionReason = {(model.Approved
										   ? "NULL"
										   : $"'{model.RejectionReason.Replace("'", "''")}'")}
						WHERE  Id = '{approvalId}'";

					await scope.Connection.ExecuteAsync(updateApproval, transaction: scope.Transaction);

					// 2. Apply or reject the entity based on OperationType
					if (model.Approved)
					{
						await ApplyApproval(scope, approval, approverId, respondedAt);
					}
					else
					{
						await RejectApproval(scope, approval, approverId, model.RejectionReason);
					}

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Transaction failed for ApprovalId: {Id}", approvalId);
					try { await scope.RollbackAsync(); }
					catch (Exception rbEx)
					{
						_logger.Error(rbEx, "Rollback failed for ApprovalId: {Id}", approvalId);
					}
					throw;
				}

				// Post-commit — fire and forget
				_ = Task.Run(() => NotifyRequester(approval, model.Approved, model.RejectionReason));

				_logger.Information(
					"Approval {ApprovalId} {Status} by {ApproverId}",
					approvalId, newStatus, approverId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = model.Approved ? "Approved successfully" : "Rejected successfully",
					Status = "successful",
					Data = new { ApprovalId = approvalId, NewStatus = newStatus }
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error responding to approval: {Id}", approvalId);
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred",
					Status = "failed"
				};
			}
		}

		// ── Apply approval per OperationType ─────────────────────────────────────────
		private async Task ApplyApproval(IDbTransactionScope scope,ApprovalRequests approval,Guid approverId,DateTime respondedAt)
		{
			switch (approval.OperationType)
			{
				case OperationType.SubmitLesson:
					if (!approval.EntityId.HasValue) break;
					var approveLesson = $@"
						UPDATE LessonContent
						SET    Status     = '{LessonStatus.Approved}',
							   ApprovedBy = '{approverId}',
							   ApprovedAt = '{respondedAt:yyyy-MM-dd HH:mm:ss}'
						WHERE  Id       = '{approval.EntityId}'
						AND    SchoolId = '{approval.SchoolId}'";

					await scope.Connection.ExecuteAsync(
						approveLesson, transaction: scope.Transaction);
					break;

				case OperationType.CreateTopic:
					// Payload holds all topicIds since one approval covers multiple topics
					var topicPayload = DeserializePayload<ApprovalPayloadSummary>(approval.Payload);

					if (topicPayload?.EntityIds?.Any() == true)
					{
						var ids = string.Join(",",
							topicPayload.EntityIds.Select(id => $"'{id}'"));

						var activateTopics = $@"
							UPDATE Topic
							SET    IsActive = 1
							WHERE  Id      IN ({ids})
							AND    SchoolId = '{approval.SchoolId}'";

						await scope.Connection.ExecuteAsync(activateTopics, transaction: scope.Transaction);

						var activateSubTopics = $@"
							UPDATE SubTopic
							SET    IsActive = 1
							WHERE  TopicId IN ({ids})
							AND    SchoolId = '{approval.SchoolId}'";

						await scope.Connection.ExecuteAsync(activateSubTopics, transaction: scope.Transaction);
					}
					break;

				case OperationType.SubmitSyllabus:
					if (!approval.EntityId.HasValue) break;
					var activateSyllabus = $@"
						UPDATE Syllabus
						SET    IsActive  = 1,
							   IsApproved = 1
						WHERE  Id       = '{approval.EntityId}'
						AND    SchoolId = '{approval.SchoolId}'";

					await scope.Connection.ExecuteAsync(
						activateSyllabus, transaction: scope.Transaction);
					break;

				case OperationType.CreateExamination:
					if (!approval.EntityId.HasValue) break;
					var activateExam = $@"
						UPDATE Examination
						SET    IsActive = 1
						WHERE  Id       = '{approval.EntityId}'
						AND    SchoolId = '{approval.SchoolId}'";

					await scope.Connection.ExecuteAsync(activateExam, transaction: scope.Transaction);
					break;

				case OperationType.CreateUser:
					if (!approval.EntityId.HasValue) break;
					var activateUser = $@"
						UPDATE Users
						SET    IsActive = 1
						WHERE  Id       = '{approval.EntityId}'
						AND    SchoolId = '{approval.SchoolId}'";

					await scope.Connection.ExecuteAsync(activateUser, transaction: scope.Transaction);
					break;
				case OperationType.AddSubTopics:
					// EntityId is the TopicId — activate all pending subtopics for this topic
					// that were created by this teacher and are still inactive
					if (approval.EntityId.HasValue)
					{
						var activateSubTopics = $@"
							UPDATE SubTopic
							SET    IsActive  = 1
							WHERE  TopicId   = '{approval.EntityId}'
							AND    SchoolId  = '{approval.SchoolId}'
							AND    IsActive  = 0
							AND    IsDeleted = 0
							AND    CreatedBy = '{approval.RequestedBy}'";

						await scope.Connection.ExecuteAsync(activateSubTopics, transaction: scope.Transaction);
					}
					break;

				default:
					_logger.Warning(
						"No apply handler for OperationType: {OperationType}, ApprovalId: {ApprovalId}",
						approval.OperationType, approval.Id);
					break;
			}
		}

		// ── Reject approval per OperationType ────────────────────────────────────────
		private async Task RejectApproval(IDbTransactionScope scope,ApprovalRequests approval,Guid approverId,string rejectionReason)
		{
			var reason = rejectionReason.Replace("'", "''");

			switch (approval.OperationType)
			{
				case OperationType.SubmitLesson:
					if (!approval.EntityId.HasValue) break;
					var rejectLesson = $@"
						UPDATE LessonContent
						SET    Status          = '{LessonStatus.Rejected}',
							   RejectedBy      = '{approverId}',
							   RejectionReason = '{reason}'
						WHERE  Id       = '{approval.EntityId}'
						AND    SchoolId = '{approval.SchoolId}'";

					await scope.Connection.ExecuteAsync(rejectLesson, transaction: scope.Transaction);
					break;

				case OperationType.CreateTopic:
					// Delete the topics and subtopics — no point keeping rejected inactive records
					var topicPayload = DeserializePayload<ApprovalPayloadSummary>(approval.Payload);

					if (topicPayload?.EntityIds?.Any() == true)
					{
						var ids = string.Join(",",topicPayload.EntityIds.Select(id => $"'{id}'"));

						var deleteSubTopics = $@"
							UPDATE SubTopic
							SET    IsDeleted = 1
							WHERE  TopicId IN ({ids})
							AND    SchoolId = '{approval.SchoolId}'";

						await scope.Connection.ExecuteAsync(deleteSubTopics, transaction: scope.Transaction);

						var deleteTopics = $@"
							UPDATE Topic
							SET    IsDeleted = 1
							WHERE  Id       IN ({ids})
							AND    SchoolId  = '{approval.SchoolId}'";

						await scope.Connection.ExecuteAsync(deleteTopics, transaction: scope.Transaction);
					}
					break;

				case OperationType.SubmitSyllabus:
					if (!approval.EntityId.HasValue) break;
					var rejectSyllabus = $@"
						UPDATE Syllabus
						SET    IsActive = 0
						WHERE  Id       = '{approval.EntityId}'
						AND    SchoolId = '{approval.SchoolId}'";

							await scope.Connection.ExecuteAsync(
								rejectSyllabus, transaction: scope.Transaction);
							break;

				case OperationType.CreateExamination:
					if (!approval.EntityId.HasValue) break;
					var rejectExam = $@"
						UPDATE Examination
						SET    IsActive = 0
						WHERE  Id       = '{approval.EntityId}'
						AND    SchoolId = '{approval.SchoolId}'";

							await scope.Connection.ExecuteAsync(
								rejectExam, transaction: scope.Transaction);
							break;

				case OperationType.CreateUser:
					// Leave user as IsActive = false — admin can re-evaluate
					// Just log it — no DB change needed beyond the approval status
					_logger.Information(
						"User creation rejected - EntityId: {EntityId}, ApprovalId: {ApprovalId}",
						approval.EntityId, approval.Id);
					break;

				default:
					_logger.Warning(
						"No reject handler for OperationType: {OperationType}, ApprovalId: {ApprovalId}",
						approval.OperationType, approval.Id);
					break;
			}
		}

		// ── Safe payload deserializer ─────────────────────────────────────────────────
		private T DeserializePayload<T>(string payload) where T : class
		{
			if (string.IsNullOrWhiteSpace(payload)) return null;
			try
			{
				return JsonSerializer.Deserialize<T>(payload);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Failed to deserialize approval payload");
				return null;
			}
		}


		private async Task NotifyRequester(ApprovalRequests approval, bool approved, string rejectionReason)
		{
			try
			{
				// Fetch the teacher who submitted
				var requester = await _queryrepositoryUser.Get(approval.RequestedBy);
				if (requester is null)
				{
					_logger.Warning(
						"NotifyRequester: requester not found - RequestedBy: {Id}",
						approval.RequestedBy);
					return;
				}

				var templateKey = approved
					? (int)EmailTemplateKey.LessonApprovalOutcome
					: (int)EmailTemplateKey.LessonApprovalRequest;

				var placeholders = new Dictionary<string, string>
				{
					{ "@@Name",   $"{requester.FirstName} {requester.LastName}" },
					{ "@@Status", approved ? "approved" : "rejected" },
					{ "@@Reason", approved ? string.Empty : rejectionReason }
				};

				var emailTemplate = await _emailService.GetRenderedTemplate(templateKey, placeholders);
				if (emailTemplate is null)
				{
					_logger.Warning("NotifyRequester: email template not found - Key: {Key}", templateKey);
					return;
				}

				await _emailService.SendAsync(
					requester.EmailAddress,
					$"{requester.FirstName} {requester.LastName}",
					approved ? "Your lesson has been approved" : "Your lesson requires attention",
					emailTemplate);

				_logger.Information(
					"Requester notified - RequestedBy: {Id}, Approved: {Approved}",
					approval.RequestedBy, approved);
			}
			catch (Exception ex)
			{
				// Fire-and-forget — log but never throw, must not affect the main response
				_logger.Error(ex, "Failed to notify requester - RequestedBy: {Id}", approval.RequestedBy);
			}
		}


		public async Task<BaseResponse> AssignAdminPermissions(AssignAdminPermissionsViewModel model,AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			{
				try
				{
					// ── Parse claims ─────────────────────────────────────────
					if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					{
						_logger.Warning(
							"Invalid SchoolId format - SchoolId: {SchoolId}",
							userClaims.SchoolId);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format",
							Status = "failed"
						};
					}

					if (!Guid.TryParse(userClaims.UserId, out var requestingUserId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId format",
							Status = "failed"
						};

					// ── Verify requesting user is SuperAdministrator ──────────
					var requestingUser = await _queryrepositoryUser.Get(requestingUserId);
					if (requestingUser == null)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "User not found",
							Status = "failed"
						};

					if (requestingUser.RoleId != (int)UserRole.SuperAdministrator)
					{
						_logger.Warning(
							"Unauthorized permission assignment attempt - " +
							"UserId: {UserId}, Role: {Role}",
							requestingUserId,
							((UserRole)requestingUser.RoleId).ToString());

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Only SuperAdministrators can assign admin permissions",
							Status = "failed"
						};
					}

					// ── Validate permission values ────────────────────────────
					var invalidPermissions = model.Permissions.GetInvalidPermissions();
					if (invalidPermissions.Any())
					{
						_logger.Warning(
							"Invalid permission values - InvalidValues: {InvalidValues}",
							string.Join(", ", invalidPermissions));

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Invalid permission values: {string.Join(", ", invalidPermissions)}",
											 
							Status = "failed"
						};
					}

					_logger.Information(
						"Assigning admin permissions - TargetUserId: {TargetUserId}, " +
						"Permissions: [{Permissions}]",
						model.AdminUserId,
						string.Join(", ", model.Permissions));

					// ── Verify target admin exists ────────────────────────────
					var targetAdmin = await _queryrepositoryUser.Get(model.AdminUserId);
					if (targetAdmin == null)
					{
						_logger.Warning(
							"Target admin not found - UserId: {UserId}", model.AdminUserId);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Admin user not found",
							Status = "failed"
						};
					}

					// ── Multi-tenancy check ───────────────────────────────────
					if (targetAdmin.SchoolId != schoolId)
					{
						_logger.Warning(
							"Admin belongs to different school - AdminId: {AdminId}, " +
							"AdminSchoolId: {AdminSchoolId}, RequestSchoolId: {RequestSchoolId}",
							model.AdminUserId, targetAdmin.SchoolId, schoolId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Admin belongs to a different school",
							Status = "failed"
						};
					}

					// ── Verify target user is Administrator or SuperAdministrator
					if (targetAdmin.RoleId != (int)UserRole.Administrator && targetAdmin.RoleId != (int)UserRole.SuperAdministrator)
					{
						_logger.Warning(
							"User is not an admin - UserId: {UserId}, Role: {Role}",
							model.AdminUserId,
							((UserRole)targetAdmin.RoleId).ToString());

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"User {targetAdmin.FirstName} {targetAdmin.LastName} is not an Administrator",
							Status = "failed"
						};
					}

					// ── Verify target admin is active ─────────────────────────
					if (!targetAdmin.IsActive)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Admin {targetAdmin.FirstName} {targetAdmin.LastName} is not active",
							Status = "failed"
						};

					// ── Convert permissions list to enum ──────────────────────
					var permissionsEnum = model.Permissions.ToAdminPermission();
					var permissionsValue = (int)permissionsEnum;

					// ── Check if permissions record already exists ────────────
					var existingPermissions = await GetExistingPermissions(
						model.AdminUserId, schoolId);

					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

					if (existingPermissions != null)
					{
						// Update existing permissions
						var updateDict = new Dictionary<string, object>
						{
							{ "Permissions",  permissionsValue },
							{ "ModifiedDate", now              }
						};

						var whereClause = new KeyValuePair<string, object>("Id", existingPermissions.Id);

						await _adminPermissionsCommandRepository.UpdateTableColumnById(updateDict, whereClause);

						_logger.Information(
							"Admin permissions updated - AdminId: {AdminId}, " +
							"PermissionsValue: {PermissionsValue}, " +
							"Permissions: [{Permissions}]",
							model.AdminUserId, permissionsValue,
							string.Join(", ", model.Permissions));

						return new BaseResponse
						{
							ResponseCode = ResponseCode.successful,
							ResponseMessage = $"Permissions updated successfully for " +
											  $"{targetAdmin.FirstName} {targetAdmin.LastName}",
							Status = "successful",
							Data = new
							{
								AdminId = model.AdminUserId,
								AdminName = $"{targetAdmin.FirstName} {targetAdmin.LastName}",
								PermissionsValue = permissionsValue,
								Permissions = model.Permissions,
								PermissionNames = permissionsEnum.ToPermissionNames()
							}
						};
					}
					else
					{
						// Create new permissions record
						var permissionsDict = new Dictionary<string, object>
						{
							{ "Id",           Guid.NewGuid()      },
							{ "UserId",       model.AdminUserId   },
							{ "SchoolId",     schoolId             },
							{ "Permissions",  permissionsValue     },
							{ "CreationDate", now                  },
							{ "ModifiedDate", now                  },
							{ "CreatedBy",    requestingUserId     },
							{ "IsActive",     true                 }
						};

						await _adminPermissionsCommandRepository.Create(permissionsDict);

						_logger.Information(
							"Admin permissions created - AdminId: {AdminId}, " +
							"PermissionsValue: {PermissionsValue}, " +
							"Permissions: [{Permissions}]",
							model.AdminUserId, permissionsValue,
							string.Join(", ", model.Permissions));

						return new BaseResponse
						{
							ResponseCode = ResponseCode.successful,
							ResponseMessage = $"Permissions assigned successfully to " +
											  $"{targetAdmin.FirstName} {targetAdmin.LastName}",
							Status = "successful",
							Data = new
							{
								AdminId = model.AdminUserId,
								AdminName = $"{targetAdmin.FirstName} {targetAdmin.LastName}",
								PermissionsValue = permissionsValue,
								Permissions = model.Permissions,
								PermissionNames = permissionsEnum.ToPermissionNames()
							}
						};
					}
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Error assigning admin permissions - AdminId: {AdminId}",
						model.AdminUserId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while assigning permissions",
						Status = "failed"
					};
				}
			}
		}


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
		        new Claim(ClaimTypes.Role, ((UserRole)user.RoleId).ToString()),
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


		private string HashPassword(string plainPassword)
		{
			using var sha256 = System.Security.Cryptography.SHA256.Create();

			var bytes = System.Text.Encoding.UTF8.GetBytes(plainPassword);

			var hash = sha256.ComputeHash(bytes);

			return Convert.ToHexString(hash).ToLower();
		}

		/// <summary>
		/// Validate role-specific requirements
		/// </summary>
		private (bool IsValid, string ErrorMessage) ValidateUserRoleRequirements(UserViewModelV2 userViewModel)
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
					if (!userViewModel.UserSubjects.Any())
					{
						return (false, "Teachers must be assigned to at least one subject");
					}
					if (userViewModel.LineManagerId is null)
						return (false, "A line manager must be assigned for Subject Teachers");
					break;
				case UserRole.ClassTeacher:
					
					if (userViewModel.LineManagerId is null)
						return (false, "A line manager must be assigned for Subject Teachers");
					break;
			case UserRole.HeadTeacher:
				//	if (!userViewModel.UserClassroomsId.Any())
				//	{
				//		return (false, "Teachers must be assigned to at least one classroom");
				//	}
				//	if (!userViewModel.UserSubjects.Any())
				//	{
				//		return (false, "Teachers must be assigned to at least one subject");
				//	}
				//	break;

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
				var emailCondition = string.IsNullOrWhiteSpace(email)
					? "0=1"
					: "LOWER(EmailAddress) = @Email";

				var query = $"SELECT COUNT(*) FROM Users WHERE (LOWER(UserName) = @UserName OR {emailCondition}) AND SchoolId = @SchoolId";
				var parameters = new Dictionary<string, object>
				{
					{ "UserName", userName.Trim().ToLower() },
					{ "SchoolId", schoolId }
				};

				if (!string.IsNullOrWhiteSpace(email))
					parameters.Add("Email", email.Trim().ToLower());

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
		private async Task<(bool Exists, string Message)> CheckStudentExists(string userName, string email, Guid schoolId)
		{
			try
			{
				var emailCondition = string.IsNullOrWhiteSpace(email)
					? "0=1"
					: "LOWER(EmailAddress) = @Email";

				var query = $"SELECT COUNT(*) FROM Users WHERE (LOWER(UserName) = @UserName OR {emailCondition}) AND SchoolId = @SchoolId";
				var parameters = new Dictionary<string, object>
				{
					{ "UserName", userName.Trim().ToLower() },
					{ "SchoolId", schoolId }
				};

				if (!string.IsNullOrWhiteSpace(email))
					parameters.Add("Email", email.Trim().ToLower());

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
		private async Task CreateStudentAssociations(IDbTransactionScope scope,Guid studentId,UserViewModelV2 userViewModel,Guid schoolId,Guid createdBy)
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
		private async Task CreateTeacherAssociations(IDbTransactionScope scope,Guid teacherId,UserViewModelV2 userViewModel,Guid schoolId,Guid createdBy)
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

			// Soft-delete existing active teacher-subject records to avoid
			// UC_TeacherSubject_UniqueActive violations from orphan / retry records
			var cleanSql = $@"
				UPDATE TeacherSubject
				SET    IsActive     = 0,
				       ModifiedDate = '{now}'
				WHERE  TeacherId    = '{teacherId}'
				AND    IsActive     = 1";
			await scope.Connection.ExecuteAsync(cleanSql, transaction: scope.Transaction);

			// Create teacher-subject associations
			if (userViewModel.UserSubjectClassrooms.Any())
			{
				var teacherSubjects = userViewModel.UserSubjectClassrooms.Select(assignment => new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "TeacherId", teacherId },
						{ "SubjectId", assignment.SubjectId },
						{ "ClassroomId", assignment.ClassroomId },
						{ "SchoolId", schoolId },
						{ "IsActive", true },
						{ "CreatedBy", createdBy }
					}).ToList();

				await _commandRepositoryTeacherSubject.CreateBatchAsync(scope.Transaction, scope.Connection, teacherSubjects);
			}
			else if (userViewModel.UserSubjects.Any())
			{
				var teacherSubjects = userViewModel.UserSubjects.Select(subjectId => new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "TeacherId", teacherId },
						{ "SubjectId", subjectId },
						{ "ClassroomId", DBNull.Value },
						{ "SchoolId", schoolId },
						{ "IsActive", true },
						{ "CreatedBy", createdBy }
					}).ToList();

				await _commandRepositoryTeacherSubject.CreateBatchAsync(scope.Transaction, scope.Connection, teacherSubjects);
			}
		}

		#region AssignAdminPermissions

		/// <summary>
		/// Assign or update permissions for an admin user
		/// Flow:
		/// 1. Validate requesting user is SuperAdmin
		/// 2. Validate target user is Admin/SuperAdmin
		/// 3. Validate permission values are valid
		/// 4. Convert List<int> to single int using bitwise OR
		/// 5. Store in database
		/// </summary>
		//public async Task<BaseResponse> AssignTeacherToClassroom(AssignTeacherToClassroomViewModel model,AuthenticatedUserClaims userClaims)
		//{
		//	using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
		//	{
		//		try
		//		{
		//			// ===== PARSE CLAIMS =====

		//			if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.BadRequest,
		//					ResponseMessage = "Invalid SchoolId format in token",
		//					Status = "failed"
		//				};

		//			if (!Guid.TryParse(userClaims.UserId, out var requestingUserId))
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.BadRequest,
		//					ResponseMessage = "Invalid UserId format in token",
		//					Status = "failed"
		//				};

		//			if (!Enum.TryParse<UserRole>(userClaims.Role, ignoreCase: true,
		//				out UserRole userRole))
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.BadRequest,
		//					ResponseMessage = "Invalid role format in token",
		//					Status = "failed"
		//				};

		//			// ===== AUTHORIZATION =====

		//			if (userRole == UserRole.Administrator)
		//			{
		//				var hasPermission = await this.HasPermission(
		//					requestingUserId,
		//					schoolId,
		//					AdminPermission.ManageLessons);

		//				if (!hasPermission)
		//				{
		//					_logger.Warning(
		//						"Admin lacks ManageLessons permission - AdminId: {AdminId}",
		//						requestingUserId);
		//					return new BaseResponse
		//					{
		//						ResponseCode = ResponseCode.Forbidden,
		//						ResponseMessage = "You don't have permission to assign teachers to classrooms.",
		//						Status = "failed"
		//					};
		//				}
		//			}
		//			else if (userRole != UserRole.SuperAdministrator)
		//			{
		//				_logger.Warning(
		//					"Unauthorized classroom assignment attempt - UserId: {UserId}, Role: {Role}",
		//					requestingUserId, userRole.ToString());
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.Forbidden,
		//					ResponseMessage = "You are not authorized to assign teachers to classrooms",
		//					Status = "failed"
		//				};
		//			}

		//			// ===== VALIDATE TEACHER =====

		//			var teacher = await _queryrepositoryUser.Get(model.TeacherId);
		//			if (teacher is null)
		//			{
		//				_logger.Warning(
		//					"Teacher not found - TeacherId: {TeacherId}", model.TeacherId);
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.NotFound,
		//					ResponseMessage = "Teacher not found",
		//					Status = "failed"
		//				};
		//			}

		//			if (teacher.SchoolId != schoolId)
		//			{
		//				_logger.Warning(
		//					"Cross-school assignment attempt - TeacherId: {TeacherId}, " +
		//					"TeacherSchool: {TeacherSchool}, RequestSchool: {RequestSchool}",
		//					model.TeacherId, teacher.SchoolId, schoolId);
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.Forbidden,
		//					ResponseMessage = "Teacher belongs to a different school",
		//					Status = "failed"
		//				};
		//			}

		//			if (!teacher.IsActive)
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.BadRequest,
		//					ResponseMessage = "Cannot assign an inactive teacher to a classroom",
		//					Status = "failed"
		//				};

		//			// ── Role check ───────────────────────────────────────────
		//			// ClassTeacher  → only one classroom at a time
		//			// HeadTeacher   → can have multiple classrooms
		//			// Administrator → cannot be assigned to a classroom
		//			var teacherRole = (UserRole)teacher.RoleId;

		//			if (teacherRole != UserRole.ClassTeacher && teacherRole != UserRole.HeadTeacher)
		//			{
		//				_logger.Warning(
		//					"User is not a ClassTeacher or HeadTeacher - " +
		//					"UserId: {UserId}, Role: {Role}",
		//					model.TeacherId, teacherRole.ToString());
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.BadRequest,
		//					ResponseMessage = "User is not a Class Teacher or Head Teacher",
		//					Status = "failed"
		//				};
		//			}

		//			// ===== VALIDATE CLASSROOM =====

		//			var classroom = await _teacherClassroomQueryRespository.Get(model.ClassroomId);

		//			if (classroom is null)
		//			{
		//				_logger.Warning(
		//					"Classroom not found - ClassroomId: {ClassroomId}",
		//					model.ClassroomId);
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.NotFound,
		//					ResponseMessage = "Classroom not found",
		//					Status = "failed"
		//				};
		//			}

		//			if (classroom.SchoolId != schoolId)
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.Forbidden,
		//					ResponseMessage = "Classroom belongs to a different school",
		//					Status = "failed"
		//				};

		//			// ===== CLASS TEACHER — ONE CLASSROOM ONLY =====

		//			var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
		//			Guid? previousClassroomId = null;

		//			if (teacherRole == UserRole.ClassTeacher)
		//			{
		//				// ClassTeacher can only have one active classroom
		//				// Soft delete existing assignment before creating new one
		//				var existingAssignment = await _teacherClassroomQueryRespository.GetBy(new Dictionary<string, object>
		//					{
		//						{ "TeacherId", model.TeacherId },
		//						{ "SchoolId",  schoolId        },
		//						{ "IsActive",  true            }
		//					});

		//				if (existingAssignment is not null)
		//				{
		//					if (existingAssignment.ClassroomId == model.ClassroomId)
		//						return new BaseResponse
		//						{
		//							ResponseCode = ResponseCode.BadRequest,
		//							ResponseMessage = "Teacher is already assigned to this classroom",
		//							Status = "failed"
		//						};

		//					// Soft delete existing assignment — preserves history
		//					var deactivateDict = new Dictionary<string, object>
		//					{
		//						{ "IsActive",    false },
		//						{ "ModifiedDate", now  }
		//					};

		//					var whereClause = new KeyValuePair<string, object>(
		//						"Id", existingAssignment.Id);

		//					await _commandRepositoryTeacherClassroom.UpdateTableColumnById(deactivateDict, whereClause);

		//					previousClassroomId = existingAssignment.ClassroomId;

		//					_logger.Information(
		//						"Deactivated previous classroom assignment - " +
		//						"TeacherId: {TeacherId}, OldClassroomId: {OldClassroomId}",
		//						model.TeacherId, existingAssignment.ClassroomId);
		//				}
		//			}
		//			else if (teacherRole == UserRole.HeadTeacher)
		//			{
		//				// HeadTeacher — assign multiple classrooms at once
		//				// Use ClassroomIds list if provided, fall back to single ClassroomId
		//				var classroomIds = model.ClassroomIds?.Any() == true ? model.ClassroomIds : new List<Guid> { model.ClassroomId };

		//				if (!classroomIds.Any())
		//					return new BaseResponse
		//					{
		//						ResponseCode = ResponseCode.BadRequest,
		//						ResponseMessage = "At least one classroom is required",
		//						Status = "failed"
		//					};

		//				foreach (var cId in classroomIds)
		//				{
		//					// Verify each classroom exists and belongs to school
		//					var cls = await _teacherClassroomQueryRespository.Get(cId);
		//					if (cls == null || cls.SchoolId != schoolId)
		//						return new BaseResponse
		//						{
		//							ResponseCode = ResponseCode.NotFound,
		//							ResponseMessage = $"Classroom {cId} not found",
		//							Status = "failed"
		//						};

		//					// Check for duplicate — skip if already assigned
		//					var existing = await _teacherClassroomQueryRespository
		//						.GetBy(new Dictionary<string, object>
		//						{
		//							{ "TeacherId",   model.TeacherId },
		//							{ "ClassroomId", cId             },
		//							{ "SchoolId",    schoolId         },
		//							{ "IsActive",    true             }
		//						});

		//					if (existing is not null)
		//					{
		//						_logger.Information(
		//							"HeadTeacher already assigned to classroom - skipping. " +
		//							"TeacherId: {TeacherId}, ClassroomId: {ClassroomId}",
		//							model.TeacherId, cId);
		//						continue;  // skip duplicate, do not error
		//					}

		//					var newAssignment2 = new Dictionary<string, object>
		//					{
		//						{ "Id",           Guid.NewGuid()  },
		//						{ "TeacherId",    model.TeacherId  },
		//						{ "ClassroomId",  cId              },
		//						{ "SchoolId",     schoolId         },
		//						{ "IsPrimary",    model.IsPrimary  },
		//						{ "IsActive",     true             },
		//						{ "CreatedBy",    requestingUserId },
		//						{ "CreationDate", now              },
		//						{ "ModifiedDate", now              }
		//					};

		//					await _commandRepositoryTeacherClassroom.Create(newAssignment2);

		//					_logger.Information(
		//						"HeadTeacher assigned to classroom - " +
		//						"TeacherId: {TeacherId}, ClassroomId: {ClassroomId}",
		//						model.TeacherId, cId);
		//				}

		//				// Return early for HeadTeacher
		//				return new BaseResponse
		//				{
		//					ResponseCode = ResponseCode.successful,
		//					ResponseMessage = $"{teacher.FirstName} {teacher.LastName} has been assigned to classrooms successfully",
		//					Status = "successful",
		//					Data = new
		//					{
		//						TeacherId = model.TeacherId,
		//						TeacherName = $"{teacher.FirstName} {teacher.LastName}",
		//						TeacherRole = teacherRole.ToString(),
		//						ClassroomIds = classroomIds,
		//						AssignedBy = requestingUserId,
		//						AssignedAt = now
		//					}
		//				};
		//			}

		//			// ===== INSERT NEW ASSIGNMENT =====

		//			var newAssignment = new Dictionary<string, object>
		//			{
		//				{ "Id",           Guid.NewGuid()      },
		//				{ "TeacherId",    model.TeacherId      },
		//				{ "ClassroomId",  model.ClassroomId   },
		//				{ "SchoolId",     schoolId             },
		//				{ "IsPrimary",    model.IsPrimary      },
		//				{ "IsActive",     true                 },
		//				{ "CreatedBy",    requestingUserId     },
		//				{ "CreationDate", now                  },
		//				{ "ModifiedDate", now                  }
		//			};

		//			await _commandRepositoryTeacherClassroom.Create(newAssignment);

		//			_logger.Information(
		//				"Teacher assigned to classroom - TeacherId: {TeacherId}, " +
		//				"ClassroomId: {ClassroomId}, Role: {Role}, " +
		//				"IsPrimary: {IsPrimary}, AssignedBy: {AssignedBy}",
		//				model.TeacherId, model.ClassroomId,
		//				teacherRole.ToString(), model.IsPrimary, requestingUserId);

		//			return new BaseResponse
		//			{
		//				ResponseCode = ResponseCode.successful,
		//				ResponseMessage = $"{teacher.FirstName} {teacher.LastName} has been assigned to the classroom successfully",
		//				Status = "successful",
		//				Data = new
		//				{
		//					TeacherId = model.TeacherId,
		//					TeacherName = $"{teacher.FirstName} {teacher.LastName}",
		//					TeacherRole = teacherRole.ToString(),
		//					ClassroomId = model.ClassroomId,
		//					IsPrimary = model.IsPrimary,
		//					PreviousClassroomId = previousClassroomId,
		//					AssignedBy = requestingUserId,
		//					AssignedAt = now
		//				}
		//			};
		//		}
		//		catch (Exception ex)
		//		{
		//			_logger.Error(ex,
		//				"Error assigning teacher to classroom - " +
		//				"TeacherId: {TeacherId}, ClassroomId: {ClassroomId}",
		//				model.TeacherId, model.ClassroomId);
		//			return new BaseResponse
		//			{
		//				ResponseCode = ResponseCode.ErrorOccured,
		//				ResponseMessage = "An unexpected error occurred while assigning teacher to classroom",
		//				Status = "failed"
		//			};
		//		}
		//	}
		//}


		public async Task<BaseResponse> AssignTeacherToClassroom(AssignTeacherToClassroomViewModel model,AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			{
				try
				{
					// ===== PARSE CLAIMS =====

					if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format in token",
							Status = "failed"
						};

					if (!Guid.TryParse(userClaims.UserId, out var requestingUserId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId format in token",
							Status = "failed"
						};

					if (!Enum.TryParse<UserRole>(userClaims.Role, ignoreCase: true,
						out UserRole userRole))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid role format in token",
							Status = "failed"
						};

					// ===== AUTHORIZATION =====

					if (userRole == UserRole.Administrator)
					{
						var hasPermission = await this.HasPermission(
							requestingUserId,
							schoolId,
							AdminPermission.ManageLessons);

						if (!hasPermission)
						{
							_logger.Warning(
								"Admin lacks ManageLessons permission - AdminId: {AdminId}",
								requestingUserId);
							return new BaseResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = "You don't have permission to assign teachers to classrooms.",
								Status = "failed"
							};
						}
					}
					else if (userRole != UserRole.SuperAdministrator)
					{
						_logger.Warning(
							"Unauthorized classroom assignment attempt - UserId: {UserId}, Role: {Role}",
							requestingUserId, userRole.ToString());
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You are not authorized to assign teachers to classrooms",
							Status = "failed"
						};
					}

					// ===== VALIDATE TEACHER =====

					var teacher = await _queryrepositoryUser.Get(model.TeacherId);
					if (teacher is null)
					{
						_logger.Warning(
							"Teacher not found - TeacherId: {TeacherId}", model.TeacherId);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Teacher not found",
							Status = "failed"
						};
					}

					if (teacher.SchoolId != schoolId)
					{
						_logger.Warning(
							"Cross-school assignment attempt - TeacherId: {TeacherId}, " +
							"TeacherSchool: {TeacherSchool}, RequestSchool: {RequestSchool}",
							model.TeacherId, teacher.SchoolId, schoolId);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Teacher belongs to a different school",
							Status = "failed"
						};
					}

					if (!teacher.IsActive)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Cannot assign an inactive teacher to a classroom",
							Status = "failed"
						};

					// ── Role check ───────────────────────────────────────────
					// ClassTeacher  → only one classroom at a time
					// HeadTeacher   → can have multiple classrooms
					// Administrator → cannot be assigned to a classroom
					var teacherRole = (UserRole)teacher.RoleId;

					if (teacherRole != UserRole.ClassTeacher && teacherRole != UserRole.HeadTeacher)
					{
						_logger.Warning(
							"User is not a ClassTeacher or HeadTeacher - " +
							"UserId: {UserId}, Role: {Role}",
							model.TeacherId, teacherRole.ToString());
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "User is not a Class Teacher or Head Teacher",
							Status = "failed"
						};
					}

					// ===== VALIDATE CLASSROOM =====

					var classroom = await _classroomQueryRespository.Get(model.ClassroomId);

					if (classroom is null)
					{
						_logger.Warning(
							"Classroom not found - ClassroomId: {ClassroomId}",
							model.ClassroomId);
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Classroom not found",
							Status = "failed"
						};
					}

					if (classroom.SchoolId != schoolId)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Classroom belongs to a different school",
							Status = "failed"
						};

					// ===== CLASS TEACHER — ONE CLASSROOM ONLY =====

					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
					Guid? previousClassroomId = null;

					if (teacherRole == UserRole.ClassTeacher)
					{
						// ClassTeacher can only have one active classroom
						// Soft delete existing assignment before creating new one
						var existingAssignment = await _teacherClassroomQueryRespository.GetBy(new Dictionary<string, object>
							{
								{ "TeacherId", model.TeacherId },
								{ "SchoolId",  schoolId        },
								{ "IsActive",  true            }
							});

						if (existingAssignment is not null)
						{
							if (existingAssignment.ClassroomId == model.ClassroomId)
								return new BaseResponse
								{
									ResponseCode = ResponseCode.BadRequest,
									ResponseMessage = "Teacher is already assigned to this classroom",
									Status = "failed"
								};

							// Soft delete existing assignment — preserves history
							var deactivateDict = new Dictionary<string, object>
							{
								{ "IsActive",    false },
								{ "ModifiedDate", now  }
							};

							var whereClause = new KeyValuePair<string, object>(
								"Id", existingAssignment.Id);

							await _commandRepositoryTeacherClassroom
								.UpdateTableColumnById(deactivateDict, whereClause);

							previousClassroomId = existingAssignment.ClassroomId;

							_logger.Information(
								"Deactivated previous classroom assignment - " +
								"TeacherId: {TeacherId}, OldClassroomId: {OldClassroomId}",
								model.TeacherId, existingAssignment.ClassroomId);
						}
					}
					else if (teacherRole == UserRole.HeadTeacher)
					{
						// HeadTeacher can have multiple classrooms
						// Only check for duplicate — do not deactivate existing
						var existingAssignment = await _teacherClassroomQueryRespository.GetBy(new Dictionary<string, object>
							{
								{ "TeacherId",  model.TeacherId   },
								{ "ClassroomId", model.ClassroomId },
								{ "SchoolId",   schoolId           },
								{ "IsActive",   true               }
							});

						if (existingAssignment is not null)
							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = "Head Teacher is already assigned to this classroom",
								Status = "failed"
							};
					}

					// ===== INSERT NEW ASSIGNMENT =====

					var newAssignment = new Dictionary<string, object>
					{
						{ "Id",           Guid.NewGuid()      },
						{ "TeacherId",    model.TeacherId      },
						{ "ClassroomId",  model.ClassroomId   },
						{ "SchoolId",     schoolId             },
						{ "IsPrimary",    model.IsPrimary      },
						{ "IsActive",     true                 },
						{ "CreatedBy",    requestingUserId     },
						{ "CreationDate", now                  },
						{ "ModifiedDate", now                  }
					};

					await _commandRepositoryTeacherClassroom.Create(newAssignment);

					_logger.Information(
						"Teacher assigned to classroom - TeacherId: {TeacherId}, " +
						"ClassroomId: {ClassroomId}, Role: {Role}, " +
						"IsPrimary: {IsPrimary}, AssignedBy: {AssignedBy}",
						model.TeacherId, model.ClassroomId,
						teacherRole.ToString(), model.IsPrimary, requestingUserId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = $"{teacher.FirstName} {teacher.LastName} has been assigned to the classroom successfully",
						Status = "successful",
						Data = new
						{
							TeacherId = model.TeacherId,
							TeacherName = $"{teacher.FirstName} {teacher.LastName}",
							TeacherRole = teacherRole.ToString(),
							ClassroomId = model.ClassroomId,
							IsPrimary = model.IsPrimary,
							PreviousClassroomId = previousClassroomId,
							AssignedBy = requestingUserId,
							AssignedAt = now
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Error assigning teacher to classroom - " +"TeacherId: {TeacherId}, ClassroomId: {ClassroomId}",
						model.TeacherId, model.ClassroomId);
					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An unexpected error occurred while assigning teacher to classroom",
						Status = "failed"
					};
				}
			}
		}

		#endregion

		public async Task<BaseResponse> UpdateTeacherSubject(Guid teacherId, UpdateTeacherSubjectViewModel model, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims.UserId, out var requesterId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Unauthorised access",
						Status = "failed"
					};

				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Unauthorised school access",
						Status = "failed"
					};

				if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var requesterRole))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid role in token",
						Status = "failed"
					};

				// ── Authorization ─────────────────────────────────────────────
				if (requesterRole == UserRole.Administrator)
				{
					var hasPermission = await this.HasPermission(
						requesterId, schoolId, AdminPermission.ManageTeachers);

					if (!hasPermission)
					{
						_logger.Warning(
							"Admin lacks ManageTeachers permission - AdminId: {AdminId}",
							requesterId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You do not have permission to update teacher subjects",
							Status = "failed"
						};
					}
				}
				else if (requesterRole != UserRole.SuperAdministrator && requesterRole != UserRole.HeadTeacher)
				{
					_logger.Warning(
						"Unauthorized teacher subject update attempt - UserId: {UserId}, Role: {Role}",
						requesterId, requesterRole.ToString());

					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You are not authorized to update teacher subjects",
						Status = "failed"
					};
				}

				// ── Validate model ────────────────────────────────────────────
				if (model.ClassroomId == Guid.Empty)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Classroom is required",
						Status = "failed"
					};

				// ── Verify teacher exists and belongs to this school ──────────
				var teacher = await _queryrepositoryUser.Get(teacherId);
				if (teacher == null || teacher.SchoolId != schoolId)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Teacher not found",
						Status = "failed"
					};

				if (teacher.RoleId != (int)UserRole.SubjectTeacher)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "User is not a subject teacher",
						Status = "failed"
					};

				if (!teacher.IsActive)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"Teacher {teacher.FirstName} {teacher.LastName} is not active",
						Status = "failed"
					};

				// ── Verify classroom belongs to school ────────────────────────
				var classroom = await _classroomQueryRespository.Get(model.ClassroomId);
				if (classroom == null || classroom.SchoolId != schoolId)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Classroom not found",
						Status = "failed"
					};

				// ── Verify all incoming subjects exist and belong to school ───
				foreach (var subjectId in model.SubjectIds)
				{
					var subject = await _subjectQueryRespository.Get(subjectId);
					if (subject == null || subject.SchoolId != schoolId)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = $"Subject {subjectId} not found",
							Status = "failed"
						};
				}

				// ── Fetch current active assignments ──────────────────────────
				var currentQuery = $@"
					SELECT SubjectId FROM TeacherSubject
					WHERE  TeacherId   = '{teacherId}'
					AND    SchoolId    = '{schoolId}'
					AND    IsActive    = 1";

				var currentRows = await _teacherSubjectQueryRespository
					.QueryAsync<TeacherSubjectIdRow>(currentQuery, new Dictionary<string, object>());

				var currentSubjectIds = currentRows.Select(r => r.SubjectId).ToHashSet();
				var incomingSubjectIds = model.SubjectIds.ToHashSet();

				// ── Diff ──────────────────────────────────────────────────────
				var toAdd = incomingSubjectIds.Except(currentSubjectIds).ToList();
				var toRemove = currentSubjectIds.Except(incomingSubjectIds).ToList();

				if (!toAdd.Any() && !toRemove.Any())
					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "No changes detected — subjects are already up to date",
						Status = "successful"
					};

				var nowStr = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				try
				{
					// ── Soft delete removed subjects ──────────────────────────
					foreach (var subjectId in toRemove)
					{
						var softDelete = $@"
							UPDATE TeacherSubject
							SET    IsActive     = 0,
								   ModifiedDate = '{nowStr}'
							WHERE  TeacherId   = '{teacherId}'
							AND    SubjectId   = '{subjectId}'
							AND    SchoolId    = '{schoolId}'
							AND    IsActive    = 1";

						await scope.Connection.ExecuteAsync(softDelete, transaction: scope.Transaction);
					}

					// ── Insert new subjects ───────────────────────────────────
					foreach (var subjectId in toAdd)
					{
						var insertDict = new Dictionary<string, object>
						{
							{ "Id",           Guid.NewGuid()    },
							{ "TeacherId",    teacherId          },
							{ "SubjectId",    subjectId          },
							//{ "ClassroomId",  model.ClassroomId },
							{ "SchoolId",     schoolId           },
							{ "CreatedBy",    requesterId        },
							{ "CreationDate", nowStr             },
							{ "ModifiedDate", nowStr             },
							{ "IsActive",     true               }
						};

						await _commandRepositoryTeacherSubject.Create(scope.Transaction, scope.Connection, insertDict);
					}

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Rolling back teacher subject update - TeacherId: {TeacherId}",
						teacherId);
					try { await scope.RollbackAsync(); } catch { }
					throw;
				}

				_logger.Information(
					"Teacher subjects updated - TeacherId: {TeacherId}, ClassroomId: {ClassroomId}, " +
					"Added: {Added}, Removed: {Removed}, UpdatedBy: {RequesterId}",
					teacherId, model.ClassroomId, toAdd.Count, toRemove.Count, requesterId);

				// ── Fetch updated assignments to return ───────────────────────
				//var updatedQuery = $@"
				//	SELECT
				//		ts.SubjectId,
				//		s.Subject  AS SubjectName,
				//		ts.ClassroomId,
				//		c.Name     AS ClassName
				//	FROM   TeacherSubject ts
				//	JOIN   Subjects        s ON s.Id = ts.SubjectId
				//	JOIN   Classroom       c ON c.Id = ts.ClassroomId
				//	WHERE  ts.TeacherId   = '{teacherId}'
				//	AND    ts.ClassroomId = '{model.ClassroomId}'
				//	AND    ts.SchoolId    = '{schoolId}'
				//	AND    ts.IsActive    = 1";

				var updatedQuery = $@"
					SELECT
						ts.SubjectId,
						s.Subject  AS SubjectName,
					FROM   TeacherSubject ts
					JOIN   Subjects        s ON s.Id = ts.SubjectId
					WHERE  ts.TeacherId   = '{teacherId}'
					AND    ts.SchoolId    = '{schoolId}'
					AND    ts.IsActive    = 1";

				var updated = await _teacherSubjectQueryRespository.QueryAsync<TeacherSubjectRow>(updatedQuery, new Dictionary<string, object>());

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Teacher subjects updated successfully",
					Status = "successful",
					Data = new
					{
						TeacherId = teacherId,
						TeacherName = $"{teacher.FirstName} {teacher.LastName}",
						ClassroomId = model.ClassroomId,
						ClassName = classroom.Name,
						Added = toAdd.Count,
						Removed = toRemove.Count,
						Subjects = updated.ToList()
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Error updating teacher subjects - TeacherId: {TeacherId}",
					teacherId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while updating teacher subjects",
					Status = "failed"
				};
			}
		}

		#region GetAdminPermissions

		/// <summary>
		/// Get permissions for a specific admin
		/// Converts database int value back to List<int> for API response
		/// </summary>
		public async Task<BaseResponse> GetAdminPermissions(
			Guid adminUserId,
			AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			//using (LogContext.PushProperty("TenantId", userClaims.TenantIdentifier))
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

					var admin = await _queryrepositoryUser.Get(adminUserId);
					if (admin == null)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Admin user not found",
							Status = "failed"
						};
					}

					if (admin.SchoolId != schoolId)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Admin belongs to a different school",
							Status = "failed"
						};
					}

					var permissions = await GetExistingPermissions(adminUserId, schoolId);

					if (permissions == null)
					{
						// No permissions assigned - return defaults (all false)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.successful,
							ResponseMessage = "No permissions assigned (using defaults)",
							Status = "successful",
							Data = new AdminPermissionsDto
							{
								UserId = adminUserId,
								UserName = $"{admin.FirstName} {admin.LastName}",
								Email = admin.EmailAddress ?? string.Empty,
								RoleName = ((UserRole)admin.RoleId).ToString(),
								PermissionsValue = 0,
								Permissions = new List<int>(),
								//PermissionNames = new List<string>()
							}
						};
					}

					// Convert database int to enum, then to list
					// Example: 19 → (AdminPermission)19 → [1, 2, 16]
					var permissionsEnum = (AdminPermission)permissions.Permissions;
					var permissionsList = permissionsEnum.ToPermissionList();
					var permissionNames = permissionsList.ToPermissionNames();

					var creator = await _queryrepositoryUser.Get(permissions.CreatedBy);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Admin permissions retrieved successfully",
						Status = "successful",
						Data = new AdminPermissionsDto
						{
							Id = permissions.Id,
							UserId = adminUserId,
							UserName = $"{admin.FirstName} {admin.LastName}",
							Email = admin.EmailAddress ?? string.Empty,
							RoleName = ((UserRole)admin.RoleId).ToString(),
							PermissionsValue = permissions.Permissions,
							Permissions = permissionsList,
							//PermissionNames = permissionNames,
							CreationDate = permissions.CreationDate ?? string.Empty,
							ModifiedDate = permissions.ModifiedDate ?? string.Empty,
							CreatedByName = creator != null ? $"{creator.FirstName} {creator.LastName}" : string.Empty
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error fetching admin permissions - AdminId: {AdminId}", adminUserId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching permissions",
						Status = "failed"
					};
				}
			}
		}

		#endregion

		#region GetAllAdminPermissions

		public async Task<BaseResponse> GetLessonsBySubjectForStudent(Guid subjectId, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims.UserId, out var studentId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};

				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};

				// Verify student is enrolled in this subject
				var minorEnrollmentQuery = $@"
					SELECT TOP 1 Id FROM StudentMinorSubject
					WHERE  StudentId = '{studentId}'
					AND    SubjectId = '{subjectId}'
					AND    SchoolId  = '{schoolId}'";

				var minorEnrolled = await _studentMinorSubjectQueryRespository.Get(minorEnrollmentQuery);
				if (minorEnrolled is null)
				{
					var coreEnrollmentQuery = $@"
						SELECT TOP 1 cs.Id 
						FROM   ClassroomSubject cs
						JOIN   StudentClassroom sc ON sc.ClassroomId = cs.ClassroomId
						WHERE  sc.StudentId = '{studentId}'
						AND    cs.SubjectId = '{subjectId}'
						AND    cs.SchoolId  = '{schoolId}'
						AND    sc.IsActive  = 1";

					var coreEnrolled = await _classroomSubjectQueryRespository.Get(coreEnrollmentQuery);

					if (coreEnrolled is null)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "You are not enrolled in this subject",
							Status = "failed"
						};
				}

				var sql = $@"
					SELECT
						lc.Id,
						lc.Aim,
						lc.Description,
						lc.SubTopic,
						lc.ApprovedAt,
						u.FirstName + ' ' + u.LastName AS TeacherName,
						s.Id   AS SubjectId,
						s.Subject AS SubjectName,
						t.Id   AS TopicId,
						t.Name AS TopicName,
						(SELECT COUNT(*) FROM LessonMedia lm
						 WHERE  lm.LessonContentId = lc.Id
						 AND    lm.IsActive = 1) AS MediaCount
					FROM   LessonContent lc
					JOIN   Subjects      s  ON s.Id = lc.SubjectId
					JOIN   Topic         t  ON t.Id = lc.TopicId
					JOIN   Users         u  ON u.Id = lc.CreatedBy
					WHERE  lc.SubjectId = '{subjectId}'
					AND    lc.SchoolId  = '{schoolId}'
					AND    lc.Status    IN ('{LessonStatus.Published}', '{LessonStatus.Approved}')
					ORDER  BY lc.ApprovedAt DESC";

				var rows = await _studentMinorSubjectQueryRespository.QueryAsync<StudentSubjectLessonDto>(sql, new Dictionary<string, object>());

				var lessons = rows.ToList();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,ResponseMessage = lessons.Any()
						? $"{lessons.Count} lesson(s) found"
						: "No lessons found for this subject",
					Status = "success",
					Data = lessons
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Error fetching lessons by subject for student - SubjectId: {SubjectId}",
					subjectId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching lessons",
					Status = "failed"
				};
			}
		}

		/// <summary>
		/// Get all admin permissions for the school (paginated)
		/// </summary>
		public async Task<BaseResponse> GetAllAdminPermissions(AuthenticatedUserClaims userClaims,int pageNumber = 1,int pageSize = 50)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			//using (LogContext.PushProperty("TenantId", userClaims.TenantIdentifier))
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

					// Validate pagination
					if (pageNumber < 1) pageNumber = 1;
					if (pageSize < 1 || pageSize > 100) pageSize = 50;

					_logger.Information("Fetching all admin permissions - SchoolId: {SchoolId}", schoolId);

					// Get all active permissions for school
					var query = $@"
                        SELECT * FROM AdminPermissions 
                        WHERE SchoolId = '{schoolId}' 
                        AND IsActive = 1
                        ORDER BY CreationDate DESC";

					var allPermissions = await _adminPermissionsQueryRespository.GetByQuery(query);
					var permissionsList = allPermissions.Where(p => p != null).ToList();

					// Pagination
					var totalCount = permissionsList.Count;
					var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

					var paginatedPermissions = permissionsList
						.Skip((pageNumber - 1) * pageSize)
						.Take(pageSize)
						.ToList();

					// Map to DTOs
					var permissionDtos = new List<AdminPermissionsDto>();

					foreach (var permission in paginatedPermissions)
					{
						if (permission == null) continue;

						var admin = await _queryrepositoryUser.Get(permission.UserId);
						if (admin == null) continue;

						var permissionsEnum = (AdminPermission)permission.Permissions;
						var permissionList = permissionsEnum.ToPermissionList();
						var permissionNames = permissionList.ToPermissionNames();

						var creator = await _queryrepositoryUser.Get(permission.CreatedBy);

						permissionDtos.Add(new AdminPermissionsDto
						{
							Id = permission.Id,
							UserId = permission.UserId,
							UserName = $"{admin.FirstName} {admin.LastName}",
							Email = admin.EmailAddress ?? string.Empty,
							RoleName = ((UserRole)admin.RoleId).ToString(),
							PermissionsValue = permission.Permissions,
							Permissions = permissionList,
							//PermissionNames = permissionNames,
							CreationDate = permission.CreationDate ?? string.Empty,
							ModifiedDate = permission.ModifiedDate ?? string.Empty,
							CreatedByName = creator != null ? $"{creator.FirstName} {creator.LastName}" : string.Empty
						});
					}

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Admin permissions retrieved successfully",
						Status = "successful",
						Data = new 
						{
							AdminPermissions = permissionDtos,
							TotalCount = totalCount,
							PageNumber = pageNumber,
							PageSize = pageSize,
							TotalPages = totalPages,
							HasPreviousPage = pageNumber > 1,
							HasNextPage = pageNumber < totalPages
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error fetching all admin permissions");

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching admin permissions",
						Status = "failed"
					};
				}
			}
		}

		#endregion

		#region RevokeAdminPermissions

		/// <summary>
		/// Revoke all permissions from an admin (soft delete)
		/// </summary>
		public async Task<BaseResponse> RevokeAdminPermissions(Guid adminUserId,AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			//using (LogContext.PushProperty("TenantId", userClaims.TenantIdentifier))
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

					if (!Guid.TryParse(userClaims.UserId, out var requestingUserId))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId format",
							Status = "failed"
						};
					}

					// Check requesting user is SuperAdmin
					var requestingUser = await _queryrepositoryUser.Get(requestingUserId);
					if (requestingUser?.RoleId != (int)UserRole.SuperAdministrator)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Only SuperAdministrators can revoke permissions",
							Status = "failed"
						};
					}

					_logger.Information("Revoking admin permissions - AdminId: {AdminId}", adminUserId);

					var permissions = await GetExistingPermissions(adminUserId, schoolId);

					if (permissions == null)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "No permissions found for this admin",
							Status = "failed"
						};
					}

					// Soft delete
					var updateDict = new Dictionary<string, object>
					{
						{ "IsActive", false },
						{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
					};

					var whereClause = new KeyValuePair<string, object>("Id", permissions.Id);
					await _adminPermissionsCommandRepository.UpdateTableColumnById(updateDict, whereClause);

					_logger.Information("Admin permissions revoked - AdminId: {AdminId}", adminUserId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Admin permissions revoked successfully",
						Status = "successful"
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error revoking admin permissions - AdminId: {AdminId}", adminUserId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while revoking permissions",
						Status = "failed"
					};
				}
			}
		}


		public async Task<BaseResponse> GetAdminPermissionsById(Guid adminUserId, AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			{
				try
				{
					if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format",
							Status = "failed"
						};

					// Fetch admin user
					var admin = await _queryrepositoryUser.Get(adminUserId);
					if (admin == null)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Admin user not found",
							Status = "failed"
						};

					if (admin.SchoolId != schoolId)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Admin belongs to a different school",
							Status = "failed"
						};

					var existing = await GetExistingPermissions(adminUserId, schoolId);
					if (existing == null)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = $"{admin.FirstName} {admin.LastName} has no permissions assigned",
							Status = "failed"
						};

					var permissionEnum = (AdminPermission)existing.Permissions;
					var permissionNames = Enum.GetValues<AdminPermission>()
						.Where(p => p != AdminPermission.None
								 && p != AdminPermission.BasicAdmin
								 && p != AdminPermission.FullAdmin
								 && permissionEnum.HasFlag(p))
						.Select(p => new
						{
							Value = (int)p,
							Name = p.ToString()
						})
						.ToList();

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Permissions retrieved successfully",
						Status = "successful",
						Data = new
						{
							AdminId = adminUserId,
							AdminName = $"{admin.FirstName} {admin.LastName}",
							PermissionsValue = existing.Permissions,
							Permissions = permissionNames,
							AssignedDate = existing.CreationDate,
							ModifiedDate = existing.ModifiedDate
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Error fetching admin permissions - AdminId: {AdminId}",
						adminUserId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching permissions",
						Status = "failed"
					};
				}
			}
		}



		public async Task<BaseResponse> GetAllStudentSubjects(Guid studentId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims.UserId))
			{
				try
				{
					if (!Guid.TryParse(claims.SchoolId, out var schoolId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "Invalid school identification",
							Status = "failed"
						};

						var sql = $@"
							SELECT
								s.Id       AS SubjectId,
								s.Subject  AS SubjectName,
								s.Category,
								'Major'    AS SubjectType
							FROM   StudentClassroom sc
							JOIN   ClassroomSubject cs ON cs.ClassroomId = sc.ClassroomId
							JOIN   Subjects         s  ON s.Id = cs.SubjectId
							WHERE  sc.StudentId = '{studentId}'
							AND    sc.SchoolId  = '{schoolId}'
							AND    sc.IsActive  = 1
							AND    cs.IsActive  = 1

							UNION ALL

							SELECT
								s.Id       AS SubjectId,
								s.Subject  AS SubjectName,
								s.Category,
								'Minor'    AS SubjectType
							FROM   StudentMinorSubject sms
							JOIN   Subjects            s  ON s.Id = sms.SubjectId
							WHERE  sms.StudentId = '{studentId}'
							AND    sms.SchoolId  = '{schoolId}'

							ORDER  BY SubjectType, SubjectName";

					var rows = await _queryrepositoryUser.QueryAsync<StudentSubjectDto>(sql, new Dictionary<string, object>());

					var subjects = rows.ToList();

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = subjects.Any()
							? $"{subjects.Count} subject(s) found"
							: "No subjects found for this student",
						Status = "success",
						Data = new
						{
							StudentId = studentId,
							TotalSubjects = subjects.Count,
							Major = subjects.Where(s => s.SubjectType == "Major").ToList(),
							Minor = subjects.Where(s => s.SubjectType == "Minor").ToList()
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Error fetching student subjects - StudentId: {StudentId}",
						studentId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching subjects",
						Status = "failed"
					};
				}
			}
		}


		public async Task<BaseResponse> GetStudentMinorSubjects(Guid studentId, Guid? classroomId, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims.UserId))
			{
				try
				{
					if (!Guid.TryParse(claims.SchoolId, out var schoolId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "Invalid school identification",
							Status = "failed"
						};

					// classroomId accepted but not used yet — reserved for future filtering
					var sql = $@"
						SELECT
							s.Id       AS SubjectId,
							s.Subject  AS SubjectName,
							s.Category AS Category
						FROM   StudentMinorSubject sms
						JOIN   Subjects            s ON s.Id = sms.SubjectId
						WHERE  sms.StudentId = '{studentId}'
						AND    sms.SchoolId  = '{schoolId}'
						AND    sms.IsActive  = 1
						ORDER  BY s.Subject";

					var rows = await _queryrepositoryUser.QueryAsync<StudentSubjectDto>(sql, new Dictionary<string, object>());

					var subjects = rows.ToList();

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = subjects.Any()
							? $"{subjects.Count} minor subject(s) found"
							: "No minor subjects found for this student",
						Status = "success",
						Data = new
						{
							StudentId = studentId,
							ClassroomId = classroomId,  // echoed back for future use
							TotalSubjects = subjects.Count,
							Subjects = subjects
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Error fetching student minor subjects - StudentId: {StudentId}",
						studentId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching minor subjects",
						Status = "failed"
					};
				}
			}
		}

		#endregion

		public async Task<BaseResponse> UpdateStudentMinorSubjects(Guid studentId,UpdateStudentMinorSubjectViewModel model,AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims.UserId))
			{
				try
				{
					if (!Guid.TryParse(claims.UserId, out var requesterId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "Unauthorised access",
							Status = "failed"
						};

					if (!Guid.TryParse(claims.SchoolId, out var schoolId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "Unauthorised school access",
							Status = "failed"
						};

					// ── Validate at least one operation ───────────────────────
					if (!model.AddSubjects.Any() && !model.RemoveSubjects.Any())
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "At least one subject to add or remove is required",
							Status = "failed"
						};

					// ── Check for overlap between add and remove ───────────────
					var overlap = model.AddSubjects.Intersect(model.RemoveSubjects).ToList();
					if (overlap.Any())
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "The same subject cannot be in both add and remove lists",
							Status = "failed"
						};

					// ── Verify student exists and belongs to school ───────────
					var student = await _queryrepositoryUser.Get(studentId);
					if (student == null || student.SchoolId != schoolId)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Student not found",
							Status = "failed"
						};

					if (student.RoleId != (int)UserRole.Student)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "User is not a student",
							Status = "failed"
						};

					// ── Verify all subjects to add exist and belong to school ─
					foreach (var subjectId in model.AddSubjects)
					{
						var subject = await _subjectQueryRespository.Get(subjectId);
						if (subject == null || subject.SchoolId != schoolId)
							return new BaseResponse
							{
								ResponseCode = ResponseCode.NotFound,
								ResponseMessage = $"Subject {subjectId} not found",
								Status = "failed"
							};
					}

					var nowStr = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
					var added = 0;
					var removed = 0;

					using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
					try
					{
						// ── Add subjects ──────────────────────────────────────
						foreach (var subjectId in model.AddSubjects)
						{
							// Check not already active
							var existing = await scope.Connection.QueryFirstOrDefaultAsync<StudentMinorSubject>(
										$@"SELECT TOP 1 * FROM StudentMinorSubject
								   WHERE  StudentId = '{studentId}'
								   AND    SubjectId = '{subjectId}'
								   AND    SchoolId  = '{schoolId}'
								   AND    IsActive  = 1",
								transaction: scope.Transaction);

							if (existing != null) continue; // already assigned, skip

							// Check if soft-deleted record exists — reactivate instead of insert
							var softDeleted = await scope.Connection.QueryFirstOrDefaultAsync<StudentMinorSubject>(
									$@"SELECT TOP 1 * FROM StudentMinorSubject
							   WHERE  StudentId = '{studentId}'
							   AND    SubjectId = '{subjectId}'
							   AND    SchoolId  = '{schoolId}'
							   AND    IsActive  = 0",
								transaction: scope.Transaction);

							if (softDeleted != null)
							{
								await scope.Connection.ExecuteAsync(
										$@"UPDATE StudentMinorSubject
								   SET    IsActive     = 1,
										  ModifiedDate = '{nowStr}'
								   WHERE  Id = '{softDeleted.Id}'",
									transaction: scope.Transaction);
							}
							else
							{
								var insertDict = new Dictionary<string, object>
								{
									{ "Id",           Guid.NewGuid() },
									{ "StudentId",    studentId      },
									{ "SubjectId",    subjectId      },
									{ "SchoolId",     schoolId       },
									{ "CreatedBy",    requesterId    },
									{ "CreationDate", nowStr         },
									{ "ModifiedDate", nowStr         },
									{ "IsActive",     true           }
								};

								await _commandRepositoryMinorSubject.Create(scope.Transaction, scope.Connection, insertDict);
							}

							added++;
						}

						// ── Remove subjects ───────────────────────────────────
						foreach (var subjectId in model.RemoveSubjects)
						{
							var rows = await scope.Connection.ExecuteAsync(
									$@"UPDATE StudentMinorSubject
							   SET    IsActive     = 0,
									  ModifiedDate = '{nowStr}'
							   WHERE  StudentId = '{studentId}'
							   AND    SubjectId = '{subjectId}'
							   AND    SchoolId  = '{schoolId}'
							   AND    IsActive  = 1",
								transaction: scope.Transaction);

							if (rows > 0) removed++;
						}

						await scope.CommitAsync();
					}
					catch (Exception ex)
					{
						_logger.Error(ex,
							"Rolling back student minor subject update - StudentId: {StudentId}",
							studentId);
						try { await scope.RollbackAsync(); } catch { }
						throw;
					}

					_logger.Information(
						"Student minor subjects updated - StudentId: {StudentId}, " +
						"Added: {Added}, Removed: {Removed}, UpdatedBy: {RequesterId}",
						studentId, added, removed, requesterId);

					// ── Fetch updated minor subjects ───────────────────────────
					var updatedQuery = $@"
						SELECT
							s.Id       AS SubjectId,
							s.Subject  AS SubjectName,
							s.Category AS Category
						FROM   StudentMinorSubject sms
						JOIN   Subjects            s ON s.Id = sms.SubjectId
						WHERE  sms.StudentId = '{studentId}'
						AND    sms.SchoolId  = '{schoolId}'
						AND    sms.IsActive  = 1
						ORDER  BY s.Subject";

					var updated = await _queryrepositoryUser.QueryAsync<StudentSubjectDto>(updatedQuery, new Dictionary<string, object>());

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Student minor subjects updated successfully",
						Status = "successful",
						Data = new
						{
							StudentId = studentId,
							StudentName = $"{student.FirstName} {student.LastName}",
							Added = added,
							Removed = removed,
							TotalSubjects = updated.Count(),
							Subjects = updated.ToList()
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Error updating student minor subjects - StudentId: {StudentId}",
						studentId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while updating minor subjects",
						Status = "failed"
					};
				}
			}
		}


		public async Task<BaseResponse> UpdateStudentAssignment(Guid studentId,UpdateStudentAssignmentViewModel model,AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims.UserId))
			{
				try
				{
					if (!Guid.TryParse(claims.UserId, out var requesterId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "Unauthorised access",
							Status = "failed"
						};

					if (!Guid.TryParse(claims.SchoolId, out var schoolId))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "Unauthorised school access",
							Status = "failed"
						};

					if (!Enum.TryParse<UserRole>(claims.Role, ignoreCase: true, out var requesterRole))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid role in token",
							Status = "failed"
						};

					// ── Authorization ─────────────────────────────────────────
					if (requesterRole == UserRole.Administrator)
					{
						var hasPermission = await this.HasPermission(
							requesterId, schoolId, AdminPermission.ManageStudents);

						if (!hasPermission)
							return new BaseResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = "You do not have permission to update student assignments",
								Status = "failed"
							};
					}
					else if (requesterRole != UserRole.SuperAdministrator && requesterRole != UserRole.HeadTeacher)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You are not authorized to update student assignments",
							Status = "failed"
						};
					}

					// ── Validate at least one change ──────────────────────────
					if (!model.ClassroomId.HasValue
					 && !model.AddSubjects.Any()
					 && !model.RemoveSubjects.Any())
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "No changes provided",
							Status = "failed"
						};

					// ── Check subject overlap ─────────────────────────────────
					var overlap = model.AddSubjects.Intersect(model.RemoveSubjects).ToList();
					if (overlap.Any())
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "The same subject cannot be in both add and remove lists",
							Status = "failed"
						};

					// ── Verify student exists and belongs to school ───────────
					var student = await _queryrepositoryUser.Get(studentId);
					if (student == null || student.SchoolId != schoolId)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Student not found",
							Status = "failed"
						};

					if (student.RoleId != (int)UserRole.Student)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "User is not a student",
							Status = "failed"
						};

					// ── Verify classroom exists and belongs to school ─────────
					if (model.ClassroomId.HasValue)
					{
						var classroom = await _classroomQueryRespository.Get(model.ClassroomId.Value);
						if (classroom == null || classroom.SchoolId != schoolId)
							return new BaseResponse
							{
								ResponseCode = ResponseCode.NotFound,
								ResponseMessage = "Classroom not found",
								Status = "failed"
							};
					}

					// ── Verify subjects to add exist and belong to school ─────
					foreach (var subjectId in model.AddSubjects)
					{
						var subject = await _subjectQueryRespository.Get(subjectId);
						if (subject == null || subject.SchoolId != schoolId)
							return new BaseResponse
							{
								ResponseCode = ResponseCode.NotFound,
								ResponseMessage = $"Subject {subjectId} not found",
								Status = "failed"
							};
					}

					var nowStr = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
					var classroomChanged = false;
					var subjectsAdded = 0;
					var subjectsRemoved = 0;

					using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
					try
					{
						// ── Update classroom ──────────────────────────────────
						if (model.ClassroomId.HasValue)
						{
							// Soft delete current classroom assignment
							await scope.Connection.ExecuteAsync(
									$@"UPDATE StudentClassroom
							   SET    IsActive     = 0,
									  ModifiedDate = '{nowStr}'
							   WHERE  StudentId = '{studentId}'
							   AND    SchoolId  = '{schoolId}'
							   AND    IsActive  = 1",
								transaction: scope.Transaction);

							// Check if already assigned to new classroom (soft deleted)
							var existing = await scope.Connection.QueryFirstOrDefaultAsync<dynamic>(
									$@"SELECT TOP 1 Id FROM StudentClassroom
							   WHERE  StudentId   = '{studentId}'
							   AND    ClassroomId = '{model.ClassroomId.Value}'
							   AND    SchoolId    = '{schoolId}'",
								transaction: scope.Transaction);

							if (existing != null)
							{
								// Reactivate
								await scope.Connection.ExecuteAsync(
										$@"UPDATE StudentClassroom
								   SET    IsActive     = 1,
										  ModifiedDate = '{nowStr}'
								   WHERE  StudentId   = '{studentId}'
								   AND    ClassroomId = '{model.ClassroomId.Value}'
								   AND    SchoolId    = '{schoolId}'",
									transaction: scope.Transaction);
							}
							else
							{
								// Insert new
								await _commandRepositoryStudentClassroom.Create(
									scope.Transaction, scope.Connection,
									new Dictionary<string, object>
									{
										{ "Id",           Guid.NewGuid()          },
										{ "StudentId",    studentId               },
										{ "ClassroomId",  model.ClassroomId.Value },
										{ "SchoolId",     schoolId                },
										{ "CreatedBy",    requesterId             },
										{ "CreationDate", nowStr                  },
										{ "ModifiedDate", nowStr                  },
										{ "IsActive",     true                    }
									});
							}

							classroomChanged = true;
						}

						// ── Add minor subjects ────────────────────────────────
						foreach (var subjectId in model.AddSubjects)
						{
							var existing = await scope.Connection.QueryFirstOrDefaultAsync<dynamic>(
									$@"SELECT TOP 1 Id, IsActive FROM StudentMinorSubject
							   WHERE  StudentId = '{studentId}'
							   AND    SubjectId = '{subjectId}'
							   AND    SchoolId  = '{schoolId}'",
									transaction: scope.Transaction);

							if (existing != null && existing.IsActive == true)
								continue; // already active, skip

							if (existing != null)
							{
								// Reactivate soft-deleted record
									await scope.Connection.ExecuteAsync(
										$@"UPDATE StudentMinorSubject
								   SET    IsActive     = 1,
										  ModifiedDate = '{nowStr}'
								   WHERE  StudentId = '{studentId}'
								   AND    SubjectId = '{subjectId}'
								   AND    SchoolId  = '{schoolId}'",
										transaction: scope.Transaction);
							}
							else
							{
								await _commandRepositoryMinorSubject.Create(
									scope.Transaction, scope.Connection,
									new Dictionary<string, object>
									{
										{ "Id",           Guid.NewGuid() },
										{ "StudentId",    studentId      },
										{ "SubjectId",    subjectId      },
										{ "SchoolId",     schoolId       },
										{ "CreatedBy",    requesterId    },
										{ "CreationDate", nowStr         },
										{ "ModifiedDate", nowStr         },
										{ "IsActive",     true           }
									});
							}

							subjectsAdded++;
						}

						// ── Remove minor subjects ─────────────────────────────
						foreach (var subjectId in model.RemoveSubjects)
						{
							var rows = await scope.Connection.ExecuteAsync(
									$@"UPDATE StudentMinorSubject
							   SET    IsActive     = 0,
									  ModifiedDate = '{nowStr}'
							   WHERE  StudentId = '{studentId}'
							   AND    SubjectId = '{subjectId}'
							   AND    SchoolId  = '{schoolId}'
							   AND    IsActive  = 1",
								transaction: scope.Transaction);

							if (rows > 0) subjectsRemoved++;
						}

						await scope.CommitAsync();
					}
					catch (Exception ex)
					{
						_logger.Error(ex,
							"Rolling back student assignment update - StudentId: {StudentId}",
							studentId);
						try { await scope.RollbackAsync(); } catch { }
						throw;
					}

					_logger.Information(
							"Student assignment updated - StudentId: {StudentId}, " +
							"ClassroomChanged: {ClassroomChanged}, " +
							"SubjectsAdded: {Added}, SubjectsRemoved: {Removed}, " +
							"UpdatedBy: {RequesterId}",
						studentId, classroomChanged,
						subjectsAdded, subjectsRemoved, requesterId);

					// ── Fetch updated state ───────────────────────────────────
					var classroomQuery = $@"
						SELECT
							c.Id       AS ClassroomId,
							c.Name     AS ClassName,
							c.IsActive AS ClassroomIsActive
						FROM   StudentClassroom sc
						JOIN   Classroom        c ON c.Id = sc.ClassroomId
						WHERE  sc.StudentId = '{studentId}'
						AND    sc.SchoolId  = '{schoolId}'
						AND    sc.IsActive  = 1";

					var minorSubjectsQuery = $@"
						SELECT
							s.Id       AS SubjectId,
							s.Subject  AS SubjectName,
							s.Category AS Category
						FROM   StudentMinorSubject sms
						JOIN   Subjects            s ON s.Id = sms.SubjectId
						WHERE  sms.StudentId = '{studentId}'
						AND    sms.SchoolId  = '{schoolId}'
						AND    sms.IsActive  = 1
						ORDER  BY s.Subject";

					var classroom2 = await _queryrepositoryUser.QueryAsync<ClassroomRow>(classroomQuery, new Dictionary<string, object>());

					var minorSubjects = await _queryrepositoryUser.QueryAsync<StudentSubjectDto>(minorSubjectsQuery, new Dictionary<string, object>());

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Student assignment updated successfully",
						Status = "successful",
						Data = new
						{
							StudentId = studentId,
							StudentName = $"{student.FirstName} {student.LastName}",
							ClassroomChanged = classroomChanged,
							SubjectsAdded = subjectsAdded,
							SubjectsRemoved = subjectsRemoved,
							Classroom = classroom2.FirstOrDefault(),
							MinorSubjects = minorSubjects.ToList()
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Error updating student assignment - StudentId: {StudentId}",
						studentId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while updating student assignment",
						Status = "failed"
					};
				}
			}
		}

		#region HasPermission

		/// <summary>
		/// Check if admin has a specific permission
		/// Used by other services for authorization checks
		/// </summary>
		/// <example>
		/// var canApprove = await HasPermission(adminId, schoolId, AdminPermission.ApproveClasses);
		/// </example>
		public async Task<bool> HasPermission(Guid adminUserId,Guid schoolId,AdminPermission permission)
		{
			try
			{
				var permissions = await GetExistingPermissions(adminUserId, schoolId);
				if (permissions == null) return false;

				// Convert database int to enum and check using bitwise AND
				var adminPermissions = (AdminPermission)permissions.Permissions;
				return adminPermissions.HasPermission(permission);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error checking permission - AdminId: {AdminId}", adminUserId);
				return false;
			}
		}

		#endregion

		#region Helper Methods

		/// <summary>
		/// Get existing permissions record from database
		/// </summary>
		private async Task<AdminPermissions?> GetExistingPermissions(Guid adminUserId, Guid schoolId)
		{
			try
			{
				var query = $@"
                    SELECT * FROM AdminPermissions 
                    WHERE UserId = '{adminUserId}' 
                    AND SchoolId = '{schoolId}' 
                    AND IsActive = 1";

				var permissions = await _adminPermissionsQueryRespository.Get(query);
				return permissions;
			}
			catch (Exception ex)
			{
				_logger.Error(
					ex,
					"Error fetching existing permissions - AdminId: {AdminId}",
					adminUserId);
				return null;
			}
		}

		#endregion


		private string GenerateTempPassword()
		{
			
			const string uppercase = "ABCDEFGHJKMNPQRSTUVWXYZ";
			const string lowercase = "abcdefghjkmnpqrstuvwxyz";
			const string numbers = "23456789";
			const string special = "!@#$%&*";

			var random = new Random();
			var password = new List<char>();

			
			password.Add(uppercase[random.Next(uppercase.Length)]);
			password.Add(uppercase[random.Next(uppercase.Length)]);
			password.Add(lowercase[random.Next(lowercase.Length)]);
			password.Add(lowercase[random.Next(lowercase.Length)]);
			password.Add(numbers[random.Next(numbers.Length)]);
			password.Add(numbers[random.Next(numbers.Length)]);
			password.Add(special[random.Next(special.Length)]);
			password.Add(special[random.Next(special.Length)]);

			// ✅ Shuffle so it is not predictable
			// pattern like AA aa 22 !!
			var shuffled = password
				.OrderBy(_ => random.Next())
				.ToArray();

			return new string(shuffled);
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

		public async Task<BaseResponse> CreateSchoolAdmin(CreateSchoolAdminViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims.UserId))
			{
				try
				{
					if (model is null)
						return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "Object is empty", Status = "failed" };

					if (string.IsNullOrWhiteSpace(model.SchoolCode))
						return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "School code is required", Status = "failed" };

					if (!Guid.TryParse(claims.UserId, out var platformUserId))
						return new BaseResponse { ResponseCode = ResponseCode.Unauthorized, ResponseMessage = "Invalid authentication", Status = "failed" };

					using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");

					try
					{
						// Resolve school by school code
						var schoolCode = await scope.Connection.QueryFirstOrDefaultAsync<SchoolCode>(
							"SELECT * FROM SchoolCode WHERE Code = @Code",
							new { Code = model.SchoolCode }, scope.Transaction);

						if (schoolCode is null)
						{
							await scope.RollbackAsync();
							return new BaseResponse { ResponseCode = ResponseCode.NotFound, ResponseMessage = "School not found with the provided code", Status = "failed" };
						}

						var schoolId = schoolCode.SchoolId;

						// Check username uniqueness within the school
						var existingUser = await scope.Connection.QueryFirstOrDefaultAsync<Guid?>(
							"SELECT TOP 1 Id FROM Users WHERE UserName = @Username AND SchoolId = @SchoolId",
							new { Username = model.Username, SchoolId = schoolId }, scope.Transaction);

						if (existingUser is not null)
						{
							await scope.RollbackAsync();
							return new BaseResponse { ResponseCode = ResponseCode.Conflict, ResponseMessage = "Username already exists in this school", Status = "failed" };
						}

						// Check email uniqueness within the school
						var existingEmail = await scope.Connection.QueryFirstOrDefaultAsync<Guid?>(
							"SELECT TOP 1 Id FROM Users WHERE EmailAddress = @Email AND SchoolId = @SchoolId",
							new { Email = model.Email, SchoolId = schoolId }, scope.Transaction);

						if (existingEmail is not null)
						{
							await scope.RollbackAsync();
							return new BaseResponse { ResponseCode = ResponseCode.Conflict, ResponseMessage = "Email already exists in this school", Status = "failed" };
						}

						var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
						var adminUserId = Guid.NewGuid();
						var passwordHash = HashPassword(model.Password);

						// Insert SuperAdministrator user (RoleId = 3)
						await scope.Connection.ExecuteAsync(@"
							INSERT INTO Users (Id, CreationDate, ModifiedDate, FirstName, MiddleName, LastName, EmailAddress, HashPassword,
								IsActive, HasAccess, UserName, SchoolId, RoleId, CreatedBy)
							VALUES (@Id, @Now, @Now, @FirstName, @MiddleName, @LastName, @Email, @PasswordHash,
								1, 1, @Username, @SchoolId, 3, @CreatedBy)",
							new
							{
								Id = adminUserId,
								Now = now,
								FirstName = model.FirstName,
								MiddleName = (object?)model.MiddleName ?? DBNull.Value,
								LastName = model.LastName,
								Email = model.Email,
								PasswordHash = passwordHash,
								Username = model.Username,
								SchoolId = schoolId,
								CreatedBy = platformUserId
							},
							scope.Transaction);

						// Insert AdminPermissions (FullAdmin = 127)
						await scope.Connection.ExecuteAsync(@"
							INSERT INTO AdminPermissions (Id, UserId, SchoolId, Permissions, CreationDate, ModifiedDate, CreatedBy, IsActive)
							VALUES (@Id, @UserId, @SchoolId, 127, @Now, @Now, @CreatedBy, 1)",
							new
							{
								Id = Guid.NewGuid(),
								UserId = adminUserId,
								SchoolId = schoolId,
								Now = now,
								CreatedBy = platformUserId
							},
							scope.Transaction);

						await scope.CommitAsync();

						_logger.Information("School admin created - UserId: {UserId}, SchoolId: {SchoolId}, Username: {Username}",
							adminUserId, schoolId, model.Username);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.successful,
							ResponseMessage = "School super admin created successfully",
							Status = "successful",
							Data = new
							{
								UserId = adminUserId,
								SchoolId = schoolId,
								Username = model.Username,
								Email = model.Email,
								Role = "SuperAdministrator"
							}
						};
					}
					catch
					{
						await scope.RollbackAsync();
						throw;
					}
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error creating school admin");
					return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = "An error occurred while creating school admin", Status = "failed" };
				}
			}
		}
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
		public object? RoleData { get; set; }
	}
}
