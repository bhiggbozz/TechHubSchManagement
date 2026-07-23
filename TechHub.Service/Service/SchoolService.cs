using AutoMapper;
using Azure;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TechHub.Core;
using TechHub.Core.Constant;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Helper;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.classroom;
using TechHub.Core.ViewModel.Platform;
using TechHub.Core.ViewModel.school;
using TechHub.QuestionBank.Core.DTO;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;
using TechhubMS.util;
using static System.Formats.Asn1.AsnWriter;
using SubjectDto = TechHub.Core.ResponseModel.SubjectDto;


namespace TechHub.Service.Service
{
	public class SchoolService : ISchoolService
	{
		private readonly ICommandRespository<School> _schCommandRespository;
		private readonly ICommandRespository<SchoolCode> _schCodeCommandRespository;
		private readonly ICommandRespository<Classroom> _studentClassCommandRespository;
		private readonly ICommandRespository<Subjects> _subjectCommandRespository;
		private readonly ICommandRespository<ClassroomSubject> _classroomSubjectCommandRespository;
		private readonly ICommandRespository<TeacherClassroom> _classroomTeacherCommandRepository;
		private readonly ICommandRespository<Topic> _topicCommandRepository;
		private readonly ICommandRespository<SubTopic> _subTopicCommandRepository;
		private readonly ICommandRespository<Users> _userCommandRepository;
		private readonly ICommandRespository<ApprovalRequests> _approvalRequestCommandRepository;
		private readonly ICommandRespository<SchoolRegistrationRequest> _registrationRequestCommandRepository;
		private readonly IQueryRepository<SchoolRegistrationRequest> _registrationRequestQueryRepository;


		private readonly IQueryRepository<State> _queryrepositoryState;
		private readonly IQueryRepository<School> _schQueryRepository;
		private readonly IQueryRepository<Subjects> _queryrepositorySubject;
		private readonly IQueryRepository<Users> _queryrepositoryUser;
		private readonly IQueryRepository<Classroom> _studentClassQueryRespository;
		private readonly IQueryRepository<ClassroomSubject> _classroomSubjectQueryRespository;
		private readonly IQueryRepository<TeacherClassroom> _classroomTeacherQueryRespository;
		private readonly IQueryRepository<AdminPermissions> _adminPermissionsQueryRespository;


		private readonly IQueryRepository<Topic> _topicQueryRepository;
		private readonly IQueryRepository<SubTopic> _subTopicQueryRepository;




		private readonly IConfiguration _configuration;
		private readonly ILogger _logger;
		private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;
		private readonly ICloudinaryService _cloudinaryService;
		private readonly IEmailService _emailService;
		private readonly string? _connString;

		private readonly IMapper _mapper;
		public SchoolService(ICommandRespository<School> schCommandRespository, IQueryRepository<State> queryRepositoryState , 
			IMapper mapper, ICommandRespository<SchoolCode> schCodeCommandRespository, ICommandRespository<Classroom> studentClassCommandRespository,
			ICommandRespository<ClassroomSubject> classroomSubjectCommandRespository, IQueryRepository<ClassroomSubject> classroomSubjectQueryRespository,
			IDbTransactionScopeFactory dbTransactionScopeFactory, IQueryRepository<Users> queryrepositoryUser, IQueryRepository<Classroom> studentClassQueryRespository,
		    ICommandRespository<Subjects> subjectCommandRespository, IQueryRepository<TeacherClassroom> classroomTeacherQueryRespository, 
		    ICommandRespository<TeacherClassroom> classroomTeacherCommandRepository,
			IQueryRepository<School> schQueryRepository, ICloudinaryService cloudinaryService, IQueryRepository<AdminPermissions> adminPermissionsQueryRespository,
			IQueryRepository<Subjects> queryrepositorySubject, ICommandRespository<Topic> topicCommandRepository, IQueryRepository<Topic> topicQueryRepository, ICommandRespository<SubTopic> subTopicCommandRepository,
			IQueryRepository<SubTopic> subTopicQueryRepository, ICommandRespository<Users> userCommandRepository, ICommandRespository<ApprovalRequests> approvalRequestCommandRepository,
			ICommandRespository<SchoolRegistrationRequest> registrationRequestCommandRepository, IQueryRepository<SchoolRegistrationRequest> registrationRequestQueryRepository,
			IConfiguration configuration, ILogger logger, IEmailService emailService)
		{
			_emailService = emailService;
			_schCommandRespository = schCommandRespository;
			_queryrepositoryState = queryRepositoryState;
			_schCodeCommandRespository= schCodeCommandRespository;
			_studentClassCommandRespository = studentClassCommandRespository;
			_subjectCommandRespository = subjectCommandRespository;
			_topicCommandRepository = topicCommandRepository;
			_queryrepositorySubject = queryrepositorySubject;
			_studentClassQueryRespository = studentClassQueryRespository;
			_subTopicCommandRepository = subTopicCommandRepository;
			_userCommandRepository = userCommandRepository;
			_approvalRequestCommandRepository = approvalRequestCommandRepository;
			_registrationRequestCommandRepository = registrationRequestCommandRepository;
			_registrationRequestQueryRepository = registrationRequestQueryRepository;

			_classroomSubjectCommandRespository = classroomSubjectCommandRespository;
			_classroomSubjectQueryRespository = classroomSubjectQueryRespository;
			_classroomTeacherCommandRepository = classroomTeacherCommandRepository;
			_classroomTeacherQueryRespository = classroomTeacherQueryRespository;
			_schQueryRepository = schQueryRepository;
			_topicQueryRepository = topicQueryRepository;
			_subTopicQueryRepository = subTopicQueryRepository;
			_adminPermissionsQueryRespository = adminPermissionsQueryRespository;


			_configuration = configuration;
			_dbTransactionScopeFactory = dbTransactionScopeFactory;
			_queryrepositoryUser = queryrepositoryUser;
			_cloudinaryService = cloudinaryService;
			_mapper = mapper;
			_logger = logger;
			_connString = _configuration.GetConnectionString("DbConnectionString") ?? null;
		}

		public async Task<BaseResponse> CreateSchool(SchoolViewModel schoolViewModel)
		{
			try
			{
				if (schoolViewModel is null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "object is empty", Status = "failed" };
				}
				var school = _mapper.Map<School>(schoolViewModel);
				var getNullProperties = HelperUtil.GetNullPorpertiesName(school);
				if(getNullProperties != string.Empty)
				{
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = getNullProperties + " cannot be null", Status = "failed" };
				}
				var insertDict = new Dictionary<string, object> { { "CreationDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")}, { "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")}, { "Id", school.Id },
					{ "SchoolName", school.SchoolName}, { "Location", school.Location}, {"CountryId", school.CountryId }, {"StateId", school.StateId },
					{ "State", (object?)school.State ?? DBNull.Value }, {"Address", school.Address }, { "HasBranch", school.HasBranch}, { "IsActive", school.ISActive} };
				if(_connString == null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = "Connection string not set", Status = "failed" };
				}
				var schoolCodeInsertDict = new Dictionary<string, object> { { "SchoolId", school.Id }, { "Code", school.Id } };

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				//await _schCommandRespository.Create(school);
				await _schCommandRespository.Create(scope.Transaction, scope.Connection, insertDict);
				await _schCodeCommandRespository.Create(scope.Transaction, scope.Connection, schoolCodeInsertDict);

				//if (returnedId != null)
				//{
				//	var schoolCodeInsertDict = new Dictionary<string, object> { { "SchoolId", returnedId }, { "Code", returnedId } };


				//	await _schCodeCommandRespository.Create(new SchoolCode { SchoolId = returnedId, Code = returnedId.ToString() });
				//}
				await scope.CommitAsync();
				//{

				//	await
				//}


				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "object created successfully", Status = "successful" };
			}
			catch (SqlException ex)
			{
				if (ex.Message.ToLower().Contains("duplicate"))
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "School information exists", Status = "failed" };
				}
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
				

			}
			catch (Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
			}

		}
		
		public async Task<BaseResponse> GetAllStates(int countryId)
		{
			try
			{
				string query = $"select * from State where CountryId = {countryId}";
				var countryStates = await _queryrepositoryState.GetByQuery(query);
				if (!countryStates.Any())
				{
					return new StatesResponseModel {  ResponseCode = ResponseCode.successful, ResponseMessage = "No State for this country", Status = "failed" };
				}
				var mappedCountryStates = _mapper.Map<List<StateResponse>>(countryStates.ToList());
				return new StatesResponseModel { states = mappedCountryStates, ResponseCode = ResponseCode.successful, ResponseMessage = "successful", Status = "successful" };
			}
			catch(Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage =  $"{ex.Message}", Status = "failed" };
			}
		}
		//public string GenerateSchoolCode(string schoolCode, string schoolName)
		//{

		//}

		public async Task<BaseResponse> UpdateSchoolCode(SchoolCodeViewModel schoolCode)
		{
			if(schoolCode == null)
			{
				return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "object is empty", Status = "failed" };
			}
			var getNullProperties = HelperUtil.GetNullPorpertiesName(schoolCode);
			if (getNullProperties != string.Empty)
			{
				return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = getNullProperties + " cannot be null", Status = "failed" };
			}

			var updateDict = new Dictionary<string, object> { { "SchoolId", schoolCode.SchoolId }, { "Code", schoolCode.Code } };
			var KeyPair = new KeyValuePair<string, object>("SchoolId", schoolCode.SchoolId);
			await _schCodeCommandRespository.UpdateTableColumnById(updateDict, KeyPair);
			return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "object updated successfully", Status = "successful" };

		}
		public async Task<BaseResponse> CreateStudentClassV2(CreateStudentClassViewModel createStudentClassViewModel, AuthenticatedUserClaims userInfo)
		{
			using (LogContext.PushProperty("RequestedBy", userInfo.UserId))
			{
				try
				{
					// Validation 1: Check if model is null
					if (createStudentClassViewModel is null)
					{
						_logger.Warning("Create classroom request with null data");
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Classroom data cannot be empty",
							Status = "failed"
						};
					}

					// Validation 2: Check if classrooms list is empty
					if (!createStudentClassViewModel.classrooms.Any())
					{
						_logger.Warning("Create classroom request with empty classrooms list");
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "At least one classroom is required",
							Status = "failed"
						};
					}

					// Validation 3: Check SchoolId
					if (string.IsNullOrEmpty(userInfo.SchoolId))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "SchoolId not found in authentication token",
							Status = "failed"
						};
					}

					// Validation 4: Check UserId
					if (string.IsNullOrEmpty(userInfo.UserId))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Unauthorized,
							ResponseMessage = "UserId not found in authentication token",
							Status = "failed"
						};
					}

					// Validation 5: Parse SchoolId
					if (!Guid.TryParse(userInfo.SchoolId, out var schoolId))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format in token",
							Status = "failed"
						};
					}

					// Validation 6: Parse UserId
					if (!Guid.TryParse(userInfo.UserId, out var createdBy))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId format in token",
							Status = "failed"
						};
					}

					_logger.Information(
						"Creating {ClassroomCount} classroom(s) - SchoolId: {SchoolId}",
						createStudentClassViewModel.classrooms.Count,
						schoolId);

					var classroomsToCreate = new List<Dictionary<string, object>>();
					var subjectsToCreate = new List<Dictionary<string, object>>();
					var classroomNames = new List<string>();
					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

					foreach (var classroomView in createStudentClassViewModel.classrooms)
					{
						// Validation 7: Check classroom name
						if (string.IsNullOrWhiteSpace(classroomView.Name))
						{
							_logger.Warning("Classroom name is empty");
							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = "Classroom name cannot be empty",
								Status = "failed"
							};
						}

						// Validation 8: Check for duplicate classroom name
						var duplicateCheck = await CheckDuplicateClassroom(classroomView.Name.Trim(), schoolId);
						if (duplicateCheck)
						{
							_logger.Warning(
								"Duplicate classroom name - Name: {ClassroomName}, SchoolId: {SchoolId}",
								classroomView.Name,
								schoolId);
							return new BaseResponse
							{
								ResponseCode = ResponseCode.Conflict,
								ResponseMessage = $"Classroom '{classroomView.Name}' already exists in your school",
								Status = "failed"
							};
						}

						var classroomId = Guid.NewGuid();

						var classroomDict = new Dictionary<string, object>
						{
							{ "Id",           classroomId },
							{ "Name",         classroomView.Name.Trim() },
							{ "NoOfStudents", classroomView.NoOfStudents },
							{ "CreationDate", now },
							{ "ModifiedDate", now },
							{ "CreatedBy",    createdBy },
							{ "SchoolId",     schoolId },
							{ "IsActive",     true }
						};

						classroomsToCreate.Add(classroomDict);
						classroomNames.Add(classroomView.Name.Trim());

						foreach (var subjectId in classroomView.SubjectIds)
						{
							subjectsToCreate.Add(new Dictionary<string, object>
							{
								{ "Id",           Guid.NewGuid() },
								{ "ClassroomId",  classroomId },
								{ "SubjectId",    subjectId },
								{ "SchoolId",     schoolId },
								{ "Createdby",    createdBy },
								{ "CreationDate", now },
								{ "ModifiedDate", now },
								{ "IsActive",     true }
							});
						}

						_logger.Debug(
							"Prepared classroom for creation - Id: {ClassroomId}, Name: {ClassroomName}, SubjectCount: {SubjectCount}",
							classroomId,
							classroomView.Name,
							classroomView.SubjectIds.Count);
					}

					using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");

					try
					{
						// Insert classrooms
						await _studentClassCommandRespository.CreateBatchAsync(
							scope.Transaction, scope.Connection, classroomsToCreate);

						// Insert classroom subjects — only if any were provided
						if (subjectsToCreate.Any())
						{
							await _classroomSubjectCommandRespository.CreateBatchAsync(
								scope.Transaction, scope.Connection, subjectsToCreate);
						}

						await scope.CommitAsync();
					}
					catch (Exception ex)
					{
						_logger.Error(
							ex,
							"Rolling back transaction during classroom creation - SchoolId: {SchoolId}",
							schoolId);

						try { await scope.RollbackAsync(); }
						catch (Exception rbEx)
						{
							_logger.Error(rbEx, "Rollback failed - SchoolId: {SchoolId}", schoolId);
						}

						throw;
					}

					_logger.Information(
						"Successfully created {ClassroomCount} classroom(s) - Names: {ClassroomNames}",
						classroomsToCreate.Count,
						string.Join(", ", classroomNames));

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = $"{classroomsToCreate.Count} classroom(s) created successfully",
						Status = "successful",
						Data = new
						{
							ClassroomsCreated = classroomsToCreate.Count,
							ClassroomIds = classroomsToCreate.Select(c => c["Id"]).ToList(),
							ClassroomNames = classroomNames,
							SubjectsAssigned = subjectsToCreate.Count
						}
					};
				}
				catch (SqlException ex)
				{
					_logger.Error(
						ex,
						"SQL error creating classrooms - SchoolId: {SchoolId}, ClassroomCount: {ClassroomCount}",
						userInfo.SchoolId,
						createStudentClassViewModel?.classrooms?.Count ?? 0);

					if (ex.Message.ToLower().Contains("duplicate"))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Conflict,
							ResponseMessage = "One or more classrooms already exist",
							Status = "failed"
						};
					}

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "Database error occurred while creating classrooms",
						Status = "failed"
					};
				}
				catch (Exception ex)
				{
					_logger.Error(
						ex,
						"Unexpected error creating classrooms - SchoolId: {SchoolId}",
						userInfo.SchoolId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An unexpected error occurred while creating classrooms",
						Status = "failed"
					};
				}
			}
		}

		/// <summary>
		/// this endpoint is to get endpoint to fetch all classrooms
		/// </summary>
		/// <param name="createSubjectModel"></param>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		#region
		public async Task<ClassroomDetails> GetAllClassrooms(AuthenticatedUserClaims userInfo,int pageNumber = 1,int pageSize = 50)
		{
			using (LogContext.PushProperty("RequestedBy", userInfo.UserId))
			//using (LogContext.PushProperty("TenantId", userInfo.TenantIdentifier))
			{
				try
				{
					// Validation: Parse SchoolId
					if (!Guid.TryParse(userInfo.SchoolId, out var schoolId))
					{
						_logger.Warning("Invalid SchoolId format in token - SchoolId: {SchoolId}", userInfo.SchoolId);

						return new ClassroomDetails
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format in token",
							Status = "failed"
						};
					}

					// Validate pagination
					if (pageNumber < 1) pageNumber = 1;
					if (pageSize < 1 || pageSize > 100) pageSize = 50;

					_logger.Information(
						"Fetching classrooms - SchoolId: {SchoolId}, Page: {PageNumber}/{PageSize}",
						schoolId,
						pageNumber,
						pageSize);

					// Get all classrooms for the school
					var query = $@"
						SELECT * FROM Classroom 
						WHERE SchoolId = '{schoolId}' 
						ORDER BY Name";

					var allClassrooms = await _studentClassQueryRespository.GetByQuery(query);
					var classroomsList = allClassrooms.Where(c => c != null).ToList();

					// Pagination
					var totalCount = classroomsList.Count;
					var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

					var paginatedClassrooms = classroomsList
						.Skip((pageNumber - 1) * pageSize)
						.Take(pageSize)
						.ToList();

					// Map to DTOs
					var classroomDtos = paginatedClassrooms.Select(c => new ClassroomDto
					{
						Id = c!.Id,
						Name = c.Name ?? string.Empty,
						NoOfStudents = c.NoOfStudents,
						IsActive = c.IsActive,
						CreationDate = c.CreationDate ?? string.Empty,
						ModifiedDate = c.ModifiedDate ?? string.Empty
					}).ToList();

					_logger.Information(
						"Successfully fetched {ClassroomCount} classroom(s) - Page: {PageNumber}/{TotalPages}",
						classroomDtos.Count,
						pageNumber,
						totalPages);

					return new ClassroomDetails
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Classrooms retrieved successfully",
						Status = "successful",
						Data = new ClassroomsListData
						{
							Classrooms = classroomDtos,
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
					_logger.Error(ex, "Error fetching classrooms - SchoolId: {SchoolId}", userInfo.SchoolId);

					return new ClassroomDetails
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching classrooms",
						Status = "failed"
					};
				}
			}
		}

		//public async Task<ClassroomDetailResponse> GetClassroomById(
		//	Guid classroomId,
		//	AuthenticatedUserClaims userInfo)
		//{
		//	using (LogContext.PushProperty("RequestedBy", userInfo.UserId))
		//	//using (LogContext.PushProperty("TenantId", userInfo.TenantIdentifier))
		//	{
		//		try
		//		{
		//			// Parse SchoolId
		//			if (!Guid.TryParse(userInfo.SchoolId, out var schoolId))
		//			{
		//				return new ClassroomDetailResponse
		//				{
		//					ResponseCode = ResponseCode.BadRequest,
		//					ResponseMessage = "Invalid SchoolId format in token",
		//					Status = "failed"
		//				};
		//			}

		//			_logger.Information("Fetching classroom by ID - ClassroomId: {ClassroomId}", classroomId);

		//			// Get classroom
		//			var classroom = await _studentClassQueryRespository.Get(classroomId);

		//			if (classroom == null)
		//			{
		//				_logger.Warning("Classroom not found - ClassroomId: {ClassroomId}", classroomId);

		//				return new ClassroomDetailResponse
		//				{
		//					ResponseCode = ResponseCode.NotFound,
		//					ResponseMessage = "Classroom not found",
		//					Status = "failed"
		//				};
		//			}

		//			// Multi-tenancy check
		//			if (classroom.SchoolId != schoolId)
		//			{
		//				_logger.Warning(
		//					"Unauthorized access attempt - User {UserId} tried to access classroom {ClassroomId} from different school",
		//					userInfo.UserId,
		//					classroomId);

		//				return new ClassroomDetailResponse
		//				{
		//					ResponseCode = ResponseCode.Forbidden,
		//					ResponseMessage = "You cannot access classrooms from a different school",
		//					Status = "failed"
		//				};
		//			}

		//			_logger.Information(
		//				"Classroom retrieved successfully - ClassroomId: {ClassroomId}, Name: {ClassroomName}",
		//				classroom.Id,
		//				classroom.Name);

		//			return new ClassroomDetailResponse
		//			{
		//				ResponseCode = ResponseCode.successful,
		//				ResponseMessage = "Classroom retrieved successfully",
		//				Status = "successful",
		//				Classroom = new ClassroomDto
		//				{
		//					Id = classroom.Id,
		//					Name = classroom.Name ?? string.Empty,
		//					NoOfStudents = classroom.NoOfStudents,
		//					IsActive = classroom.IsActive,
		//					CreationDate = classroom.CreationDate ?? string.Empty,
		//					ModifiedDate = classroom.ModifiedDate ?? string.Empty
		//				}
		//			};
		//		}
		//		catch (Exception ex)
		//		{
		//			_logger.Error(ex, "Error fetching classroom by ID - ClassroomId: {ClassroomId}", classroomId);

		//			return new ClassroomDetailResponse
		//			{
		//				ResponseCode = ResponseCode.ErrorOccured,
		//				ResponseMessage = "An error occurred while fetching classroom",
		//				Status = "failed"
		//			};
		//		}
		//	}
		//}
		#endregion

		//public async Task<BaseResponse> CreateStudentClass(CreateStudentClassViewModel createStudentClassViewModel)
		//{
		//	try
		//	{
		//		if (createStudentClassViewModel.classrooms.Count == 0)
		//		{
		//			throw new ArgumentNullException(nameof(createStudentClassViewModel));
		//		}
		//		var columnInput = new Dictionary<string, object> { { "Id", createStudentClassViewModel.CreatedBy }, { "SchoolId", createStudentClassViewModel.SchoolId } };
		//		string query = "select * from Users  where Id = @Id and SchoolId = @SchoolId";
		//		var user = await _queryrepositoryUser.SelectByColumns(query, columnInput);
		//		if (user == null)
		//		{
		//			return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This user does not exist", Status = "successful" };
		//		}
		//		if (!user.IsActive)
		//		{
		//			return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This user is not active", Status = "failed" };
		//		}
		//		var inputValueList = new List<Dictionary<string, object>>();
		//		foreach(var classroom in createStudentClassViewModel.classrooms)
		//		{
		//			var mappedSchClass = _mapper.Map<Classroom>(classroom);
		//			var inputValue = new Dictionary<string, object> { { "Id", mappedSchClass.Id}, {"Name", mappedSchClass.Name },
		//		{"CreationDate", mappedSchClass.CreationDate }, {"ModifiedDate", mappedSchClass.ModifiedDate }, {"CreatedBy", mappedSchClass.CreatedBy },
		//		{ "SchoolId", mappedSchClass.SchoolId}, {"NoOfStudents", mappedSchClass.NoOfStudents }, { "IsActive", mappedSchClass.IsActive} };
		//			inputValueList.Add(inputValue);
		//		}
		//		using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
		//		//await _schCommandRespository.Create(school);
		//		//await _schCommandRespository.Create(scope.Transaction, scope.Connection, insertDict);
		//		await _studentClassCommandRespository.CreateBatchAsync(scope.Transaction, scope.Connection, inputValueList);
		//		await scope.CommitAsync();
		//		return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "object updated successfully", Status = "successful" };
		//		//await _studentClassCommandRespository.Create(mappedSchClass);
		//		//return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "object updated successfully", Status = "successful" };

		//	}
		//	catch (ArgumentNullException ex)
		//	{
		//		return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = ex.Message, Status = "falied" };
		//	}
		//	catch (SqlException ex)
		//	{
		//		if (ex.Message.ToLower().Contains("duplicate"))
		//		{
		//			return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "class exists", Status = "failed" };
		//		}
		//		return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
		//	}
		//	catch (Exception ex)
		//	{
		//		return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
		//	}


		//}
		public async Task<BaseResponse> CreateSchoolSubjects(CreateSubjectViewModel createSubjectModel, AuthenticatedUserClaims userInfo)
		{
			try
			{

				if (createSubjectModel is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Subject data cannot be empty",
						Status = "failed"
					};
				}

				if (!createSubjectModel.Subjects.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "At least one subject is required",
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

				foreach (var subject in createSubjectModel.Subjects)
				{
					if (!Enum.IsDefined(typeof(SubjectCategory), subject.Category))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Invalid subject category for '{subject.Subject}'. Must be Major (1) or Minor (2)",
							Status = "failed"
						};
					}

					if (!Enum.IsDefined(typeof(ClassCategory), subject.ClassCategory))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Invalid class category for '{subject.Subject}'. Must be a valid class level",
							Status = "failed"
						};
					}
				}

				var duplicatesInRequest = createSubjectModel.Subjects
					.GroupBy(s => new
					{
						Name = s.Subject.Trim().ToLower(),
						s.Category,
						s.ClassCategory
					})
					.Where(g => g.Count() > 1)
					.Select(g => $"{g.Key.Name} ({g.Key.Category}, {g.Key.ClassCategory})")
					.ToList();

				if (duplicatesInRequest.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"Duplicate subjects found in request: {string.Join(", ", duplicatesInRequest)}",
						Status = "failed"
					};
				}

				var existingSubjects = await GetExistingSubjects(createSubjectModel.Subjects, schoolId);

				if (existingSubjects.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = $"The following subjects already exist: {string.Join(", ", existingSubjects.Select(s => $"{s.Name} ({s.Category}, {s.ClassCategory})"))}",
						Status = "failed",
						Data = new { ExistingSubjects = existingSubjects }
					};
				}


				var subjectsToCreate = new List<Dictionary<string, object>>();
				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				foreach (var subject in createSubjectModel.Subjects)
				{
					if (string.IsNullOrWhiteSpace(subject.Subject))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Subject name cannot be empty",
							Status = "failed"
						};
					}

					var subjectDict = new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "Subject", subject.Subject.Trim() },
						{ "Category", (int)subject.Category },
						{ "ClassCategory", (int)subject.ClassCategory },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "CreatedBy", createdBy },
						{ "SchoolId", schoolId },
						{ "IsActive", subject.IsActive }
					};

					subjectsToCreate.Add(subjectDict);
				}


				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				await _subjectCommandRespository.CreateBatchAsync(scope.Transaction, scope.Connection, subjectsToCreate);
				await scope.CommitAsync();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = $"{subjectsToCreate.Count} subject(s) created successfully",
					Status = "successful",
					Data = new
					{
						SubjectsCreated = subjectsToCreate.Count,
						SubjectIds = subjectsToCreate.Select(s => s["Id"]).ToList(),
						Subjects = createSubjectModel.Subjects.Select(s => new
						{
							Name = s.Subject,
							Category = s.Category.ToString(),
							CategoryValue = (int)s.Category,
							ClassCategory = s.ClassCategory.ToString(),
							ClassCategoryValue = (int)s.ClassCategory,
							IsActive = s.IsActive
						}).ToList()
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
						ResponseMessage = "One or more subjects already exist",
						Status = "failed"
					};
				}

				// Log exception here
				// _logger.LogError(ex, "SQL error occurred while creating subjects for SchoolId: {SchoolId}", schoolId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Database error occurred while creating subjects",
					Status = "failed"
				};
			}
			catch (Exception ex)
			{
				// Log exception here
				// _logger.LogError(ex, "Unexpected error occurred while creating subjects");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred while creating subjects",
					Status = "failed"
				};
			}
		}

		/// <summary>
		/// Get existing subjects to prevent duplicates
		/// </summary>
	
		private async Task<List<(string Name, string Category, string ClassCategory)>> GetExistingSubjects(List<SubjectsDetails> subjects,Guid schoolId)
		{
			try
			{
				var existingSubjects = new List<(string, string, string)>();

				foreach (var subject in subjects)
				{
					var query = @"SELECT COUNT(*) FROM Subjects 
						  WHERE LOWER(Subject) = @Subject 
						  AND Category = @Category 
						  AND ClassCategory = @ClassCategory 
						  AND SchoolId = @SchoolId 
						  AND IsActive = 1";

					var parameters = new Dictionary<string, object>
					{
						{ "Subject", subject.Subject.Trim().ToLower() },
						{ "Category", (int)subject.Category },
						{ "ClassCategory", (int)subject.ClassCategory },
						{ "SchoolId", schoolId }
					};

					var count = await _queryrepositorySubject.CountAsync(query, parameters);
					if (count > 0)
					{
						existingSubjects.Add((
							subject.Subject,
							subject.Category.ToString(),
							subject.ClassCategory.ToString()
						));
					}
				}

				return existingSubjects;
			}
			catch
			{
				return new List<(string, string, string)>();
			}
		}
		public async Task<BaseResponse> GetAllSubjects(Guid schoolid)
		{
			try
			{
				var keyValue = new KeyValuePair<string, object>("schoolId", schoolid);

				var subjects = await _queryrepositorySubject.SelectAllBySingleColumn(keyValue);
				if(!subjects.Any())
				{
					return new AllSubjectResponseModel {AllSubjects= new List<SubjectResponseModel>(), ResponseCode = ResponseCode.successful, ResponseMessage = "No registered Subjects", Status = "successful" };
				}
				var subjectMapped = _mapper.Map<List<SubjectResponseModel>>(subjects.ToList());
				return new AllSubjectResponseModel {AllSubjects= subjectMapped, ResponseCode = ResponseCode.successful, ResponseMessage = ResponseMessage.ResponseSucessful, Status = "successful" };

			}
			catch (ArgumentNullException ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = ex.Message, Status = "falied" };
			}
			catch (Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };

			}
		}
		/// <summary>
		/// this service register classroom subject...register subject to classroom
		/// </summary>
		/// <param name="createClassroomViewModel"></param>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		#region
		public async Task<BaseResponse> RegisterClassroomSubjects(CreateClassroomViewModel createClassroomViewModel, AuthenticatedUserClaims userInfo)
		{
			try
			{
				
				if (createClassroomViewModel is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Classroom subjects data cannot be empty",
						Status = "failed"
					};
				}

				// Validation 2: Check if subjects list is empty
				if (!createClassroomViewModel.SubjectIds.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "At least one subject is required",
						Status = "failed"
					};
				}

				// Validation 3: Validate user claims
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

				// Validation 4: Parse claims to Guid
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

				// Validation 5: Check for empty Guids in subject list
				if (createClassroomViewModel.SubjectIds.Any(id => id == Guid.Empty))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid subject IDs found in the list",
						Status = "failed"
					};
				}

				// Validation 6: Check for duplicate subjects in request
				var duplicateSubjects = createClassroomViewModel.SubjectIds
					.GroupBy(id => id)
					.Where(g => g.Count() > 1)
					.Select(g => g.Key)
					.ToList();

				if (duplicateSubjects.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Duplicate subjects found in the request",
						Status = "failed"
					};
				}

				// Validation 7: Verify classroom exists and belongs to the school
				var classroom = await VerifyClassroomExists(createClassroomViewModel.ClassroomId, schoolId);
				if (!classroom.Exists)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Classroom not found",
						Status = "failed"
					};
				}

				if (!classroom.BelongsToSchool)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You can only manage classrooms in your own school",
						Status = "failed"
					};
				}

				// Validation 8: Verify all subjects exist and belong to the school
				var subjectValidation = await VerifySubjectsExist(createClassroomViewModel.SubjectIds, schoolId);
				if (subjectValidation.MissingSubjects.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"The following subjects were not found: {string.Join(", ", subjectValidation.MissingSubjects)}",
						Status = "failed"
					};
				}

				if (subjectValidation.InvalidSchoolSubjects.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "Some subjects do not belong to your school",
						Status = "failed"
					};
				}

				// Validation 9: Check for existing classroom-subject associations
				var existingAssociations = await GetExistingClassroomSubjects(
					createClassroomViewModel.ClassroomId,
					createClassroomViewModel.SubjectIds
				);

				if (existingAssociations.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = $"{existingAssociations.Count} subject(s) are already assigned to this classroom",
						Status = "failed",
						Data = new { ExistingSubjects = existingAssociations }
					};
				}


				var inputValues = new List<Dictionary<string, object>>();
				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				foreach (var subjectId in createClassroomViewModel.SubjectIds)
				{
					var classroomSubject = new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "ClassroomId", createClassroomViewModel.ClassroomId },
						{ "SubjectId", subjectId },
						{ "SchoolId", schoolId },
						{ "CreatedBy", createdBy },
						{ "IsActive", true }
					};

					inputValues.Add(classroomSubject);
				}


				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				await _classroomSubjectCommandRespository.CreateBatchAsync(scope.Transaction, scope.Connection, inputValues);
				await scope.CommitAsync();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = $"{inputValues.Count} subject(s) assigned to classroom successfully",
					Status = "successful",
					Data = new
					{
						ClassroomId = createClassroomViewModel.ClassroomId,
						SubjectsAssigned = inputValues.Count,
						SubjectIds = createClassroomViewModel.SubjectIds
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
						ResponseMessage = "One or more subjects are already assigned to this classroom",
						Status = "failed"
					};
				}

				// Log exception
				// _logger.LogError(ex, "SQL error occurred while assigning subjects to classroom");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Database error occurred while assigning subjects",
					Status = "failed"
				};
			}
			catch (Exception ex)
			{
				// Log exception
				// _logger.LogError(ex, "Unexpected error occurred while assigning subjects to classroom");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred while assigning subjects",
					Status = "failed"
				};
			}
		}
		#endregion
		/// <summary>
		/// this service is the get classroom details , teachers, classroom info ect
		/// </summary>
		/// <param name="updateSchoolSubjects"></param>
		/// <returns></returns>
		#region
		public async Task<BaseResponse> GetClassroomDetailsById(Guid classroomId,AuthenticatedUserClaims userInfo)
		{
			using (LogContext.PushProperty("RequestedBy", userInfo.UserId))
			//using (LogContext.PushProperty("TenantId", userInfo.TenantIdentifier))
			{
				try
				{
					// Parse SchoolId
					if (!Guid.TryParse(userInfo.SchoolId, out var schoolId))
					{
						_logger.Warning("Invalid SchoolId format in token - SchoolId: {SchoolId}", userInfo.SchoolId);

						return new ClassroomDetailResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format in token",
							Status = "failed"
						};
					}

					_logger.Information("Fetching classroom details by ID - ClassroomId: {ClassroomId}", classroomId);

					// Get classroom
					var classroom = await _studentClassQueryRespository.Get(classroomId);

					if (classroom == null)
					{
						_logger.Warning("Classroom not found - ClassroomId: {ClassroomId}", classroomId);

						return new ClassroomDetailResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Classroom not found",
							Status = "failed"
						};
					}

					// Multi-tenancy check
					if (classroom.SchoolId != schoolId)
					{
						_logger.Warning(
							"Unauthorized access attempt - User {UserId} tried to access classroom {ClassroomId} from different school",
							userInfo.UserId,
							classroomId);

						return new ClassroomDetailResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You cannot access classrooms from a different school",
							Status = "failed"
						};
					}

					// Get active teachers assigned to this classroom
					var teachers = await GetActiveTeachersForClassroom(classroomId);

					_logger.Information(
						"Classroom details retrieved successfully - ClassroomId: {ClassroomId}, Name: {ClassroomName}, TeacherCount: {TeacherCount}",
						classroom.Id,
						classroom.Name,
						teachers.Count);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Classroom details retrieved successfully",
						Status = "successful",
						Data = new 
						{
							Id = classroom.Id,
							Name = classroom.Name ?? string.Empty,
							NoOfStudents = classroom.NoOfStudents,
							IsActive = classroom.IsActive,
							CreationDate = classroom.CreationDate ?? string.Empty,
							ModifiedDate = classroom.ModifiedDate ?? string.Empty,
							Teachers = teachers
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error fetching classroom details - ClassroomId: {ClassroomId}", classroomId);

					return new ClassroomDetailResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching classroom details",
						Status = "failed"
					};
				}
			}
		}

		private async Task<List<ClassroomTeacherDto>> GetActiveTeachersForClassroom(Guid classroomId)
		{
			try
			{
				var query = @"
					SELECT 
						ct.TeacherId,
						ct.IsPrimary,
						u.FirstName,
						u.LastName,
						u.EmailAddress,
						u.RoleId
					FROM ClassroomTeacher ct
					INNER JOIN Users u ON ct.TeacherId = u.Id
					WHERE ct.ClassroomId = @ClassroomId
					AND ct.IsActive = 1
					AND u.IsActive = 1
					ORDER BY ct.IsPrimary DESC, u.FirstName, u.LastName";

				var parameters = new Dictionary<string, object>
				{
					{ "ClassroomId", classroomId }
				};

				var results = await _classroomTeacherQueryRespository.QueryAsync<ClassroomTeacherDto>(query, parameters);

				_logger.Debug(
					"Found {TeacherCount} active teachers for classroom - ClassroomId: {ClassroomId}",
					results.Count(),
					classroomId);

				return results.ToList();
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching teachers for classroom - ClassroomId: {ClassroomId}", classroomId);
				return new List<ClassroomTeacherDto>();
			}
		}
		#endregion
		public async Task<BaseResponse> UpdateSchoolId(updateSchoolSubject updateSchoolSubjects)
		{
			try
			{
				if (updateSchoolSubjects.subjectUpdates.Count == 0)
				{
					throw new ArgumentNullException(nameof(updateSchoolSubject));
				};
				var columnInput = new Dictionary<string, object> { { "Id", updateSchoolSubjects.CreatedBy }, { "SchoolId", "" } };
				string query = "select * from Users where Id = @Id and SchoolId = @SchoolId";
				var user = await _queryrepositoryUser.SelectByColumns(query, columnInput);
				if (user == null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This user does not exist", Status = "failed" };
				}
				if (!user.IsActive)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This user is not active", Status = "failed" };
				}
				var inputValueList = new List<Dictionary<string, object>>();
				foreach (var subject in updateSchoolSubjects.subjectUpdates)
				{
					var values = new Dictionary<string, object> { { "Subject", subject.Subject }, { "IsActive", subject.isDeleted },
					{ "ModifiedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},{"CreatedBy",updateSchoolSubjects.CreatedBy },{ "Id", subject.subjectId}  };
					inputValueList.Add(values);
				}
				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				await _subjectCommandRespository.UpdateBatchByIdAsync(scope.Transaction, scope.Connection, inputValueList);
				await scope.CommitAsync();
				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = ResponseMessage.ResponseSucessful, Status = "successful" };

			}

			catch (ArgumentNullException ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = ex.Message, Status = "falied" };
			}
			catch (Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };

			}


		}
		/// <summary>
		/// this endpoint assigns teachers to classrooms
		/// </summary>
		/// <param name="assignTeachersViewModel"></param>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		#region
		public async Task<BaseResponse> AssignTeachersToClassroom(AssignTeacherViewModel assignTeachersViewModel, AuthenticatedUserClaims userInfo)
		{
			using (LogContext.PushProperty("RequestedBy", userInfo.UserId))
			//using (LogContext.PushProperty("TenantId", userInfo.TenantIdentifier))
			{
				try
				{
					if (assignTeachersViewModel is null)
					{
						_logger.Warning("Assign teachers request with null data");

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Assignment data cannot be empty",
							Status = "failed"
						};
					}

					if (assignTeachersViewModel.ClassroomId == Guid.Empty)
					{
						_logger.Warning("Empty ClassroomId in assignment");

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "ClassroomId cannot be empty",
							Status = "failed"
						};
					}

					if (!assignTeachersViewModel.TeacherIds.Any())
					{
						_logger.Warning("Assign teachers request with empty teacher list - ClassroomId: {ClassroomId}",
							assignTeachersViewModel.ClassroomId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "At least one teacher is required",
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

					_logger.Information(
						"Assigning {TeacherCount} teacher(s) to classroom - ClassroomId: {ClassroomId}",
						assignTeachersViewModel.TeacherIds.Count,
						assignTeachersViewModel.ClassroomId);

					// Validation 6: Check if classroom exists
					var classroom = await _studentClassQueryRespository.Get(assignTeachersViewModel.ClassroomId);

					if (classroom == null)
					{
						_logger.Warning("Classroom not found - ClassroomId: {ClassroomId}",
							assignTeachersViewModel.ClassroomId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Classroom not found",
							Status = "failed"
						};
					}

					if (classroom.SchoolId != schoolId)
					{
						_logger.Warning(
							"Classroom belongs to different school - ClassroomId: {ClassroomId}, ClassroomSchoolId: {ClassroomSchoolId}, RequestSchoolId: {RequestSchoolId}",
							assignTeachersViewModel.ClassroomId,
							classroom.SchoolId,
							schoolId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Classroom belongs to a different school",
							Status = "failed"
						};
					}

					var assignmentsToCreate = new List<Dictionary<string, object>>();
					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
					var assignedTeacherNames = new List<string>();
					var isPrimaryAssigned = false;

					foreach (var teacherId in assignTeachersViewModel.TeacherIds)
					{
						if (teacherId == Guid.Empty)
						{
							_logger.Warning("Empty TeacherId in assignment for ClassroomId: {ClassroomId}",
								assignTeachersViewModel.ClassroomId);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = "TeacherId cannot be empty",
								Status = "failed"
							};
						}

						var teacher = await _queryrepositoryUser.Get(teacherId);

						if (teacher == null)
						{
							_logger.Warning("Teacher not found - TeacherId: {TeacherId}", teacherId);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.NotFound,
								ResponseMessage = $"Teacher with ID {teacherId} not found",
								Status = "failed"
							};
						}

						// Validation 10: Check if teacher belongs to same school
						if (teacher.SchoolId != schoolId)
						{
							_logger.Warning(
								"Teacher belongs to different school - TeacherId: {TeacherId}, TeacherSchoolId: {TeacherSchoolId}, RequestSchoolId: {RequestSchoolId}",
								teacherId,
								teacher.SchoolId,
								schoolId);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = $"Teacher {teacher.FirstName} {teacher.LastName} belongs to a different school",
								Status = "failed"
							};
						}

						if (!teacher.IsActive)
						{
							_logger.Warning("Teacher is not active - TeacherId: {TeacherId}", teacherId);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = $"Teacher {teacher.FirstName} {teacher.LastName} is not active",
								Status = "failed"
							};
						}

						//var teacherRole = (UserRole)teacher.RoleId;
						//if (teacherRole != UserRole.SubjectTeacher &&
						//	teacherRole != UserRole.HeadTeacher &&
						//	teacherRole != UserRole.Administrator &&
						//	teacherRole != UserRole.SuperAdministrator)
						//{
						//	_logger.Warning(
						//		"User is not a teacher - UserId: {UserId}, Role: {Role}",
						//		teacherId,
						//		teacherRole);

						//	return new BaseResponse
						//	{
						//		ResponseCode = ResponseCode.BadRequest,
						//		ResponseMessage = $"User {teacher.FirstName} {teacher.LastName} is not a teacher (Role: {teacherRole})",
						//		Status = "failed"
						//	};
						//}

						// Validation 13: Check if teacher is already assigned to this classroom
						var existingAssignment = await CheckTeacherAlreadyAssigned(
							assignTeachersViewModel.ClassroomId,
							teacherId);

						if (existingAssignment)
						{
							_logger.Warning(
								"Teacher already assigned to classroom - TeacherId: {TeacherId}, ClassroomId: {ClassroomId}",
								teacherId,
								assignTeachersViewModel.ClassroomId);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.Conflict,
								ResponseMessage = $"Teacher {teacher.FirstName} {teacher.LastName} is already assigned to this classroom",
								Status = "failed"
							};
						}

						var isPrimary = !isPrimaryAssigned;
						if (isPrimary) isPrimaryAssigned = true;

						var assignmentDict = new Dictionary<string, object>
						{
							{ "Id", Guid.NewGuid() },
							{ "ClassroomId", assignTeachersViewModel.ClassroomId },
							{ "TeacherId", teacherId },
							{ "IsPrimary", isPrimary },
							{ "CreationDate", now },
							{ "ModifiedDate", now },
							{ "CreatedBy", createdBy },
							{ "SchoolId", schoolId },
							{ "IsActive", true }
						};

						assignmentsToCreate.Add(assignmentDict);
						assignedTeacherNames.Add($"{teacher.FirstName} {teacher.LastName}" + (isPrimary ? " (Primary)" : ""));

						_logger.Debug(
							"Prepared teacher assignment - ClassroomId: {ClassroomId}, TeacherId: {TeacherId}, TeacherName: {TeacherName}, IsPrimary: {IsPrimary}",
							assignTeachersViewModel.ClassroomId,
							teacherId,
							$"{teacher.FirstName} {teacher.LastName}",
							isPrimary);
					}

					// Use batch insert
					using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");

					await _classroomTeacherCommandRepository.CreateBatchAsync(
						scope.Transaction,
						scope.Connection,
						assignmentsToCreate);

					await scope.CommitAsync();

					_logger.Information(
						"Successfully assigned {TeacherCount} teacher(s) to classroom - ClassroomId: {ClassroomId}, ClassroomName: {ClassroomName}, Teachers: {Teachers}",
						assignmentsToCreate.Count,
						assignTeachersViewModel.ClassroomId,
						classroom.Name,
						string.Join(", ", assignedTeacherNames));

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = $"{assignmentsToCreate.Count} teacher(s) assigned successfully to classroom '{classroom.Name}'",
						Status = "successful",
						Data = new
						{
							ClassroomId = assignTeachersViewModel.ClassroomId,
							ClassroomName = classroom.Name,
							TeachersAssigned = assignmentsToCreate.Count,
							TeacherNames = assignedTeacherNames,
							AssignmentIds = assignmentsToCreate.Select(a => a["Id"]).ToList()
						}
					};
				}
				catch (SqlException ex)
				{
					_logger.Error(
						ex,
						"SQL error assigning teachers - ClassroomId: {ClassroomId}",
						assignTeachersViewModel?.ClassroomId);

					if (ex.Message.ToLower().Contains("duplicate") || ex.Number == 2627)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Conflict,
							ResponseMessage = "One or more teachers are already assigned to this classroom",
							Status = "failed"
						};
					}

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "Database error occurred while assigning teachers",
						Status = "failed"
					};
				}
				catch (Exception ex)
				{
					_logger.Error(
						ex,
						"Unexpected error assigning teachers - ClassroomId: {ClassroomId}",
						assignTeachersViewModel?.ClassroomId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An unexpected error occurred while assigning teachers",
						Status = "failed"
					};
				}
			}
		}
		#endregion

		/// <summary>
		/// this service method is to get all all subjects
		/// </summary>
		/// <param name="classroomId"></param>
		/// <param name="teacherId"></param>
		/// <returns></returns>
		#region

		public async Task<SubjectsListResponse> GetAllSubjects(AuthenticatedUserClaims userInfo,int? classCategory = null,int? subjectCategory = null,int pageNumber = 1,int pageSize = 50)
		{
			using (LogContext.PushProperty("RequestedBy", userInfo.UserId))
			//using (LogContext.PushProperty("TenantId", userInfo.TenantIdentifier))
			{
				try
				{
					// Parse SchoolId
					if (!Guid.TryParse(userInfo.SchoolId, out var schoolId))
					{
						_logger.Warning("Invalid SchoolId format in token - SchoolId: {SchoolId}", userInfo.SchoolId);

						return new SubjectsListResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format in token",
							Status = "failed"
						};
					}

					// Validate pagination
					if (pageNumber < 1) pageNumber = 1;
					if (pageSize < 1 || pageSize > 100) pageSize = 50;

					// Validate categories if provided
					if (classCategory.HasValue && !Enum.IsDefined(typeof(ClassCategory), classCategory.Value))
					{
						return new SubjectsListResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid class category",
							Status = "failed"
						};
					}

					if (subjectCategory.HasValue && !Enum.IsDefined(typeof(SubjectCategory), subjectCategory.Value))
					{
						return new SubjectsListResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid subject category",
							Status = "failed"
						};
					}

					_logger.Information(
						"Fetching subjects - SchoolId: {SchoolId}, ClassCategory: {ClassCategory}, SubjectCategory: {SubjectCategory}, Page: {PageNumber}",
						schoolId,
						classCategory?.ToString() ?? "All",
						subjectCategory?.ToString() ?? "All",
						pageNumber);

					var query = $"SELECT * FROM Subjects WHERE SchoolId = '{schoolId}'";

					if (classCategory.HasValue)
					{
						query += $" AND ClassCategory = {classCategory.Value}";
					}

					if (subjectCategory.HasValue)
					{
						query += $" AND Category = {subjectCategory.Value}";
					}

					query += " ORDER BY Subject";

					var allSubjects = await _queryrepositorySubject.GetByQuery(query);
					var subjectsList = allSubjects.Where(s => s != null).ToList();

					// Pagination
					var totalCount = subjectsList.Count;
					var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

					var paginatedSubjects = subjectsList
						.Skip((pageNumber - 1) * pageSize)
						.Take(pageSize)
						.ToList();

					// Map to DTOs
					var subjectDtos = paginatedSubjects.Select(s => new SubjectDto
					{
						Id = s!.Id,
						Name = s.Subject,
						ClassCategory = (int)s.ClassCategory,
						ClassCategoryName = ((ClassCategory)s.ClassCategory).ToString(),
						SubjectCategory = (int)s.Category,
						SubjectCategoryName = ((SubjectCategory)s.Category).ToString(),
						IsActive = s.IsActive,
					
					}).ToList();

					// Build filter description
					var filterParts = new List<string>();
					if (classCategory.HasValue)
						filterParts.Add($"Class: {((ClassCategory)classCategory.Value)}");
					if (subjectCategory.HasValue)
						filterParts.Add($"Subject: {((SubjectCategory)subjectCategory.Value)}");
					var filteredBy = filterParts.Any() ? string.Join(", ", filterParts) : "All Subjects";

					_logger.Information(
						"Successfully fetched {SubjectCount} subject(s) - Filter: {Filter}, Page: {PageNumber}/{TotalPages}",
						subjectDtos.Count,
						filteredBy,
						pageNumber,
						totalPages);

					return new SubjectsListResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "Subjects retrieved successfully",
						Status = "successful",
						Data = new SubjectsListData
						{
							Subjects = subjectDtos,
							TotalCount = totalCount,
							PageNumber = pageNumber,
							PageSize = pageSize,
							TotalPages = totalPages,
							HasPreviousPage = pageNumber > 1,
							HasNextPage = pageNumber < totalPages,
							FilteredBy = filteredBy
						}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error fetching subjects - SchoolId: {SchoolId}", userInfo.SchoolId);

					return new SubjectsListResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching subjects",
						Status = "failed"
					};
				}
			}
		}
		#endregion

		/// <summary>
		/// this method we can fetch subject based on category
		/// </summary>
		/// <param name="classroomId"></param>
		/// <param name="teacherId"></param>
		/// <returns></returns>
		/// 

		#region
		public async Task<SubjectsListResponse> GetSubjectsByClassCategory(int classCategory,AuthenticatedUserClaims userInfo,int pageNumber = 1,int pageSize = 50)
		{
			return await GetAllSubjects(userInfo, classCategory, null, pageNumber, pageSize);
		}

		#endregion

		#region GetSubjectsBySubjectCategory

		public async Task<SubjectsListResponse> GetSubjectsBySubjectCategory(int subjectCategory,AuthenticatedUserClaims userInfo,int pageNumber = 1,int pageSize = 50)
		{
			return await GetAllSubjects(userInfo, null, subjectCategory, pageNumber, pageSize);
		}
		private async Task<bool> CheckTeacherAlreadyAssigned(Guid classroomId, Guid teacherId)
		{
			try
			{
				var query = $@"
					SELECT COUNT(1) 
					FROM ClassroomTeacher 
					WHERE ClassroomId = '{classroomId}' 
					AND TeacherId = '{teacherId}'
					AND IsActive = 1";

				var count = await _classroomTeacherQueryRespository.CountAsync(query, new Dictionary<string, object>());
				return count > 0;
			}
			catch (Exception ex)
			{
				_logger.Error(
					ex,
					"Error checking teacher assignment - ClassroomId: {ClassroomId}, TeacherId: {TeacherId}",
					classroomId,
					teacherId);
				return true; 
			}
		}
		#endregion
		/// <summary>
		/// this endpoint is for update classroom information
		/// </summary>
		/// <param name="updateClassroomView"></param>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		#region
		public async Task<BaseResponse> UpdateSchoolClassroom(UpdateClassroomView updateClassroomView, AuthenticatedUserClaims userInfo)
		{
			try
			{
				// Validation 1: Check if model is null
				if (updateClassroomView is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Update data cannot be empty",
						Status = "failed"
					};
				}

				if (!updateClassroomView.classroomUpdateViews.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "At least one classroom is required for update",
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

				// Validation 4: Parse claims to Guid
				if (!Guid.TryParse(userInfo.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId format in token",
						Status = "failed"
					};
				}

				if (!Guid.TryParse(userInfo.UserId, out var modifiedBy))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid UserId format in token",
						Status = "failed"
					};
				}

				// Validation 5: Check for duplicate IDs in request
				var duplicateIds = updateClassroomView.classroomUpdateViews
					.GroupBy(c => c.Id)
					.Where(g => g.Count() > 1)
					.Select(g => g.Key)
					.ToList();

				if (duplicateIds.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Duplicate classroom IDs found in request",
						Status = "failed"
					};
				}

				var classroomIds = updateClassroomView.classroomUpdateViews.Select(c => c.Id).ToList();
				var existingClassrooms = await VerifyClassroomsExist(classroomIds, schoolId);

				if (existingClassrooms.MissingIds.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"The following classrooms were not found: {string.Join(", ", existingClassrooms.MissingIds)}",
						Status = "failed"
					};
				}

				if (existingClassrooms.InvalidSchoolIds.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You can only update classrooms in your own school",
						Status = "failed"
					};
				}

				// Validation 7: Check for duplicate names within the update request
				var duplicateNames = updateClassroomView.classroomUpdateViews
					.GroupBy(c => c.Name.Trim().ToLower())
					.Where(g => g.Count() > 1)
					.Select(g => g.Key)
					.ToList();

				if (duplicateNames.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"Duplicate classroom names found: {string.Join(", ", duplicateNames)}",
						Status = "failed"
					};
				}

				var nameConflicts = await CheckNameConflicts(updateClassroomView.classroomUpdateViews,schoolId);

				if (nameConflicts.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = $"The following classroom names already exist: {string.Join(", ", nameConflicts)}",
						Status = "failed"
					};
				}

				var inputValueList = new List<Dictionary<string, object>>();
				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				foreach (var classroom in updateClassroomView.classroomUpdateViews)
				{
					if (string.IsNullOrWhiteSpace(classroom.Name))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Classroom name cannot be empty",
							Status = "failed"
						};
					}

					var values = new Dictionary<string, object>
					{
						{ "Id", classroom.Id },
						{ "Name", classroom.Name.Trim() },
						{ "IsActive", classroom.IsActive },
						{ "ModifiedDate", now },
						{ "SchoolId", schoolId } 
					};

							inputValueList.Add(values);
				}

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				await _studentClassCommandRespository.UpdateBatchByIdAsyncV2(scope.Transaction, scope.Connection, inputValueList);
				await scope.CommitAsync();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = $"{inputValueList.Count} classroom(s) updated successfully",
					Status = "successful",
					Data = new
					{
						ClassroomsUpdated = inputValueList.Count,
						ClassroomIds = inputValueList.Select(v => v["Id"]).ToList(),
						UpdatedAt = now
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
						ResponseMessage = "One or more classroom names already exist",
						Status = "failed"
					};
				}

				// Log exception here
				// _logger.LogError(ex, "SQL error occurred while updating classrooms");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "Database error occurred while updating classrooms",
					Status = "failed"
				};
			}
			catch (Exception ex)
			{
				// Log exception here
				// _logger.LogError(ex, "Unexpected error occurred while updating classrooms");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred while updating classrooms",
					Status = "failed"
				};
			}
		}
		/// <summary>
		/// Check if classroom name already exists for the school
		/// </summary>
		private async Task<bool> CheckDuplicateClassroom(string classroomName, Guid schoolId)
		{
			try
			{
				var query = "SELECT COUNT(*) FROM Classroom WHERE Name = @Name AND SchoolId = @SchoolId AND IsActive = 1";
				var parameters = new Dictionary<string, object>
				{
					{ "Name", classroomName.Trim() },
					{ "SchoolId", schoolId }
				};

				var count = await _studentClassQueryRespository.CountAsync(query, parameters);
				return count > 0;
			}
			catch
			{
				return false;
			}
		}
		#endregion



		public async Task<TopicListResponse> GetTopics(Guid subjectId,AuthenticatedUserClaims userClaims)
		{
			try
			{
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					return new TopicListResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};

				var query = $@"
					SELECT Id, SubjectId, Name, IsActive
					FROM Topic
					WHERE SubjectId = '{subjectId}'
					AND   SchoolId  = '{schoolId}'
					AND   IsDeleted = 0
					AND   IsActive  = 1
					ORDER BY Name ASC";

				var results = await _topicQueryRepository.GetByQuery(query, DatabaseTarget.QuestionBank);

				var topics = results?.Select(t => new TopicDto
				{
					Id = t.Id,
					SubjectId = t.SubjectId,
					Name = t.Name,
					IsActive = t.IsActive
				}).ToList() ?? new List<TopicDto>();

				return new TopicListResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Topics retrieved successfully",
					Status = "successful",
					Topics = topics
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error getting topics");
				return new TopicListResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving topics",
					Status = "failed"
				};
			}
		}
		/// <summary>
		/// Get existing subjects for the school to prevent duplicates
		/// </summary>
		/// 

		

		private async Task<List<string>> GetExistingSubjects(List<string> subjectNames, Guid schoolId)
		{
			try
			{
				var existingSubjects = new List<string>();

				foreach (var subjectName in subjectNames)
				{
					var query = "SELECT COUNT(*) FROM Subjects WHERE LOWER(Subject) = @Subject AND SchoolId = @SchoolId AND IsActive = 1";
					var parameters = new Dictionary<string, object>
					{
						{ "Subject", subjectName.Trim().ToLower() },
						{ "SchoolId", schoolId }
					};

					var count = await _queryrepositorySubject.CountAsync(query, parameters);
					if (count > 0)
					{
						existingSubjects.Add(subjectName);
					}
				}

				return existingSubjects;
			}
			catch
			{
				return new List<string>();
			}
		}

		/// <summary>
		/// this service update the logo of the school 
		/// </summary>
		/// <param name="logo"></param>
		/// <param name="userClaims"></param>
		/// <returns></returns>
		public async Task<BaseResponse> UpdateSchoolLogoAsync(IFormFile logo, AuthenticatedUserClaims userClaims)
		{
			try
			{
				// Validate claims
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId",
						Status = "failed"
					};
				}

				// Validate file
				if (logo == null || logo.Length == 0)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Logo file is required",
						Status = "failed"
					};
				}

				// Validate file type
				var allowedTypes = new[]
				{"image/jpeg", "image/png", "image/webp", "image/svg+xml"};

				if (!allowedTypes.Contains(logo.ContentType.ToLower()))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Only JPEG, PNG, WebP " + "and SVG files are allowed",
						Status = "failed"
					};
				}

				// Validate file size — max 2MB for logos
				if (logo.Length > 2 * 1024 * 1024)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Logo must be less than 2MB",
						Status = "failed"
					};
				}

				// Get existing school to check for old logo
				var school = await _schQueryRepository.GetByQuery($"SELECT * FROM School WHERE Id = '{schoolId}'");

				var existingSchool = school.FirstOrDefault();

				// Delete old logo from Cloudinary if exists
				if (existingSchool != null && !string.IsNullOrEmpty(existingSchool.LogoPublicId))
				{
					await _cloudinaryService.DeleteMediaAsync(existingSchool.LogoPublicId,MediaType.Image);

					_logger.Information("Old logo deleted - PublicId: {PublicId}", existingSchool.LogoPublicId);
				}

				// Upload new logo
				using var stream = logo.OpenReadStream();

				var uploadResult = await _cloudinaryService.UploadSchoolLogoAsync(stream,logo.FileName,schoolId);

				if (!uploadResult.Success)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "Failed to upload logo: " + uploadResult.ErrorMessage,
						Status = "failed"
					};
				}

				// Save logo URL and PublicId to DB
				var updateDict = new Dictionary<string, object>
				{
					{ "LogoUrl", uploadResult.SecureUrl },
					{ "LogoPublicId", uploadResult.PublicId },
					{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
				};

				await _schCommandRespository.UpdateTableColumnById(updateDict,new KeyValuePair<string, object>("Id", schoolId));

				_logger.Information("School logo updated - SchoolId: {SchoolId}, " + "Url: {Url}",schoolId,uploadResult.SecureUrl);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "School logo updated successfully",
					Status = "successful",
					Data = new UpdateSchoolLogoResponse
					{
						LogoUrl = uploadResult.SecureUrl,
						PublicId = uploadResult.PublicId
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,"Error updating school logo - SchoolId: {SchoolId}", userClaims.SchoolId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while " + "updating school logo",
					Status = "failed"
				};
			}
		}

		/// <summary>
		/// Verify that all classrooms exist and belong to the correct school
		/// </summary>
		private async Task<(List<Guid> MissingIds, List<Guid> InvalidSchoolIds)> VerifyClassroomsExist(List<Guid> classroomIds, Guid schoolId)
		{
			try
			{
				var missingIds = new List<Guid>();
				var invalidSchoolIds = new List<Guid>();

				foreach (var classroomId in classroomIds)
				{
					var query = "SELECT SchoolId FROM Classroom WHERE Id = @Id";
					var parameters = new Dictionary<string, object>
					{
						{ "Id", classroomId }
					};

					var result = await _studentClassQueryRespository.SelectByColumns(query, parameters);

					if (result == null)
					{
						missingIds.Add(classroomId);
					}
					else if (result.SchoolId != schoolId)
					{
						invalidSchoolIds.Add(classroomId);
					}
				}

				return (missingIds, invalidSchoolIds);
			}
			catch
			{
				return (classroomIds, new List<Guid>());
			}
		}

		/// <summary>
		/// Check if updated names conflict with existing classrooms (excluding the ones being updated)
		/// </summary>
		private async Task<List<string>> CheckNameConflicts(List<ClassroomUpdateView> classrooms, Guid schoolId)
		{
			try
			{
				var conflicts = new List<string>();

				foreach (var classroom in classrooms)
				{
					// Check if name exists for a different classroom in the same school
					var query = "SELECT COUNT(*) FROM Classroom WHERE LOWER(Name) = @Name AND SchoolId = @SchoolId AND Id != @Id AND IsActive = 1";
					var parameters = new Dictionary<string, object>
					{
						{ "Name", classroom.Name.Trim().ToLower() },
						{ "SchoolId", schoolId },
						{ "Id", classroom.Id }
					};

					var count = await _studentClassQueryRespository.CountAsync(query, parameters);
					if (count > 0)
					{
						conflicts.Add(classroom.Name);
					}
				}

				return conflicts;
			}
			catch
			{
				return new List<string>();
			}
		}

		/// <summary>
		/// Verify classroom exists and belongs to the school
		/// </summary>
		private async Task<(bool Exists, bool BelongsToSchool)> VerifyClassroomExists(Guid classroomId, Guid schoolId)
		{
			try
			{
				var query = "SELECT SchoolId FROM Classroom WHERE Id = @ClassroomId AND IsActive = 1";
				var parameters = new Dictionary<string, object>
				{
					{ "ClassroomId", classroomId }
				};

				var classroom = await _studentClassQueryRespository.SelectByColumns(query, parameters);

				if (classroom == null)
				{
					return (false, false);
				}

				return (true, classroom.SchoolId == schoolId);
			}
			catch
			{
				return (false, false);
			}
		}

		/// <summary>
		/// Verify all subjects exist and belong to the school
		/// </summary>
		private async Task<(List<Guid> MissingSubjects, List<Guid> InvalidSchoolSubjects)> VerifySubjectsExist(
			List<Guid> subjectIds,
			Guid schoolId)
		{
			try
			{
				var missingSubjects = new List<Guid>();
				var invalidSchoolSubjects = new List<Guid>();

				foreach (var subjectId in subjectIds)
				{
					var query = "SELECT SchoolId FROM Subjects WHERE Id = @SubjectId AND IsActive = 1";
					var parameters = new Dictionary<string, object>
					{
						{ "SubjectId", subjectId }
					};

					var subject = await  _queryrepositorySubject.SelectByColumns(query, parameters);

					if (subject == null)
					{
						missingSubjects.Add(subjectId);
					}
					else if (subject.SchoolId != schoolId)
					{
						invalidSchoolSubjects.Add(subjectId);
					}
				}

				return (missingSubjects, invalidSchoolSubjects);
			}
			catch
			{
				return (subjectIds, new List<Guid>());
			}
		}

		/// <summary>
		/// Get existing classroom-subject associations to prevent duplicates
		/// </summary>
		private async Task<List<Guid>> GetExistingClassroomSubjects(Guid classroomId, List<Guid> subjectIds)
		{
			try
			{
				var existingSubjects = new List<Guid>();

				foreach (var subjectId in subjectIds)
				{
					var query = "SELECT COUNT(*) FROM ClassroomSubjects WHERE ClassroomId = @ClassroomId AND SubjectId = @SubjectId AND IsActive = 1";
					var parameters = new Dictionary<string, object>
					{
						{ "ClassroomId", classroomId },
						{ "SubjectId", subjectId }
					};

					var count = await _classroomSubjectQueryRespository.CountAsync(query, parameters);
					if (count > 0)
					{
						existingSubjects.Add(subjectId);
					}
				}

				return existingSubjects;
			}
			catch
			{
				return new List<Guid>();
			}
		}

		public async Task<BaseResponse> GetSubjectById(Guid subjectId, AuthenticatedUserClaims userClaims)
		{
			try
			{
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId format in token",
						Status = "failed"
					};
				}

				var subject = await _queryrepositorySubject.Get(subjectId);

				if (subject is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Subject not found",
						Status = "failed"
					};
				}

				if (subject.SchoolId != schoolId)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You cannot access subjects from a different school",
						Status = "failed"
					};
				}

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Subject retrieved successfully",
					Status = "successful",
					Data = new
					{
						subject.Id,
						subject.Subject,
						Category = subject.Category.ToString(),
						ClassCategory = subject.ClassCategory.ToString(),
						subject.SchoolId,
						subject.IsActive,
						subject.CreationDate,
						subject.ModifiedDate
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching subject by ID - SubjectId: {SubjectId}", subjectId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching subject",
					Status = "failed"
				};
			}
		}

		public async Task<BaseResponse> GetClassroomById(Guid classroomId, AuthenticatedUserClaims userClaims)
		{
			try
			{
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId format in token",
						Status = "failed"
					};
				}

				var classroom = await _studentClassQueryRespository.Get(classroomId);

				if (classroom is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Classroom not found",
						Status = "failed"
					};
				}

				if (classroom.SchoolId != schoolId)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You cannot access classrooms from a different school",
						Status = "failed"
					};
				}

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Classroom retrieved successfully",
					Status = "successful",
					Data = new
					{
						classroom.Id,
						classroom.Name,
						classroom.TeacherName,
						classroom.NoOfStudents,
						classroom.SchoolId,
						classroom.IsActive,
						classroom.CreationDate,
						classroom.ModifiedDate
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching classroom by ID - ClassroomId: {ClassroomId}", classroomId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching classroom",
					Status = "failed"
				};
			}
		}
		//_queryrepositorySubject,_classroomSubjectQueryRespository;_studentClassQueryRespository
		/// <summary>
		/// this endpoint fetches subjects for a class with Id
		/// </summary>
		/// <param name="classroomId"></param>
		/// <param name="userClaims"></param>
		/// <returns></returns>
		#region
		public async Task<BaseResponse> GetSubjectsByClassroom(Guid classroomId, AuthenticatedUserClaims userClaims)
		{
			try
			{
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid SchoolId format in token",
						Status = "failed"
					};
				}

				// Validate classroom exists and belongs to school
				var classroom = await _studentClassQueryRespository.Get(classroomId);
				if (classroom is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Classroom not found",
						Status = "failed"
					};
				}

				if (classroom.SchoolId != schoolId)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You cannot access classrooms from a different school",
						Status = "failed"
					};
				}

				// Query 1: Get classroom-subject mappings
				var classroomSubjectsQuery = $@"
					SELECT * FROM ClassroomSubject 
					WHERE ClassroomId = '{classroomId}'
					AND SchoolId = '{schoolId}'
					AND IsActive = 1";

				var classroomSubjects = await _classroomSubjectQueryRespository.GetByQuery(classroomSubjectsQuery);
				var classroomSubjectsList = classroomSubjects.Where(cs => cs != null).ToList();

				if (!classroomSubjectsList.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "No subjects found for this classroom",
						Status = "successful",
						Data = new
						{
							ClassroomId = classroomId,
							ClassroomName = classroom.Name,
							TotalCount = 0,
							MajorSubjectsCount = 0,
							MinorSubjectsCount = 0,
							MajorSubjects = Array.Empty<object>(),
							MinorSubjects = Array.Empty<object>()
						}
					};
				}

				var subjectIds = classroomSubjectsList.Select(cs => $"'{cs!.SubjectId}'");

				var subjectsQuery = $@"
					SELECT * FROM Subjects 
					WHERE Id IN ({string.Join(",", subjectIds)})
					AND SchoolId = '{schoolId}'
					AND IsActive = 1";

				var subjects = await _queryrepositorySubject.GetByQuery(subjectsQuery);
				var subjectsList = subjects.Where(s => s != null).ToList();

				var majorSubjects = subjectsList
					.Where(s => s!.Category == SubjectCategory.Major)
					.Select(s => new
					{
						s!.Id,
						s.Subject,
						Category = s.Category.ToString(),
						ClassCategory = s.ClassCategory.ToString(),
						s.IsActive,
						s.CreationDate,
						s.ModifiedDate
					}).ToList();

				var minorSubjects = subjectsList
					.Where(s => s!.Category == SubjectCategory.Minor)
					.Select(s => new
					{
						s!.Id,
						s.Subject,
						Category = s.Category.ToString(),
						ClassCategory = s.ClassCategory.ToString(),
						s.IsActive,
						s.CreationDate,
						s.ModifiedDate
					}).ToList();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Subjects retrieved successfully",
					Status = "successful",
					Data = new
					{
						ClassroomId = classroomId,
						ClassroomName = classroom.Name,
						TotalCount = subjectsList.Count,
						MajorSubjectsCount = majorSubjects.Count,
						MinorSubjectsCount = minorSubjects.Count,
						MajorSubjects = majorSubjects,
						MinorSubjects = minorSubjects
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching subjects for classroom - ClassroomId: {ClassroomId}", classroomId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching subjects",
					Status = "failed"
				};
			}
		}
		#endregion


		/// <summary>
		/// this endpoint remove or reactivate a teacher from a classroom
		/// </summary>
		/// <param name="updateClassroomTeachersViewModel"></param>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		public async Task<BaseResponse> UpdateClassroomTeachers(UpdateClassroomTeachersViewModel updateClassroomTeachersViewModel,AuthenticatedUserClaims userInfo)
		{
			#region
			using (LogContext.PushProperty("RequestedBy", userInfo.UserId))
			//using (LogContext.PushProperty("TenantId", userInfo.TenantIdentifier))
			{
				try
				{
					// Validation 1: Check if model is null
					if (updateClassroomTeachersViewModel is null)
					{
						_logger.Warning("Update classroom teachers request with null data");

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Update data cannot be empty",
							Status = "failed"
						};
					}

					// Validation 2: Check if ClassroomId is valid
					if (updateClassroomTeachersViewModel.ClassroomId == Guid.Empty)
					{
						_logger.Warning("Empty ClassroomId in update request");

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "ClassroomId cannot be empty",
							Status = "failed"
						};
					}

					// Validation 3: Check if actions list is empty
					if (!updateClassroomTeachersViewModel.TeacherActions.Any())
					{
						_logger.Warning("Update classroom teachers request with empty actions list");

						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "At least one teacher action is required",
							Status = "failed"
						};
					}

					// Validation 4: Parse SchoolId
					if (!Guid.TryParse(userInfo.SchoolId, out var schoolId))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid SchoolId format in token",
							Status = "failed"
						};
					}

					// Validation 5: Parse UserId
					if (!Guid.TryParse(userInfo.UserId, out var modifiedBy))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Invalid UserId format in token",
							Status = "failed"
						};
					}

					_logger.Information(
						"Updating classroom teachers - ClassroomId: {ClassroomId}, ActionCount: {ActionCount}",
						updateClassroomTeachersViewModel.ClassroomId,
						updateClassroomTeachersViewModel.TeacherActions.Count);

					// Validation 6: Check if classroom exists
					var classroom = await _studentClassQueryRespository.Get(updateClassroomTeachersViewModel.ClassroomId);

					if (classroom == null)
					{
						_logger.Warning("Classroom not found - ClassroomId: {ClassroomId}",
							updateClassroomTeachersViewModel.ClassroomId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = "Classroom not found",
							Status = "failed"
						};
					}

					// Validation 7: Check if classroom belongs to same school
					if (classroom.SchoolId != schoolId)
					{
						_logger.Warning(
							"Classroom belongs to different school - ClassroomId: {ClassroomId}",
							updateClassroomTeachersViewModel.ClassroomId);

						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "Classroom belongs to a different school",
							Status = "failed"
						};
					}

					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
					var results = new List<string>();
					var assignmentsToAdd = new List<Dictionary<string, object>>();
					var assignmentsToUpdate = new List<(Guid AssignmentId, Dictionary<string, object> UpdateData)>();

					foreach (var action in updateClassroomTeachersViewModel.TeacherActions)
					{
						// Validation 8: Check if TeacherId is valid
						if (action.TeacherId == Guid.Empty)
						{
							_logger.Warning("Empty TeacherId in action");

							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = "TeacherId cannot be empty",
								Status = "failed"
							};
						}

						// Validation 9: Check if teacher exists
						var teacher = await _queryrepositoryUser.Get(action.TeacherId);

						if (teacher == null)
						{
							_logger.Warning("Teacher not found - TeacherId: {TeacherId}", action.TeacherId);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.NotFound,
								ResponseMessage = $"Teacher with ID {action.TeacherId} not found",
								Status = "failed"
							};
						}

						// Validation 10: Check if teacher belongs to same school
						if (teacher.SchoolId != schoolId)
						{
							_logger.Warning(
								"Teacher belongs to different school - TeacherId: {TeacherId}",
								action.TeacherId);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.Forbidden,
								ResponseMessage = $"Teacher {teacher.FirstName} {teacher.LastName} belongs to a different school",
								Status = "failed"
							};
						}

						// Validation 11: Check if teacher is active
						if (!teacher.IsActive)
						{
							_logger.Warning("Teacher is not active - TeacherId: {TeacherId}", action.TeacherId);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = $"Teacher {teacher.FirstName} {teacher.LastName} is not active",
								Status = "failed"
							};
						}

						// Validation 12: Check if teacher role is valid
						var teacherRole = (UserRole)teacher.RoleId;
						if (teacherRole != UserRole.SubjectTeacher &&
							teacherRole != UserRole.HeadTeacher &&
							teacherRole != UserRole.Administrator &&
							teacherRole != UserRole.SuperAdministrator)
						{
							_logger.Warning(
								"User is not a teacher - UserId: {UserId}, Role: {Role}",
								action.TeacherId,
								teacherRole);

							return new BaseResponse
							{
								ResponseCode = ResponseCode.BadRequest,
								ResponseMessage = $"User {teacher.FirstName} {teacher.LastName} is not a teacher",
								Status = "failed"
							};
						}

						// Check if assignment already exists (active or inactive)
						var existingAssignment = await GetClassroomTeacherAssignment(
							updateClassroomTeachersViewModel.ClassroomId,
							action.TeacherId);

						// Process based on action type
						switch (action.Action)
						{
							case TeacherActionType.Add:
								if (existingAssignment != null && existingAssignment.IsActive)
								{
									_logger.Warning(
										"Teacher already assigned (active) - TeacherId: {TeacherId}, ClassroomId: {ClassroomId}",
										action.TeacherId,
										updateClassroomTeachersViewModel.ClassroomId);

									return new BaseResponse
									{
										ResponseCode = ResponseCode.Conflict,
										ResponseMessage = $"Teacher {teacher.FirstName} {teacher.LastName} is already assigned to this classroom",
										Status = "failed"
									};
								}

								if (existingAssignment != null && !existingAssignment.IsActive)
								{
									// Reactivate instead of creating new
									var reactivateDict = new Dictionary<string, object>
									{
										{ "IsActive", true },
										{ "IsPrimary", action.IsPrimary },
										{ "ModifiedDate", now }
									};

									assignmentsToUpdate.Add((existingAssignment.Id, reactivateDict));

									results.Add($"Reactivated: {teacher.FirstName} {teacher.LastName}" + (action.IsPrimary ? " (Primary)" : ""));

									_logger.Debug(
										"Reactivating existing assignment - AssignmentId: {AssignmentId}",
										existingAssignment.Id);
								}
								else
								{
									// Create new assignment
									var newAssignmentDict = new Dictionary<string, object>
									{
										{ "Id", Guid.NewGuid() },
										{ "ClassroomId", updateClassroomTeachersViewModel.ClassroomId },
										{ "TeacherId", action.TeacherId },
										{ "IsPrimary", action.IsPrimary },
										{ "CreationDate", now },
										{ "ModifiedDate", now },
										{ "CreatedBy", modifiedBy },
										{ "SchoolId", schoolId },
										{ "IsActive", true }
									};

									assignmentsToAdd.Add(newAssignmentDict);

									results.Add($"Added: {teacher.FirstName} {teacher.LastName}" + (action.IsPrimary ? " (Primary)" : ""));

									_logger.Debug(
										"Adding new assignment - TeacherId: {TeacherId}, IsPrimary: {IsPrimary}",
										action.TeacherId,
										action.IsPrimary);
								}
								break;

							case TeacherActionType.Remove:
								if (existingAssignment == null)
								{
									_logger.Warning(
										"Cannot remove - No assignment found - TeacherId: {TeacherId}",
										action.TeacherId);

									return new BaseResponse
									{
										ResponseCode = ResponseCode.NotFound,
										ResponseMessage = $"Teacher {teacher.FirstName} {teacher.LastName} is not assigned to this classroom",
										Status = "failed"
									};
								}

								if (!existingAssignment.IsActive)
								{
									_logger.Warning(
										"Assignment already inactive - AssignmentId: {AssignmentId}",
										existingAssignment.Id);

									return new BaseResponse
									{
										ResponseCode = ResponseCode.BadRequest,
										ResponseMessage = $"Teacher {teacher.FirstName} {teacher.LastName} is already removed from this classroom",
										Status = "failed"
									};
								}

								// Deactivate assignment
								var deactivateDict = new Dictionary<string, object>
								{
									{ "IsActive", false },
									{ "ModifiedDate", now }
								};

								assignmentsToUpdate.Add((existingAssignment.Id, deactivateDict));

								results.Add($"Removed: {teacher.FirstName} {teacher.LastName}");

								_logger.Debug(
									"Deactivating assignment - AssignmentId: {AssignmentId}",
									existingAssignment.Id);
								break;

							case TeacherActionType.Reactivate:
								if (existingAssignment == null)
								{
									_logger.Warning(
										"Cannot reactivate - No assignment found - TeacherId: {TeacherId}",
										action.TeacherId);

									return new BaseResponse
									{
										ResponseCode = ResponseCode.NotFound,
										ResponseMessage = $"No previous assignment found for teacher {teacher.FirstName} {teacher.LastName}",
										Status = "failed"
									};
								}

								if (existingAssignment.IsActive)
								{
									_logger.Warning(
										"Assignment already active - AssignmentId: {AssignmentId}",
										existingAssignment.Id);

									return new BaseResponse
									{
										ResponseCode = ResponseCode.BadRequest,
										ResponseMessage = $"Teacher {teacher.FirstName} {teacher.LastName} is already active in this classroom",
										Status = "failed"
									};
								}

								// Reactivate assignment
								var activateDict = new Dictionary<string, object>
								{
									{ "IsActive", true },
									{ "IsPrimary", action.IsPrimary },
									{ "ModifiedDate", now }
								};

								assignmentsToUpdate.Add((existingAssignment.Id, activateDict));

								results.Add($"Reactivated: {teacher.FirstName} {teacher.LastName}" + (action.IsPrimary ? " (Primary)" : ""));

								_logger.Debug(
									"Reactivating assignment - AssignmentId: {AssignmentId}",
									existingAssignment.Id);
								break;

							default:
								return new BaseResponse
								{
									ResponseCode = ResponseCode.BadRequest,
									ResponseMessage = "Invalid action type",
									Status = "failed"
								};
						}
					}

					// Execute all changes in a transaction
					using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");

					// Add new assignments
					if (assignmentsToAdd.Any())
					{
						await _classroomTeacherCommandRepository.CreateBatchAsync(
							scope.Transaction,
							scope.Connection,
							assignmentsToAdd);
					}

					// Update existing assignments
					foreach (var (assignmentId, updateData) in assignmentsToUpdate)
					{
						var whereClause = new KeyValuePair<string, object>("Id", assignmentId);
						await _classroomTeacherCommandRepository.UpdateTableColumnById(updateData, whereClause);
					}

					await scope.CommitAsync();

					_logger.Information(
						"Successfully updated classroom teachers - ClassroomId: {ClassroomId}, Added: {AddCount}, Updated: {UpdateCount}",
						updateClassroomTeachersViewModel.ClassroomId,
						assignmentsToAdd.Count,
						assignmentsToUpdate.Count);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = $"Classroom teachers updated successfully for '{classroom.Name}'",
						Status = "successful",
						Data = new
						{
							ClassroomId = updateClassroomTeachersViewModel.ClassroomId,
							ClassroomName = classroom.Name,
							Changes = results,
							TotalChanges = results.Count
						}
					};
				}
				catch (SqlException ex)
				{
					_logger.Error(
						ex,
						"SQL error updating classroom teachers - ClassroomId: {ClassroomId}",
						updateClassroomTeachersViewModel?.ClassroomId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "Database error occurred while updating classroom teachers",
						Status = "failed"
					};
				}
				catch (Exception ex)
				{
					_logger.Error(
						ex,
						"Unexpected error updating classroom teachers - ClassroomId: {ClassroomId}",
						updateClassroomTeachersViewModel?.ClassroomId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An unexpected error occurred while updating classroom teachers",
						Status = "failed"
					};
				}
				#endregion
			}
		}

		public async Task<BaseResponse> UpdateTeacherClassroom(Guid teacherId, UpdateTeacherClassroomViewModel model, AuthenticatedUserClaims claims)
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
					var hasPermission = await this.HasPermission(requesterId, schoolId, AdminPermission.ManageTeachers);

					if (!hasPermission)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Forbidden,
							ResponseMessage = "You do not have permission to update teacher classrooms",
							Status = "failed"
						};
				}
				else if (requesterRole != UserRole.SuperAdministrator && requesterRole != UserRole.HeadTeacher)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Forbidden,
						ResponseMessage = "You are not authorized to update teacher classrooms",
						Status = "failed"
					};
				}

				// ── Verify teacher exists and belongs to school ───────────────
				var teacher = await _queryrepositoryUser.Get(teacherId);
				if (teacher == null || teacher.SchoolId != schoolId)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Teacher not found",
						Status = "failed"
					};

				if (teacher.RoleId != (int)UserRole.SubjectTeacher && teacher.RoleId != (int)UserRole.HeadTeacher && teacher.RoleId != (int)UserRole.ClassTeacher)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "User is not a teacher",
						Status = "failed"
					};

				if (!teacher.IsActive)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = $"Teacher {teacher.FirstName} {teacher.LastName} is not active",
						Status = "failed"
					};

				// ── Verify all incoming classrooms exist and belong to school ─
				foreach (var classroomId in model.ClassroomIds)
				{
					var classroom = await _studentClassQueryRespository.Get(classroomId);
					if (classroom == null || classroom.SchoolId != schoolId)
						return new BaseResponse
						{
							ResponseCode = ResponseCode.NotFound,
							ResponseMessage = $"Classroom {classroomId} not found",
							Status = "failed"
						};
				}

				// ── Fetch current active classroom assignments ─────────────────
				var currentQuery = $@"
					SELECT ClassroomId FROM TeacherClassroom
					WHERE  TeacherId = '{teacherId}'
					AND    SchoolId  = '{schoolId}'
					AND    IsActive  = 1";

				var currentRows = await _classroomTeacherQueryRespository.QueryAsync<TeacherClassroomIdRow>(currentQuery, new Dictionary<string, object>());

				var currentClassroomIds = currentRows.Select(r => r.ClassroomId).ToHashSet();
				var incomingClassroomIds = model.ClassroomIds.ToHashSet();

				// ── Diff ───────────────────────────────────────────────────────
				var toAdd = incomingClassroomIds.Except(currentClassroomIds).ToList();
				var toRemove = currentClassroomIds.Except(incomingClassroomIds).ToList();

				if (!toAdd.Any() && !toRemove.Any())
					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "No changes detected — classrooms are already up to date",
						Status = "successful"
					};

				var nowStr = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				try
				{
					// ── Soft delete removed classrooms ────────────────────────
					foreach (var classroomId in toRemove)
					{
						var softDelete = $@"
							UPDATE ClassroomTeacher   
							SET    IsActive     = 0,
								   ModifiedDate = '{nowStr}'
							WHERE  TeacherId   = '{teacherId}'
							AND    ClassroomId = '{classroomId}'
							AND    SchoolId    = '{schoolId}'
							AND    IsActive    = 1";

						await scope.Connection.ExecuteAsync(softDelete, transaction: scope.Transaction);
					}

					// ── Insert new classrooms ─────────────────────────────────
					foreach (var classroomId in toAdd)
					{
						var insertDict = new Dictionary<string, object>
						{
							{ "Id",           Guid.NewGuid() },
							{ "TeacherId",    teacherId       },
							{ "ClassroomId",  classroomId     },
							{ "SchoolId",     schoolId        },
							{ "CreatedBy",    requesterId     },
							{ "CreationDate", nowStr          },
							{ "ModifiedDate", nowStr          },
							{ "IsActive",     true            }
						};

						await _classroomTeacherCommandRepository.Create(scope.Transaction, scope.Connection, insertDict);
					}

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex,"Rolling back teacher classroom update - TeacherId: {TeacherId}", teacherId);
					try { await scope.RollbackAsync(); } catch { }
					throw;
				}

				_logger.Information(
					"Teacher classrooms updated - TeacherId: {TeacherId}, " +
					"Added: {Added}, Removed: {Removed}, UpdatedBy: {RequesterId}",
					teacherId, toAdd.Count, toRemove.Count, requesterId);

				// ── Fetch updated assignments to return ───────────────────────
				var updatedQuery = $@"
					SELECT
						tc.ClassroomId,
						c.Name AS ClassName,
						c.IsActive AS ClassroomIsActive
					FROM   TeacherClassroom tc
					JOIN   Classroom        c ON c.Id = tc.ClassroomId
					WHERE  tc.TeacherId = '{teacherId}'
					AND    tc.SchoolId  = '{schoolId}'
					AND    tc.IsActive  = 1
					ORDER  BY c.Name";

				var updated = await _classroomTeacherQueryRespository.QueryAsync<ClassroomRow>(updatedQuery, new Dictionary<string, object>());

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Teacher classrooms updated successfully",
					Status = "successful",
					Data = new
					{
						TeacherId = teacherId,
						TeacherName = $"{teacher.FirstName} {teacher.LastName}",
						Added = toAdd.Count,
						Removed = toRemove.Count,
						Classrooms = updated.ToList()
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,"Error updating teacher classrooms - TeacherId: {TeacherId}",teacherId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while updating teacher classrooms",
					Status = "failed"
				};
			}
		}

		public async Task<CreateTopicResponse> CreateTopic(CreateTopicViewModel model, AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			{
				try
				{
					_logger.Information(
						"Creating topics - SubjectId: {SubjectId}, ClassroomId: {ClassroomId}, TopicCount: {Count}",
						model.SubjectId, model.ClassroomId, model.Topics.Count);

					if (!Guid.TryParse(userClaims.UserId, out var userId))
						return StringSanitizer.Fail<CreateTopicResponse>("Invalid user identification");

					if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
						return StringSanitizer.Fail<CreateTopicResponse>("Invalid school identification");

					if (!Enum.TryParse<UserRole>(userClaims.Role, ignoreCase: true, out var userRole))
						return StringSanitizer.Fail<CreateTopicResponse>("Invalid role in token");

					if (model.SubjectId == Guid.Empty)
						return StringSanitizer.Fail<CreateTopicResponse>("Subject is required");

					if (model.ClassroomId == Guid.Empty)
						return StringSanitizer.Fail<CreateTopicResponse>("Classroom is required");

					if (!model.Topics.Any())
						return StringSanitizer.Fail<CreateTopicResponse>("At least one topic is required");

					// Validate all topics
					foreach (var topic in model.Topics)
					{
						if (string.IsNullOrWhiteSpace(topic.Name))
							return StringSanitizer.Fail<CreateTopicResponse>("Topic name cannot be empty");

						if (topic.Name.Length > 200)
							return StringSanitizer.Fail<CreateTopicResponse>(
								$"Topic name '{topic.Name}' cannot exceed 200 characters");

						if (!topic.SubTopics.Any())
							return StringSanitizer.Fail<CreateTopicResponse>(
								$"Topic '{topic.Name}' must have at least one subtopic");

						if (topic.SubTopics.Any(string.IsNullOrWhiteSpace))
							return StringSanitizer.Fail<CreateTopicResponse>(
								$"Topic '{topic.Name}' has an empty subtopic name");

						if (topic.SubTopics.Count != topic.SubTopics
								.Distinct(StringComparer.OrdinalIgnoreCase).Count())
							return StringSanitizer.Fail<CreateTopicResponse>(
								$"Topic '{topic.Name}' has duplicate subtopic names");
					}

					// Duplicate topic names across the submitted batch
					var topicNames = model.Topics.Select(t => t.Name.Trim()).ToList();
					if (topicNames.Count != topicNames
							.Distinct(StringComparer.OrdinalIgnoreCase).Count())
						return StringSanitizer.Fail<CreateTopicResponse>("Duplicate topic names are not allowed in the same request");

					// Verify subject belongs to this school
					var subject = await _queryrepositorySubject.Get(
						model.SubjectId, DatabaseTarget.Core);
					if (subject == null || !subject.IsActive || subject.SchoolId != schoolId)
						return StringSanitizer.Fail<CreateTopicResponse>("Subject not found");

					// Verify classroom belongs to this school
					var classroomQuery = $@"
						SELECT TOP 1 Id, Name FROM Classroom
						WHERE Id       = '{model.ClassroomId}'
						AND   SchoolId = '{schoolId}'
						AND   IsActive = 1";

					var classroom = await _studentClassQueryRespository.Get(classroomQuery);
					if (classroom is null)
						return StringSanitizer.Fail<CreateTopicResponse>("Classroom not found");

					// Check for existing topics with same names in same subject + classroom
					var sanitizedNames = string.Join(
						",", topicNames.Select(n => $"'{StringSanitizer.Sanitize(n)}'"));

					var duplicateQuery = $@"
						SELECT Name FROM Topic
						WHERE  SubjectId   = '{model.SubjectId}'
						AND    ClassroomId = '{model.ClassroomId}'
						AND    SchoolId    = '{schoolId}'
						AND    IsDeleted   = 0
						AND    Name        IN ({sanitizedNames})";

					var existingTopics = await _topicQueryRepository
						.GetByQuery(duplicateQuery, DatabaseTarget.Core);

					if (existingTopics?.Any() == true)
					{
						var duplicates = string.Join(", ", existingTopics.Select(t => t.Name));
						return StringSanitizer.Fail<CreateTopicResponse>(
							$"Topics already exist for this subject and classroom: {duplicates}");
					}

					// Fetch teacher for line manager
					var teacher = await _queryrepositoryUser.Get(userId);
					if (teacher is null)
						return StringSanitizer.Fail<CreateTopicResponse>("Teacher not found");

					var requiresApproval = userRole == UserRole.SubjectTeacher || userRole == UserRole.ClassTeacher;

					if (requiresApproval && !teacher.LineManagerId.HasValue)
						return StringSanitizer.Fail<CreateTopicResponse>("No line manager assigned. Cannot submit for approval.");

					var now = DateTime.UtcNow;
					var nowStr = now.ToString("yyyy-MM-dd HH:mm:ss");
					var isActive = !requiresApproval;

					// Build all topic and subtopic dicts
					var allTopicDicts = new List<Dictionary<string, object>>();
					var allSubTopicDicts = new List<Dictionary<string, object>>();
					var topicIds = new List<Guid>();

					foreach (var topicInput in model.Topics)
					{
						var topicId = Guid.NewGuid();
						topicIds.Add(topicId);

						allTopicDicts.Add(new Dictionary<string, object>
						{
							{ "Id",          topicId },
							{ "SubjectId",   model.SubjectId },
							{ "ClassroomId", model.ClassroomId },
							{ "SchoolId",    schoolId },
							{ "Name",        topicInput.Name.Trim() },
							{ "IsActive",    isActive },
							{ "IsDeleted",   false },
							{ "CreatedAt",   nowStr },
							{ "CreatedBy",   userId }
						});

						var subTopics = topicInput.SubTopics.Select(name =>
							new Dictionary<string, object>
							{
								{ "Id",          Guid.NewGuid() },
								{ "TopicId",     topicId },
								{ "SchoolId",    schoolId },
								{ "ClassroomId", model.ClassroomId },
								{ "Name",        name.Trim() },
								{ "IsActive",    isActive },
								{ "IsDeleted",   false },
								{ "CreatedAt",   nowStr },
								{ "CreatedBy",   userId }
							}).ToList();

						allSubTopicDicts.AddRange(subTopics);
					}

					var approvalId = Guid.NewGuid();

					using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
					try
					{
						// 1. Batch insert all topics
						await _topicCommandRepository.CreateBatchAsync(
							scope.Transaction, scope.Connection, allTopicDicts);

						// 2. Batch insert all subtopics
						await _subTopicCommandRepository.CreateBatchAsync(
							scope.Transaction, scope.Connection, allSubTopicDicts);

						// 3. One approval request covering all topics
						if (requiresApproval)
						{
							var expiryDays = int.Parse(
								_configuration["Approvals:ExpiryDays"] ?? "5");

							var payloadSummary = new ApprovalPayloadSummary
							{
								Title = $"{model.Topics.Count} topic(s) for {subject.Subject}",
								SubjectName = subject.Subject,
								ClassName = classroom.Name,
								Description = string.Join(", ", topicNames.Take(3)) +
											  (topicNames.Count > 3 ? "..." : ""),
								// Store all topicIds so RespondToApproval can activate them all
								EntityIds = topicIds
							};

							var approvalDict = new Dictionary<string, object>
							{
								{ "Id",              approvalId },
								{ "SchoolId",        schoolId },
								{ "RequestedBy",     userId },
								{ "ApproverId",      teacher.LineManagerId!.Value },
								{ "OperationType",   OperationType.CreateTopic },
								{ "EntityType",      "Topic" },
								{ "EntityId",        DBNull.Value }, 
								{ "Payload",         JsonSerializer.Serialize(payloadSummary) },
								{ "Status",          ApprovalStatus.Pending },
								{ "RejectionReason", DBNull.Value },
								{ "CreatedAt",       now },
								{ "RespondedAt",     DBNull.Value },
								{ "ExpiresAt",       now.AddDays(expiryDays) }
							};

							await _approvalRequestCommandRepository.Create(scope.Transaction, scope.Connection, approvalDict);
						}

						await scope.CommitAsync();
					}
					catch (Exception ex)
					{
						_logger.Error(ex, "Rolling back topic batch creation");
						try { await scope.RollbackAsync(); }
						catch (Exception rbEx)
						{
							_logger.Error(rbEx, "Rollback failed");
						}
						throw;
					}

					_logger.Information(
						"Topics created - TopicCount: {TopicCount}, SubTopicCount: {SubTopicCount}, " +
						"RequiresApproval: {RequiresApproval}",
						allTopicDicts.Count, allSubTopicDicts.Count, requiresApproval);

					return new CreateTopicResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = requiresApproval
							? $"{model.Topics.Count} topic(s) and subtopics submitted for approval"
							: $"{model.Topics.Count} topic(s) and subtopics created successfully",
						Status = "successful",
						TopicId = topicIds.FirstOrDefault()
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error creating topics");
					return StringSanitizer.Error<CreateTopicResponse>();
				}
			}
		}


		public async Task<CreateSubTopicResponse> CreateSubTopic(CreateSubTopicViewModel model,AuthenticatedUserClaims userClaims)
		{
			using (LogContext.PushProperty("RequestedBy", userClaims.UserId))
			{
				try
				{
					_logger.Information("Creating subtopic - Name: {Name}, TopicId: {TopicId}",model.Name, model.TopicId);

					if (!Guid.TryParse(userClaims.UserId, out var userId))
						return StringSanitizer.Fail<CreateSubTopicResponse>("Invalid user identification");

					if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
						return StringSanitizer.Fail<CreateSubTopicResponse>("Invalid school identification");

					if (string.IsNullOrWhiteSpace(model.Name))
						return StringSanitizer.Fail<CreateSubTopicResponse>("SubTopic name is required");

					if (model.Name.Length > 200)
						return StringSanitizer.Fail<CreateSubTopicResponse>("SubTopic name cannot exceed 200 characters");

					if (model.TopicId == Guid.Empty)
						return StringSanitizer.Fail<CreateSubTopicResponse>("Topic is required");

					// Verify topic exists and belongs to this school
					var topic = await _topicQueryRepository.Get(model.TopicId, DatabaseTarget.Core);

					if (topic == null || topic.IsDeleted || topic.SchoolId != schoolId)
						return StringSanitizer.Fail<CreateSubTopicResponse>("Topic not found");

					// Duplicate check within this topic
					var duplicateQuery = $@"
						SELECT TOP 1 Id FROM SubTopic
						WHERE TopicId   = '{model.TopicId}'
						AND   SchoolId  = '{schoolId}'
						AND   IsDeleted = 0
						AND   Name      = '{StringSanitizer.Sanitize(model.Name)}'";

					var existing = await _subTopicQueryRepository.GetByQuery(duplicateQuery, DatabaseTarget.Core);

					if (existing?.Any() == true)
						return StringSanitizer.Fail<CreateSubTopicResponse>("A subtopic with this name already exists in this topic");

					var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

					var subTopic = new SubTopic
					{
						Id = Guid.NewGuid(),
						TopicId = model.TopicId,
						SchoolId = schoolId,
						Name = model.Name.Trim(),
						IsActive = true,
						IsDeleted = false,
						CreatedAt = now,
						CreatedBy = userId
					};

					await _subTopicCommandRepository.Create(subTopic, DatabaseTarget.Core);

					_logger.Information("SubTopic created - SubTopicId: {SubTopicId}", subTopic.Id);

					return new CreateSubTopicResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "SubTopic created successfully",
						Status = "successful",
						SubTopicId = subTopic.Id
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error creating subtopic");
					return StringSanitizer.Error<CreateSubTopicResponse>();
				}
			}
		}


		/// <summary>
		/// Get all active subtopics for a topic
		/// </summary>
		public async Task<SubTopicListResponse> GetSubTopics(Guid topicId,AuthenticatedUserClaims userClaims)
		{
			try
			{
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					return new SubTopicListResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};

				var query = $@"
					SELECT Id, TopicId, Name, IsActive
					FROM SubTopic
					WHERE TopicId   = '{topicId}'
					AND   SchoolId  = '{schoolId}'
					AND   IsDeleted = 0
					AND   IsActive  = 1
					ORDER BY Name ASC";

				var results = await _subTopicQueryRepository.GetByQuery(query, DatabaseTarget.QuestionBank);

				var subTopics = results?.Select(st => new SubTopicDto
				{
					Id = st.Id,
					TopicId = st.TopicId,
					Name = st.Name,
					IsActive = st.IsActive
				}).ToList() ?? new List<SubTopicDto>();

				return new SubTopicListResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "SubTopics retrieved successfully",
					Status = "successful",
					SubTopics = subTopics
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error getting subtopics");
				return new SubTopicListResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving subtopics",
					Status = "failed"
				};
			}
		}

		public async Task<BaseResponse> GetSubjectCurriculum(Guid subjectId, Guid classroomId, AuthenticatedUserClaims userClaims)
		{
			try
			{
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				// Validate subject exists and belongs to school.
				var subject = await _queryrepositorySubject.Get(subjectId, DatabaseTarget.Core);
				if (subject is null || !subject.IsActive || subject.SchoolId != schoolId)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Subject not found",
						Status = "failed"
					};
				}

				// 1) Get all topics for this subject + classroom.
				var topicsQuery = $@"
					SELECT Id, Name, SubjectId, IsActive
					FROM   Topic
					WHERE  SubjectId   = '{subjectId}'
					  AND  SchoolId    = '{schoolId}'
					  AND  ClassroomId = '{classroomId}'
					  AND  IsDeleted   = 0
					  AND  IsActive    = 1
					ORDER BY Name ASC";

				var topics = await _topicQueryRepository.GetByQuery(topicsQuery, DatabaseTarget.Core);
				var topicList = topics?.Where(t => t != null).ToList() ?? new List<Topic>();

				// 2) Get all subtopics for all topics in one query (no query in loop).
				var subTopicList = new List<SubTopic>();

				if (topicList.Any())
				{
					var topicIds = topicList.Select(t => t.Id).Distinct().ToList();
					var topicIdsCsv = string.Join(",", topicIds.Select(id => $"'{id}'"));

					var subTopicsQuery = $@"
						SELECT Id, TopicId, Name, IsActive
						FROM   SubTopic
						WHERE  TopicId      IN ({topicIdsCsv})
						  AND  SchoolId     = '{schoolId}'
						  AND  ClassroomId  = '{classroomId}'
						  AND  IsDeleted    = 0
						  AND  IsActive     = 1
						ORDER BY Name ASC";

					var subTopics = await _subTopicQueryRepository.GetByQuery(subTopicsQuery, DatabaseTarget.Core);
					subTopicList = subTopics?.Where(st => st != null).ToList() ?? new List<SubTopic>();
				}

				// 3) Group subtopics by topic for fast in-memory composition.
				var subTopicsByTopicId = subTopicList.GroupBy(st => st.TopicId).ToDictionary(g => g.Key, g => g.ToList());

				var topicsWithSubTopics = topicList.Select(topic =>
				{
					var topicSubTopics = subTopicsByTopicId.TryGetValue(topic.Id, out var stList) ? stList : new List<SubTopic>();

					return new
					{
						topic.Id,
						topic.Name,
						topic.SubjectId,
						SubTopics = topicSubTopics.Select(st => new
						{
							st.Id,
							st.Name,
							st.TopicId,
							st.IsActive
						}).ToList()
					};
				}).ToList<object>();

				_logger.Information(
					"Subject curriculum fetched - SubjectId: {SubjectId}, TopicCount: {Count}, ClassroomId: {ClassroomId}",
					subjectId, topicsWithSubTopics.Count, classroomId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Subject curriculum retrieved successfully",
					Status = "successful",
					Data = new
					{
						SubjectId = subject.Id,
						SubjectName = subject.Subject,
						Category = subject.Category.ToString(),
						ClassCategory = subject.ClassCategory.ToString(),
						ClassroomId = classroomId,
						Topics = topicsWithSubTopics
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Error fetching subject curriculum - SubjectId: {SubjectId}",
					subjectId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving subject curriculum",
					Status = "failed"
				};
			}
		}
		/// <summary>
		/// ftch topics and subtopic with classroom and subject 
		/// </summary>
		/// <param name="subjectId"></param>
		/// <param name="classroomId"></param>
		/// <param name="claims"></param>
		/// <returns></returns>

		public async Task<BaseResponse> GetTopicsWithSubTopics(Guid subjectId, Guid classroomId, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims.SchoolId, out var schoolId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Unauthorized,
						ResponseMessage = "Invalid authentication",
						Status = "failed"
					};

				// Validate subject belongs to school
				var subject = await _queryrepositorySubject.Get(subjectId, DatabaseTarget.Core);
				if (subject == null || subject.SchoolId != schoolId)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Subject not found",
						Status = "failed"
					};

				// Single query — topics + subtopics joined
				var query = $@"
					SELECT
						t.Id           AS TopicId,
						t.Name         AS TopicName,
						t.IsActive     AS TopicIsActive,
						t.CreatedAt    AS TopicCreatedAt,

						st.Id          AS SubTopicId,
						st.Name        AS SubTopicName,
						st.IsActive    AS SubTopicIsActive,
						st.CreatedAt   AS SubTopicCreatedAt

					FROM   Topic    t
					LEFT JOIN SubTopic st ON st.TopicId  = t.Id
										 AND st.IsDeleted = 0
										 AND st.IsActive  = 1
					WHERE  t.SubjectId   = '{subjectId}'
					AND    t.ClassroomId = '{classroomId}'
					AND    t.SchoolId    = '{schoolId}'
					AND    t.IsDeleted   = 0
					AND    t.IsActive    = 1
					ORDER  BY t.Name ASC, st.Name ASC";

				var rows = await _topicQueryRepository.QueryAsync<TopicSubTopicRow>(query, new Dictionary<string, object>());

				// Group flat rows into nested structure
				var grouped = rows
					.GroupBy(r => new { r.TopicId, r.TopicName, r.TopicIsActive, r.TopicCreatedAt })
					.Select(g => new TopicWithSubTopicsDto
					{
						TopicId = g.Key.TopicId,
						TopicName = g.Key.TopicName,
						IsActive = g.Key.TopicIsActive,
						CreatedAt = g.Key.TopicCreatedAt,
						SubTopics = g
							.Where(r => r.SubTopicId != Guid.Empty)
							.Select(r => new SubTopicDto2
							{
								SubTopicId = r.SubTopicId,
								Name = r.SubTopicName,
								IsActive = r.SubTopicIsActive,
								CreatedAt = r.SubTopicCreatedAt
							}).ToList()
					}).ToList();

				_logger.Information("Topics fetched - SubjectId: {SubjectId}, ClassroomId: {ClassroomId}, " + "TopicCount: {TopicCount}",
					subjectId, classroomId, grouped.Count);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = grouped.Any()
						? $"{grouped.Count} topic(s) found"
						: "No topics found for this subject",
					Status = "successful",
					Data = new
					{
						SubjectId = subjectId,
						SubjectName = subject.Subject,
						ClassroomId = classroomId,
						TopicCount = grouped.Count,
						Topics = grouped
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Error fetching topics - SubjectId: {SubjectId}, ClassroomId: {ClassroomId}",
					subjectId, classroomId);
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred",
					Status = "failed"
				};
			}
		}


		public async Task<BaseResponse> GetClassroomCurriculum(Guid classroomId, AuthenticatedUserClaims userClaims)
		{
			try
			{
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				// 1) Get all subjects assigned to this classroom.
				var subjectsQuery = $@"
					SELECT s.Id, s.Subject, s.Category, s.ClassCategory, s.IsActive
					FROM   Subjects s
					JOIN   ClassroomSubject cs ON cs.SubjectId = s.Id
					WHERE  cs.ClassroomId = '{classroomId}'
					  AND  cs.SchoolId    = '{schoolId}'
					  AND  cs.IsActive    = 1
					  AND  s.IsActive     = 1
					ORDER BY s.Subject ASC";

				var subjects = await _queryrepositorySubject.GetByQuery(subjectsQuery);
				var subjectList = subjects?.Where(s => s != null).ToList() ?? new List<Subjects>();

				if (!subjectList.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "No subjects found for this classroom",
						Status = "successful",
						Data = new
						{
							ClassroomId = classroomId,
							Subjects = new List<object>()
						}
					};
				}

				// 2) Get all topics for all fetched subjects in one query.
				var subjectIds = subjectList.Select(s => s.Id).Distinct().ToList();
				var subjectIdsCsv = string.Join(",", subjectIds.Select(id => $"'{id}'"));

				var topicsQuery = $@"
					SELECT Id, Name, SubjectId, IsActive
					FROM   Topic
					WHERE  SubjectId IN ({subjectIdsCsv})
					  AND  SchoolId   = '{schoolId}'
					  AND  IsDeleted  = 0
					  AND  IsActive   = 1
					ORDER BY Name ASC";

				var topics = await _topicQueryRepository.GetByQuery(topicsQuery, DatabaseTarget.Core);
				var topicList = topics?.Where(t => t != null).ToList() ?? new List<Topic>();

				// 3) Get all subtopics for all fetched topics in one query.
				var subTopicList = new List<SubTopic>();

				if (topicList.Any())
				{
					var topicIds = topicList.Select(t => t.Id).Distinct().ToList();
					var topicIdsCsv = string.Join(",", topicIds.Select(id => $"'{id}'"));

					var subTopicsQuery = $@"
						SELECT Id, TopicId, Name, IsActive
						FROM   SubTopic
						WHERE  TopicId IN ({topicIdsCsv})
						  AND  SchoolId  = '{schoolId}'
						  AND  IsDeleted = 0
						  AND  IsActive  = 1
						ORDER BY Name ASC";

					var subTopics = await _subTopicQueryRepository.GetByQuery(subTopicsQuery, DatabaseTarget.Core);
					subTopicList = subTopics?.Where(st => st != null).ToList() ?? new List<SubTopic>();
				}

				// 4) Build lookup maps (no DB calls in loops).
				var topicsBySubjectId = topicList
					.GroupBy(t => t.SubjectId)
					.ToDictionary(g => g.Key, g => g.ToList());

				var subTopicsByTopicId = subTopicList
					.GroupBy(st => st.TopicId)
					.ToDictionary(g => g.Key, g => g.ToList());

				// 5) Compose response hierarchy in memory.
				var curriculum = subjectList.Select(subject =>
				{
					var subjectTopics = topicsBySubjectId.TryGetValue(subject.Id, out var tList)
						? tList
						: new List<Topic>();

					var topicsWithSubTopics = subjectTopics.Select(topic =>
					{
						var topicSubTopics = subTopicsByTopicId.TryGetValue(topic.Id, out var stList)
							? stList
							: new List<SubTopic>();

						return new
						{
							topic.Id,
							topic.Name,
							topic.SubjectId,
							SubTopics = topicSubTopics.Select(st => new
							{
								st.Id,
								st.Name,
								st.TopicId,
								st.IsActive
							}).ToList()
						};
					}).ToList();

					return new
					{
						Id = subject.Id,
						Name = subject.Subject,
						Category = (int)subject.Category,
						CategoryName = subject.Category.ToString(),
						ClassCategory = (int)subject.ClassCategory,
						IsActive = subject.IsActive,
						Topics = topicsWithSubTopics
					};
				}).ToList<object>();

				_logger.Information(
					"Curriculum fetched - ClassroomId: {ClassroomId}, SubjectCount: {Count}",
					classroomId, curriculum.Count);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Curriculum retrieved successfully",
					Status = "successful",
					Data = new
					{
						ClassroomId = classroomId,
						Subjects = curriculum
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error fetching classroom curriculum - ClassroomId: {ClassroomId}", classroomId);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while retrieving curriculum",
					Status = "failed"
				};
			}
		}

		public async Task<BaseResponse> AddSubTopicsToTopic(AddSubTopicsViewModel model, AuthenticatedUserClaims userClaims)
		{
			try
			{
				if (!Guid.TryParse(userClaims.UserId, out var userId))
					return StringSanitizer.Fail<BaseResponse>("Invalid user identification");

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					return StringSanitizer.Fail<BaseResponse>("Invalid school identification");

				if (!Enum.TryParse<UserRole>(userClaims.Role, ignoreCase: true, out var userRole))
					return StringSanitizer.Fail<BaseResponse>("Invalid role in token");

				if (model.TopicId == Guid.Empty)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Topic is required",
						Status = "failed"
					};

				if (!model.SubTopics.Any())
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "At least one subtopic is required",
						Status = "failed"
					};

				if (model.SubTopics.Any(string.IsNullOrWhiteSpace))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Subtopic names cannot be empty",
						Status = "failed"
					};

				if (model.SubTopics.Count != model.SubTopics.Distinct(StringComparer.OrdinalIgnoreCase).Count())
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Duplicate subtopic names are not allowed",
						Status = "failed"
					};

				// Verify topic exists and belongs to this school
				var topicQuery = $@"
					SELECT TOP 1 Id, Name, ClassroomId, SubjectId FROM Topic
					WHERE  Id        = '{model.TopicId}'
					AND    SchoolId  = '{schoolId}'
					AND    IsDeleted = 0
					AND    IsActive  = 1";

				var topic = await _topicQueryRepository.Get(topicQuery);
				if (topic is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Topic not found",
						Status = "failed"
					};

				// Check for duplicate names within this topic
				var sanitizedNames = string.Join(",",
					model.SubTopics.Select(n => $"'{StringSanitizer.Sanitize(n.Trim())}'"));

				var duplicateQuery = $@"
					SELECT Name FROM SubTopic
					WHERE  TopicId   = '{model.TopicId}'
					AND    SchoolId  = '{schoolId}'
					AND    IsDeleted = 0
					AND    Name      IN ({sanitizedNames})";

				var existing = await _subTopicQueryRepository.GetByQuery(
					duplicateQuery, DatabaseTarget.Core);

				if (existing?.Any() == true)
				{
					var duplicates = string.Join(", ", existing.Select(s => s.Name));
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = $"Subtopics already exist in this topic: {duplicates}",
						Status = "failed"
					};
				}

				// Fetch teacher for line manager
				var teacher = await _queryrepositoryUser.Get(userId);
				if (teacher is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Teacher not found",
						Status = "failed"
					};

				var requiresApproval = userRole == UserRole.SubjectTeacher ||
									   userRole == UserRole.ClassTeacher;

				if (requiresApproval && !teacher.LineManagerId.HasValue)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "No line manager assigned. Cannot submit for approval.",
						Status = "failed"
					};

				var now = DateTime.UtcNow;
				var nowStr = now.ToString("yyyy-MM-dd HH:mm:ss");
				var isActive = !requiresApproval;

				var subTopicDicts = model.SubTopics.Select(name =>
					new Dictionary<string, object>
					{
						{ "Id",          Guid.NewGuid() },
						{ "TopicId",     model.TopicId },
						{ "SchoolId",    schoolId },
						{ "ClassroomId", topic.ClassroomId },
						{ "Name",        name.Trim() },
						{ "IsActive",    isActive },
						{ "IsDeleted",   false },
						{ "CreatedAt",   nowStr },
						{ "CreatedBy",   userId }
					}).ToList();

				var approvalId = Guid.NewGuid();

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				try
				{
					// 1. Batch insert subtopics
					await _subTopicCommandRepository.CreateBatchAsync(scope.Transaction, scope.Connection, subTopicDicts);

					// 2. Approval request only for SubjectTeacher and ClassTeacher
					if (requiresApproval)
					{
						var expiryDays = int.Parse(_configuration["Approvals:ExpiryDays"] ?? "5");

						var payloadSummary = new ApprovalPayloadSummary
						{
							Title = $"{model.SubTopics.Count} new subtopic(s) for {topic.Name}",
							SubjectName = string.Empty,  // resolved from topic if needed
							Description = string.Join(", ", model.SubTopics.Take(3)) +
										  (model.SubTopics.Count > 3 ? "..." : "")
						};

						var approvalDict = new Dictionary<string, object>
						{
							{ "Id",              approvalId },
							{ "SchoolId",        schoolId },
							{ "RequestedBy",     userId },
							{ "ApproverId",      teacher.LineManagerId!.Value },
							{ "OperationType",   OperationType.AddSubTopics },
							{ "EntityType",      "Topic" },
							{ "EntityId",        model.TopicId },
							{ "Payload",         JsonSerializer.Serialize(payloadSummary) },
							{ "Status",          ApprovalStatus.Pending },
							{ "RejectionReason", DBNull.Value },
							{ "CreatedAt",       now },
							{ "RespondedAt",     DBNull.Value },
							{ "ExpiresAt",       now.AddDays(expiryDays) }
						};

						await _approvalRequestCommandRepository.Create(
							scope.Transaction, scope.Connection, approvalDict);
					}

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Rolling back subtopic addition - TopicId: {TopicId}", model.TopicId);
					try { await scope.RollbackAsync(); }
					catch (Exception rbEx)
					{
						_logger.Error(rbEx, "Rollback failed - TopicId: {TopicId}", model.TopicId);
					}
					throw;
				}

				_logger.Information(
					"Subtopics added - TopicId: {TopicId}, Count: {Count}, RequiresApproval: {RequiresApproval}",
					model.TopicId, model.SubTopics.Count, requiresApproval);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = requiresApproval
						? $"{model.SubTopics.Count} subtopic(s) submitted for approval"
						: $"{model.SubTopics.Count} subtopic(s) added successfully",
					Status = "successful",
					Data = new
					{
						TopicId = model.TopicId,
						TopicName = topic.Name,
						AddedCount = model.SubTopics.Count,
						RequiresApproval = requiresApproval
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error adding subtopics - TopicId: {TopicId}", model.TopicId);
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred",
					Status = "failed"
				};
			}
		}


		public async Task<BaseResponse> CreateTopicsWithSubTopics(CreateTopicsWithSubTopicsViewModel model, AuthenticatedUserClaims userClaims)
		{
			try
			{
				if (!Guid.TryParse(userClaims.UserId, out var userId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid user identification",
						Status = "failed"
					};

				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};

				if (!Enum.TryParse<UserRole>(userClaims.Role, ignoreCase: true, out var userRole))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid role in token",
						Status = "failed"
					};

				if (!Guid.TryParse(model.SubjectId, out var subjectId))
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid subject identification",
						Status = "failed"
					};

				if (!model.Topics.Any())
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "At least one topic is required",
						Status = "failed"
					};

				foreach (var topic in model.Topics)
				{
					if (string.IsNullOrWhiteSpace(topic.Name))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Topic name cannot be empty",
							Status = "failed"
						};

					if (!topic.SubTopics.Any())
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Topic '{topic.Name}' must have at least one subtopic",
							Status = "failed"
						};

					if (topic.SubTopics.Any(string.IsNullOrWhiteSpace))
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Topic '{topic.Name}' has empty subtopic names",
							Status = "failed"
						};

					// Check duplicate subtopic names within same topic
					if (topic.SubTopics.Count != topic.SubTopics.Distinct(StringComparer.OrdinalIgnoreCase).Count())
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = $"Topic '{topic.Name}' has duplicate subtopic names",
							Status = "failed"
						};
				}

				// ── Check duplicate topic names within request ─────────────────
				var topicNames = model.Topics.Select(t => t.Name.Trim()).ToList();
				if (topicNames.Count != topicNames.Distinct(StringComparer.OrdinalIgnoreCase).Count())
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Duplicate topic names found in request",
						Status = "failed"
					};

				// ── Verify subject exists and belongs to school ───────────────
				var subject = await _queryrepositorySubject.Get(subjectId);
				if (subject == null || subject.SchoolId != schoolId)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Subject not found",
						Status = "failed"
					};

				// ── Check for existing topic names in DB ──────────────────────
				var sanitizedTopicNames = string.Join(",",topicNames.Select(n => $"'{StringSanitizer.Sanitize(n)}'"));

				var existingTopicsQuery = $@"
					SELECT Name FROM Topic
					WHERE  SubjectId = '{subjectId}'
					AND    SchoolId  = '{schoolId}'
					AND    IsDeleted = 0
					AND    Name      IN ({sanitizedTopicNames})";

				var existingTopics = await _topicQueryRepository.GetByQuery(existingTopicsQuery, DatabaseTarget.Core);

				if (existingTopics?.Any() == true)
				{
					var duplicates = string.Join(", ", existingTopics.Select(t => t.Name));
					return new BaseResponse
					{
						ResponseCode = ResponseCode.Conflict,
						ResponseMessage = $"Topics already exist for this subject: {duplicates}",
						Status = "failed"
					};
				}

				// ── Fetch teacher for approval check ──────────────────────────
				var teacher = await _queryrepositoryUser.Get(userId);
				if (teacher is null)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Teacher not found",
						Status = "failed"
					};

				var requiresApproval = userRole == UserRole.SubjectTeacher || userRole == UserRole.ClassTeacher;

				if (requiresApproval && !teacher.LineManagerId.HasValue)
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "No line manager assigned. Cannot submit for approval.",
						Status = "failed"
					};

				var now = DateTime.UtcNow;
				var nowStr = now.ToString("yyyy-MM-dd HH:mm:ss");
				var isActive = !requiresApproval;

				// ── Build topic and subtopic dicts ────────────────────────────
				var topicDicts = new List<Dictionary<string, object>>();
				var subTopicDicts = new List<Dictionary<string, object>>();
				var topicResults = new List<object>();

				foreach (var topic in model.Topics)
				{
					var topicId = Guid.NewGuid();

					topicDicts.Add(new Dictionary<string, object>
					{
						{ "Id",        topicId          },
						{ "SubjectId", subjectId         },
						{ "SchoolId",  schoolId          },
						{ "Name",      topic.Name.Trim() },
						{ "IsActive",  isActive          },
						{ "IsDeleted", false             },
						{ "CreatedAt", nowStr            },
						{ "CreatedBy", userId            }
					});

					foreach (var subTopic in topic.SubTopics)
					{
						subTopicDicts.Add(new Dictionary<string, object>
						{
							{ "Id",        Guid.NewGuid()    },
							{ "TopicId",   topicId           },
							//{ "SubjectId", subjectId          },
							{ "SchoolId",  schoolId           },
							{ "Name",      subTopic.Trim()   },
							{ "IsActive",  isActive          },
							{ "IsDeleted", false             },
							{ "CreatedAt", nowStr            },
							{ "CreatedBy", userId            }
						});
					}

					topicResults.Add(new
					{
						TopicId = topicId,
						TopicName = topic.Name.Trim(),
						SubTopicCount = topic.SubTopics.Count,
						SubTopics = topic.SubTopics.Select(s => s.Trim()).ToList()
					});
				}

				var approvalId = Guid.NewGuid();

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				try
				{
					// ── Batch insert topics ───────────────────────────────────
					await _topicCommandRepository.CreateBatchAsync(
						scope.Transaction, scope.Connection, topicDicts);

					// ── Batch insert subtopics ────────────────────────────────
					await _subTopicCommandRepository.CreateBatchAsync(
						scope.Transaction, scope.Connection, subTopicDicts);

					// ── Approval request for teachers ─────────────────────────
					if (requiresApproval)
					{
						var expiryDays = int.Parse(
							_configuration["Approvals:ExpiryDays"] ?? "5");

						var payloadSummary = new ApprovalPayloadSummary
						{
							Title = $"{model.Topics.Count} topic(s) with subtopics for {subject.Subject}",
							SubjectName = subject.Subject,
							Description = string.Join(", ", model.Topics.Select(t => t.Name))
						};

						var approvalDict = new Dictionary<string, object>
						{
							{ "Id",              approvalId                   },
							{ "SchoolId",        schoolId                     },
							{ "RequestedBy",     userId                       },
							{ "ApproverId",      teacher.LineManagerId!.Value },
							{ "OperationType",   OperationType.AddSubTopics   },
							{ "EntityType",      "Topic"                      },
							{ "EntityId",        subjectId                    },
							{ "Payload",         JsonSerializer.Serialize(
													 payloadSummary)          },
							{ "Status",          ApprovalStatus.Pending       },
							{ "RejectionReason", DBNull.Value                 },
							{ "CreatedAt",       now                          },
							{ "RespondedAt",     DBNull.Value                 },
							{ "ExpiresAt",       now.AddDays(expiryDays)      }
						};

						await _approvalRequestCommandRepository.Create(
							scope.Transaction, scope.Connection, approvalDict);
					}

					await scope.CommitAsync();
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Rolling back topics creation - SubjectId: {SubjectId}", subjectId);
					try { await scope.RollbackAsync(); }
					catch (Exception rbEx)
					{
						_logger.Error(rbEx,
							"Rollback failed - SubjectId: {SubjectId}", subjectId);
					}
					throw;
				}

				_logger.Information(
					"Topics created - SubjectId: {SubjectId}, TopicCount: {TopicCount}, " +
					"SubTopicCount: {SubTopicCount}, RequiresApproval: {RequiresApproval}",
					subjectId, topicDicts.Count, subTopicDicts.Count, requiresApproval);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = requiresApproval
						? $"{topicDicts.Count} topic(s) submitted for approval"
						: $"{topicDicts.Count} topic(s) created successfully",
					Status = "successful",
					Data = new
					{
						SubjectId = subjectId,
						SubjectName = subject.Subject,
						TopicsCreated = topicDicts.Count,
						SubTopicsCreated = subTopicDicts.Count,
						RequiresApproval = requiresApproval,
						Topics = topicResults
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Error creating topics - SubjectId: {SubjectId}", model.SubjectId);
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred",
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

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = students.Any()
							? $"{students.Count} student(s) found"
							: "No students found in this classroom",
						Status = "success",
						Data = students
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Error fetching students by classroom - ClassroomId: {ClassroomId}",
						classroomId);

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

					var sql = $@"
						SELECT
							u.Id,
							u.FirstName,
							u.LastName,
							u.UserName,
							u.EmailAddress,
							u.IsActive,
							u.CreationDate
						FROM   StudentMinorSubject sms
						JOIN   Users               u  ON u.Id = sms.StudentId
						WHERE  sms.SubjectId = '{subjectId}'
						AND    sms.SchoolId  = '{schoolId}'
						AND    u.IsActive    = 1
						ORDER  BY u.FirstName ASC";

					var rows = await _queryrepositoryUser.QueryAsync<StudentRowDto>(sql, new Dictionary<string, object>());

					var students = rows.ToList();

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = students.Any()
							? $"{students.Count} student(s) found"
							: "No students found for this subject",
						Status = "success",
						Data = students
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex,
						"Error fetching students by subject - SubjectId: {SubjectId}",
						subjectId);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.ErrorOccured,
						ResponseMessage = "An error occurred while fetching students",
						Status = "failed"
					};
				}
			}
		}

		public async Task<bool> HasPermission(Guid adminUserId, Guid schoolId, AdminPermission permission)
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


		private async Task<TeacherClassroom?> GetClassroomTeacherAssignment(Guid classroomId, Guid teacherId)
		{
			try
			{
				var query = $@"
					SELECT * FROM ClassroomTeacher 
					WHERE ClassroomId = '{classroomId}' 
					AND TeacherId = '{teacherId}'";

				var assignment = await _classroomTeacherQueryRespository.Get(query);
				return assignment;
			}
			catch (Exception ex)
			{
				_logger.Error(
					ex,
					"Error fetching classroom teacher assignment - ClassroomId: {ClassroomId}, TeacherId: {TeacherId}",
					classroomId,
					teacherId);
				return null;
			}
		}
		public async Task<BaseResponse> GetSubjectStatsAsync(Guid subjectId, Guid classroomId, AuthenticatedUserClaims userClaims)
		{
			try
			{
				if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Invalid school identification",
						Status = "failed"
					};
				}

				var subject = await _queryrepositorySubject.Get(subjectId);
				if (subject is null || !subject.IsActive || subject.SchoolId != schoolId)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.NotFound,
						ResponseMessage = "Subject not found",
						Status = "failed"
					};
				}

				var sql = $@"
                    SELECT
                        COUNT(*)                                          AS LessonCount,
                        SUM(CASE WHEN QuizCode IS NOT NULL THEN 1 ELSE 0 END) AS QuizCount
                    FROM LessonContent
                    WHERE SubjectId   = '{subjectId}'
                    AND   ClassroomId = '{classroomId}'
                    AND   SchoolId    = '{schoolId}'
                    AND   Status      IN ('Published', 'Approved')";

				using var conn = new Microsoft.Data.SqlClient.SqlConnection(_connString);
				conn.Open();
				var stats = await conn.QueryFirstOrDefaultAsync(sql);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Subject stats retrieved successfully",
					Status = "successful",
					Data = new SubjectStatsDto
					{
						SubjectId = subjectId,
						SubjectName = subject.Subject,
						LessonCount = (int)(stats?.LessonCount ?? 0),
						QuizCount = (int)(stats?.QuizCount ?? 0)
					}
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex,
					"Error fetching subject stats - SubjectId: {SubjectId}, ClassroomId: {ClassroomId}",
					subjectId, classroomId);
				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An error occurred while fetching subject stats",
					Status = "failed"
				};
			}
		}

		public Task<BaseResponse> CreateStudentClass(CreateStudentClassViewModel createStudentClassViewModel, AuthenticatedUserClaims userInfo)
		{
			throw new NotImplementedException();
		}

		private string HashPassword(string plainPassword)
		{
			using var sha256 = System.Security.Cryptography.SHA256.Create();
			var bytes = Encoding.UTF8.GetBytes(plainPassword);
			var hash = sha256.ComputeHash(bytes);
			return Convert.ToHexString(hash).ToLower();
		}

		public async Task<BaseResponse> ProvisionSchool(ProvisionSchoolViewModel model, AuthenticatedUserClaims claims)
		{
			using (LogContext.PushProperty("RequestedBy", claims.UserId))
			{
				try
				{
					if (!Guid.TryParse(claims.UserId, out var platformUserId))
						return new BaseResponse { ResponseCode = ResponseCode.Unauthorized, ResponseMessage = "Invalid authentication", Status = "failed" };

					if (model is null)
						return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "object is empty", Status = "failed" };

				if (string.IsNullOrWhiteSpace(model.TenantIdentifier))
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "Tenant identifier is required", Status = "failed" };

				if (string.IsNullOrWhiteSpace(model.SchoolCode))
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "School code is required", Status = "failed" };

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
				var schoolId = Guid.NewGuid();
				var adminUserId = Guid.NewGuid();

				var insertDict = new Dictionary<string, object> {
					{ "CreationDate", now }, { "ModifiedDate", now }, { "Id", schoolId },
					{ "SchoolName", model.SchoolName }, { "Location", model.Location },
					{ "CountryId", model.CountryId }, { "StateId", model.StateId },
					{ "State", (object?)model.State ?? DBNull.Value },
					{ "Address", model.Address }, { "HasBranch", model.HasBranch },
					{ "LogoUrl", (object?)model.LogoUrl ?? DBNull.Value }, { "IsActive", true }
				};

					using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");

					try
					{
					// Check tenant uniqueness
					var existingTenant = await scope.Connection.QueryFirstOrDefaultAsync<Guid?>(
						"SELECT TOP 1 Id FROM TenantInfo WHERE Identifier = @Identifier AND IsActive = 1",
						new { Identifier = model.TenantIdentifier }, scope.Transaction);

					if (existingTenant is not null)
					{
						await scope.RollbackAsync();
						return new BaseResponse { ResponseCode = ResponseCode.Conflict, ResponseMessage = "Tenant identifier already exists", Status = "failed" };
					}

					// Check school code uniqueness
					var existingCode = await scope.Connection.QueryFirstOrDefaultAsync<Guid?>(
						"SELECT TOP 1 SchoolId FROM SchoolCode WHERE Code = @Code",
						new { Code = model.SchoolCode }, scope.Transaction);

					if (existingCode is not null)
					{
						await scope.RollbackAsync();
						return new BaseResponse { ResponseCode = ResponseCode.Conflict, ResponseMessage = "School code already exists", Status = "failed" };
					}

					// Check admin username uniqueness (global)
					var existingUser = await scope.Connection.QueryFirstOrDefaultAsync<Guid?>(
						"SELECT TOP 1 Id FROM Users WHERE UserName = @Username",
						new { Username = model.AdminUsername }, scope.Transaction);

					if (existingUser is not null)
					{
						await scope.RollbackAsync();
						return new BaseResponse { ResponseCode = ResponseCode.Conflict, ResponseMessage = "Admin username already exists", Status = "failed" };
					}

					// 1. Insert School
					await _schCommandRespository.Create(scope.Transaction, scope.Connection, insertDict);

					// 2. Insert SchoolCode
					await _schCodeCommandRespository.Create(scope.Transaction, scope.Connection,
						new Dictionary<string, object> { { "SchoolId", schoolId }, { "Code", model.SchoolCode } });

						// 3. Insert TenantInfo
						await scope.Connection.ExecuteAsync(@"
							INSERT INTO TenantInfo (Id, SchoolId, Identifier, IsActive, ConnectionString, CreatedDate, ModifiedDate)
							VALUES (@Id, @SchoolId, @Identifier, 1, NULL, @Now, @Now)",
							new { Id = Guid.NewGuid(), SchoolId = schoolId, Identifier = model.TenantIdentifier, Now = DateTime.UtcNow },
							scope.Transaction);

					// 4. Insert Admin User (Administrator role)
					var passwordHash = HashPassword(model.AdminPassword);
					await scope.Connection.ExecuteAsync(@"
						INSERT INTO Users (Id, CreationDate, ModifiedDate, FirstName, MiddleName, LastName, EmailAddress, HashPassword,
							IsActive, HasAccess, UserName, SchoolId, RoleId, CreatedBy)
						VALUES (@Id, @Now, @Now, @FirstName, @MiddleName, @LastName, @Email, @PasswordHash,
							1, 1, @Username, @SchoolId, 2, @CreatedBy)",
						new
						{
							Id = adminUserId,
							Now = now,
							FirstName = model.AdminFirstName,
							MiddleName = (object?)model.AdminMiddleName ?? DBNull.Value,
							LastName = model.AdminLastName,
							Email = model.AdminEmail,
							PasswordHash = passwordHash,
							Username = model.AdminUsername,
							SchoolId = schoolId,
							CreatedBy = platformUserId
						},
						scope.Transaction);

						// 5. Insert AdminPermissions (FullAdmin = 127)
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
					}
					catch
					{
						await scope.RollbackAsync();
						throw;
					}

					// Send welcome email (fire-and-forget)
					_ = Task.Run(async () =>
					{
						try
						{
							var subject = $"Welcome to {model.SchoolName} - TechHub";
							var body = $@"
								<html>
								<body style='font-family: Arial, sans-serif;'>
									<h2>School Created Successfully</h2>
									<p>Dear {model.AdminFirstName},</p>
									<p>Your school <strong>{model.SchoolName}</strong> has been created on TechHub.</p>
									<h3>School Details</h3>
									<ul>
										<li><strong>School:</strong> {model.SchoolName}</li>
										<li><strong>School Code:</strong> {model.SchoolCode}</li>
										<li><strong>Tenant ID:</strong> {model.TenantIdentifier}</li>
										<li><strong>Location:</strong> {model.Location}</li>
									</ul>
									<h3>Admin Login Credentials</h3>
									<ul>
										<li><strong>Username:</strong> {model.AdminUsername}</li>
										<li><strong>Password:</strong> {model.AdminPassword}</li>
									</ul>
									<p>Please log in and change your password on first login.</p>
									<p>Best regards,<br/>TechHub Platform Team</p>
								</body>
								</html>";

							await _emailService.SendAsync(model.AdminEmail, $"{model.AdminFirstName} {model.AdminLastName}", subject, body);
							_logger.Information("Welcome email sent to {Email} for school {School}", model.AdminEmail, model.SchoolName);
						}
						catch (Exception ex)
						{
							_logger.Error(ex, "Failed to send welcome email for school {School}", model.SchoolName);
						}
					});

_logger.Information(
					"School provisioned - SchoolId: {SchoolId}, Code: {Code}, Name: {Name}, Tenant: {Tenant}, Admin: {Admin}",
					schoolId, model.SchoolCode, model.SchoolName, model.TenantIdentifier, model.AdminUsername);

					return new BaseResponse
					{
						ResponseCode = ResponseCode.successful,
						ResponseMessage = "School provisioned successfully",
						Status = "successful",
				Data = new
					{
						SchoolId = schoolId,
						SchoolName = model.SchoolName,
						SchoolCode = model.SchoolCode,
						TenantIdentifier = model.TenantIdentifier,
						LogoUrl = model.LogoUrl,
						AdminUserId = adminUserId,
						AdminUsername = model.AdminUsername,
						AdminEmail = model.AdminEmail
					}
					};
				}
				catch (Exception ex)
				{
					_logger.Error(ex, "Error provisioning school");
					return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = "An error occurred while provisioning school", Status = "failed" };
				}
			}
		}

		public async Task<BaseResponse> SubmitRegistrationRequest(SchoolRegistrationRequestViewModel model)
		{
			try
			{
				if (model is null)
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "Object is empty", Status = "failed" };

				if (string.IsNullOrWhiteSpace(model.TenantIdentifier))
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "Tenant identifier is required", Status = "failed" };

				if (string.IsNullOrWhiteSpace(model.SchoolCode))
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "School code is required", Status = "failed" };

				// Check tenant uniqueness
				using (var conn = new Microsoft.Data.SqlClient.SqlConnection(_connString))
				{
					var existingTenant = await conn.QueryFirstOrDefaultAsync<Guid?>(
						"SELECT TOP 1 Id FROM TenantInfo WHERE Identifier = @Identifier AND IsActive = 1",
						new { Identifier = model.TenantIdentifier });

					if (existingTenant is not null)
						return new BaseResponse { ResponseCode = ResponseCode.Conflict, ResponseMessage = "Tenant identifier already exists", Status = "failed" };

					var existingCode = await conn.QueryFirstOrDefaultAsync<Guid?>(
						"SELECT TOP 1 SchoolId FROM SchoolCode WHERE Code = @Code",
						new { Code = model.SchoolCode });

					if (existingCode is not null)
						return new BaseResponse { ResponseCode = ResponseCode.Conflict, ResponseMessage = "School code already exists", Status = "failed" };

					var existingUser = await conn.QueryFirstOrDefaultAsync<Guid?>(
						"SELECT TOP 1 Id FROM Users WHERE UserName = @Username",
						new { Username = model.AdminUsername });

					if (existingUser is not null)
						return new BaseResponse { ResponseCode = ResponseCode.Conflict, ResponseMessage = "Username already exists", Status = "failed" };
				}

				var entity = new SchoolRegistrationRequest
				{
					Id = Guid.NewGuid(),
					SchoolName = model.SchoolName,
					Location = model.Location,
					CountryId = model.CountryId,
					StateId = model.StateId,
					State = model.State,
					Address = model.Address,
					HasBranch = model.HasBranch,
					TenantIdentifier = model.TenantIdentifier,
					SchoolCode = model.SchoolCode,
					LogoUrl = model.LogoUrl,
					LogoPublicId = model.LogoPublicId,
					AdminFirstName = model.AdminFirstName,
					AdminMiddleName = model.AdminMiddleName,
					AdminLastName = model.AdminLastName,
					AdminEmail = model.AdminEmail,
					AdminUsername = model.AdminUsername,
					AdminPassword = model.AdminPassword,
					Status = "Pending",
					CreatedAt = DateTime.UtcNow
				};

				var insertDict = new Dictionary<string, object>
				{
					{ "Id", entity.Id },
					{ "SchoolName", entity.SchoolName },
					{ "Location", entity.Location },
					{ "CountryId", entity.CountryId },
					{ "StateId", entity.StateId },
					{ "State", (object?)entity.State ?? DBNull.Value },
					{ "Address", entity.Address },
					{ "HasBranch", entity.HasBranch },
					{ "TenantIdentifier", entity.TenantIdentifier },
					{ "SchoolCode", entity.SchoolCode },
					{ "LogoUrl", (object?)entity.LogoUrl ?? DBNull.Value },
					{ "LogoPublicId", (object?)entity.LogoPublicId ?? DBNull.Value },
					{ "AdminFirstName", entity.AdminFirstName },
					{ "AdminMiddleName", (object?)entity.AdminMiddleName ?? DBNull.Value },
					{ "AdminLastName", entity.AdminLastName },
					{ "AdminEmail", entity.AdminEmail },
					{ "AdminUsername", entity.AdminUsername },
					{ "AdminPassword", entity.AdminPassword },
					{ "Status", entity.Status },
					{ "CreatedAt", entity.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss") }
				};

				await _registrationRequestCommandRepository.Create(insertDict);

				_logger.Information("School registration request submitted - School: {Name}, Code: {Code}", model.SchoolName, model.SchoolCode);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Registration request submitted successfully. Awaiting approval.",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error submitting school registration request");
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = "An error occurred while submitting registration request", Status = "failed" };
			}
		}

		public async Task<BaseResponse> GetRegistrationRequests(string? statusFilter, AuthenticatedUserClaims claims)
		{
			try
			{
				if (!Guid.TryParse(claims.UserId, out _))
					return new BaseResponse { ResponseCode = ResponseCode.Unauthorized, ResponseMessage = "Invalid authentication", Status = "failed" };

				using var conn = new Microsoft.Data.SqlClient.SqlConnection(_connString);
				IEnumerable<SchoolRegistrationRequest> results;

				if (!string.IsNullOrWhiteSpace(statusFilter))
				{
					results = await conn.QueryAsync<SchoolRegistrationRequest>(
						"SELECT * FROM SchoolRegistrationRequest WHERE Status = @Status ORDER BY CreatedAt DESC",
						new { Status = statusFilter });
				}
				else
				{
					results = await conn.QueryAsync<SchoolRegistrationRequest>(
						"SELECT * FROM SchoolRegistrationRequest ORDER BY CreatedAt DESC");
				}

				var mapped = results.Select(r => new RegistrationRequestResponse
				{
					Id = r.Id,
					SchoolName = r.SchoolName,
					Location = r.Location,
					Address = r.Address,
					TenantIdentifier = r.TenantIdentifier,
					SchoolCode = r.SchoolCode,
					LogoUrl = r.LogoUrl,
					LogoPublicId = r.LogoPublicId,
					AdminFirstName = r.AdminFirstName,
					AdminLastName = r.AdminLastName,
					AdminEmail = r.AdminEmail,
					AdminUsername = r.AdminUsername,
					Status = r.Status,
					RejectionReason = r.RejectionReason,
					CreatedAt = r.CreatedAt,
					RespondedAt = r.RespondedAt
				}).ToList();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Registration requests retrieved",
					Status = "successful",
					Data = mapped
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error retrieving registration requests");
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = "An error occurred", Status = "failed" };
			}
		}

		public async Task<BaseResponse> ApproveRegistrationRequest(Guid requestId, AuthenticatedUserClaims claims)
		{
			if (!Guid.TryParse(claims.UserId, out var platformUserId))
				return new BaseResponse { ResponseCode = ResponseCode.Unauthorized, ResponseMessage = "Invalid authentication", Status = "failed" };

			using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
			try
			{
				var request = await _registrationRequestQueryRepository.Get(requestId);
				if (request is null)
				{
					await scope.RollbackAsync();
					return new BaseResponse { ResponseCode = ResponseCode.NotFound, ResponseMessage = "Registration request not found", Status = "failed" };
				}

				if (request.Status != "Pending")
				{
					await scope.RollbackAsync();
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = $"Request already {request.Status}", Status = "failed" };
				}

				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
				var schoolId = Guid.NewGuid();
				var adminUserId = Guid.NewGuid();

				// 1. Insert School
				var schoolInsertDict = new Dictionary<string, object>
				{
					{ "CreationDate", now }, { "ModifiedDate", now }, { "Id", schoolId },
					{ "SchoolName", request.SchoolName }, { "Location", request.Location },
					{ "CountryId", request.CountryId }, { "StateId", request.StateId },
					{ "State", (object?)request.State ?? DBNull.Value },
					{ "Address", request.Address }, { "HasBranch", request.HasBranch },
					{ "LogoUrl", (object?)request.LogoUrl ?? DBNull.Value },
					{ "LogoPublicId", (object?)request.LogoPublicId ?? DBNull.Value },
					{ "IsActive", true }
				};
				await _schCommandRespository.Create(scope.Transaction, scope.Connection, schoolInsertDict);

				// 2. Insert SchoolCode
				await _schCodeCommandRespository.Create(scope.Transaction, scope.Connection,
					new Dictionary<string, object> { { "SchoolId", schoolId }, { "Code", request.SchoolCode } });

				// 3. Insert TenantInfo
				await scope.Connection.ExecuteAsync(@"
					INSERT INTO TenantInfo (Id, SchoolId, Identifier, IsActive, ConnectionString, CreatedDate, ModifiedDate)
					VALUES (@Id, @SchoolId, @Identifier, 1, NULL, @Now, @Now)",
					new { Id = Guid.NewGuid(), SchoolId = schoolId, Identifier = request.TenantIdentifier, Now = DateTime.UtcNow },
					scope.Transaction);

				// 4. Insert Admin User (SuperAdministrator role - RoleId = 3)
				var passwordHash = HashPassword(request.AdminPassword);
				await scope.Connection.ExecuteAsync(@"
					INSERT INTO Users (Id, CreationDate, ModifiedDate, FirstName, MiddleName, LastName, EmailAddress, HashPassword,
						IsActive, HasAccess, UserName, SchoolId, RoleId, CreatedBy)
					VALUES (@Id, @Now, @Now, @FirstName, @MiddleName, @LastName, @Email, @PasswordHash,
						1, 1, @Username, @SchoolId, 3, @CreatedBy)",
					new
					{
						Id = adminUserId,
						Now = now,
						FirstName = request.AdminFirstName,
						MiddleName = (object?)request.AdminMiddleName ?? DBNull.Value,
						LastName = request.AdminLastName,
						Email = request.AdminEmail,
						PasswordHash = passwordHash,
						Username = request.AdminUsername,
						SchoolId = schoolId,
						CreatedBy = platformUserId
					},
					scope.Transaction);

				// 5. Insert AdminPermissions (FullAdmin = 127)
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

				// 6. Update request status
				await scope.Connection.ExecuteAsync(@"
					UPDATE SchoolRegistrationRequest SET Status = 'Approved', ApprovedBy = @ApprovedBy, RespondedAt = @RespondedAt
					WHERE Id = @Id",
					new { Id = requestId, ApprovedBy = platformUserId, RespondedAt = DateTime.UtcNow },
					scope.Transaction);

				await scope.CommitAsync();

				_logger.Information("School registration approved - School: {Name}, Code: {Code}", request.SchoolName, request.SchoolCode);

				// Send welcome email (fire-and-forget)
				_ = Task.Run(async () =>
				{
					try
					{
						var subject = $"Welcome to {request.SchoolName} - TechHub";
						var body = $@"
							<html>
							<body style='font-family: Arial, sans-serif;'>
								<h2>School Registration Approved</h2>
								<p>Dear {request.AdminFirstName},</p>
								<p>Your school <strong>{request.SchoolName}</strong> has been approved and created on TechHub.</p>
								<h3>School Details</h3>
								<ul>
									<li><strong>School:</strong> {request.SchoolName}</li>
									<li><strong>School Code:</strong> {request.SchoolCode}</li>
									<li><strong>Tenant ID:</strong> {request.TenantIdentifier}</li>
									<li><strong>Location:</strong> {request.Location}</li>
								</ul>
								<h3>Admin Login Credentials</h3>
								<ul>
									<li><strong>Username:</strong> {request.AdminUsername}</li>
									<li><strong>Password:</strong> {request.AdminPassword}</li>
								</ul>
								<p>Please log in and change your password on first login.</p>
								<p>Best regards,<br/>TechHub Platform Team</p>
							</body>
							</html>";

						await _emailService.SendAsync(request.AdminEmail, $"{request.AdminFirstName} {request.AdminLastName}", subject, body);
						_logger.Information("Welcome email sent to {Email} for school {School}", request.AdminEmail, request.SchoolName);
					}
					catch (Exception ex)
					{
						_logger.Error(ex, "Failed to send welcome email for school {School}", request.SchoolName);
					}
				});

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "School registration approved and provisioned successfully",
					Status = "successful",
					Data = new
					{
						SchoolId = schoolId,
						SchoolName = request.SchoolName,
						SchoolCode = request.SchoolCode,
						TenantIdentifier = request.TenantIdentifier,
						AdminUserId = adminUserId,
						AdminUsername = request.AdminUsername
					}
				};
			}
			catch (Exception ex)
			{
				await scope.RollbackAsync();
				_logger.Error(ex, "Error approving school registration");
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = "An error occurred while approving registration", Status = "failed" };
			}
		}

		public async Task<BaseResponse> RejectRegistrationRequest(Guid requestId, string reason, AuthenticatedUserClaims claims)
		{
			if (!Guid.TryParse(claims.UserId, out var platformUserId))
				return new BaseResponse { ResponseCode = ResponseCode.Unauthorized, ResponseMessage = "Invalid authentication", Status = "failed" };

			try
			{
				var request = await _registrationRequestQueryRepository.Get(requestId);
				if (request is null)
					return new BaseResponse { ResponseCode = ResponseCode.NotFound, ResponseMessage = "Registration request not found", Status = "failed" };

				if (request.Status != "Pending")
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = $"Request already {request.Status}", Status = "failed" };

				using var conn = new Microsoft.Data.SqlClient.SqlConnection(_connString);
				await conn.ExecuteAsync(@"
					UPDATE SchoolRegistrationRequest SET Status = 'Rejected', RejectionReason = @Reason, ApprovedBy = @ApprovedBy, RespondedAt = @RespondedAt
					WHERE Id = @Id",
					new { Id = requestId, Reason = reason, ApprovedBy = platformUserId, RespondedAt = DateTime.UtcNow });

				_logger.Information("School registration rejected - School: {Name}, Code: {Code}, Reason: {Reason}",
					request.SchoolName, request.SchoolCode, reason);

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = "Registration request rejected",
					Status = "successful"
				};
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error rejecting school registration");
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = "An error occurred", Status = "failed" };
			}
		}
	}
}
