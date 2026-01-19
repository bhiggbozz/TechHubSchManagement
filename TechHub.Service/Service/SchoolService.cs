using AutoMapper;
using Azure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TechHub.Core;
using TechHub.Core.Constant;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Helper;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.school;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;
using TechhubMS.util;
using static System.Formats.Asn1.AsnWriter;


namespace TechHub.Service.Service
{
	public class SchoolService : ISchoolService
	{
		private readonly ICommandRespository<School> _schCommandRespository;
		private readonly ICommandRespository<SchoolCode> _schCodeCommandRespository;
		private readonly ICommandRespository<Classroom> _studentClassCommandRespository;
		private readonly ICommandRespository<Subjects> _subjectCommandRespository;
		private readonly ICommandRespository<ClassroomSubjects> _classroomSubjectCommandRespository;
		private readonly IQueryRepository<State> _queryrepositoryState;
		private readonly IQueryRepository<Subjects> _queryrepositorySubject;
		private readonly IQueryRepository<Users> _queryrepositoryUser;
		private readonly IQueryRepository<Classroom> _studentClassQueryRespository;
		private readonly IQueryRepository<ClassroomSubjects> _classroomSubjectQueryRespository;


		private readonly IConfiguration _configuration;
		private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;
		private readonly string? _connString;

		private readonly IMapper _mapper;
		public SchoolService(ICommandRespository<School> schCommandRespository, IQueryRepository<State> queryRepositoryState , 
			IMapper mapper, ICommandRespository<SchoolCode> schCodeCommandRespository, ICommandRespository<Classroom> studentClassCommandRespository,
			ICommandRespository<ClassroomSubjects> classroomSubjectCommandRespository, IQueryRepository<ClassroomSubjects> classroomSubjectQueryRespository,
			IDbTransactionScopeFactory dbTransactionScopeFactory, IQueryRepository<Users> queryrepositoryUser, IQueryRepository<Classroom> studentClassQueryRespository,
		ICommandRespository<Subjects> subjectCommandRespository,
			IQueryRepository<Subjects> queryrepositorySubject,IConfiguration configuration)
		{
			_schCommandRespository = schCommandRespository;
			_queryrepositoryState = queryRepositoryState;
			_schCodeCommandRespository= schCodeCommandRespository;
			_studentClassCommandRespository = studentClassCommandRespository;
			_subjectCommandRespository = subjectCommandRespository;
			_queryrepositorySubject = queryrepositorySubject;
			_studentClassQueryRespository = studentClassQueryRespository;
			_classroomSubjectCommandRespository = classroomSubjectCommandRespository;
			_classroomSubjectQueryRespository = classroomSubjectQueryRespository;

			_configuration = configuration;
			_dbTransactionScopeFactory = dbTransactionScopeFactory;
			_queryrepositoryUser = queryrepositoryUser;
			_mapper = mapper;
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
					{"Address", school.Address }, { "HasBranch", school.HasBranch}, { "IsActive", school.ISActive} };
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
			try
			{
				if (createStudentClassViewModel is null)
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "Classroom data cannot be empty",
						Status = "failed"
					};
				}

				if (!createStudentClassViewModel.classrooms.Any())
				{
					return new BaseResponse
					{
						ResponseCode = ResponseCode.BadRequest,
						ResponseMessage = "At least one classroom is required",
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

				var classroomsToCreate = new List<Dictionary<string, object>>();
				var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

				foreach (var classroomView in createStudentClassViewModel.classrooms)
				{
					if (string.IsNullOrWhiteSpace(classroomView.Name))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Classroom name cannot be empty",
							Status = "failed"
						};
					}

					if (string.IsNullOrWhiteSpace(classroomView.TeacherName))
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.BadRequest,
							ResponseMessage = "Teacher name cannot be empty",
							Status = "failed"
						};
					}

					var duplicateCheck = await CheckDuplicateClassroom(classroomView.Name, schoolId);
					if (duplicateCheck)
					{
						return new BaseResponse
						{
							ResponseCode = ResponseCode.Conflict,
							ResponseMessage = $"Classroom '{classroomView.Name}' already exists in your school",
							Status = "failed"
						};
					}

					var classroomDict = new Dictionary<string, object>
					{
						{ "Id", Guid.NewGuid() },
						{ "Name", classroomView.Name.Trim() },
						{ "TeacherName", classroomView.TeacherName.Trim() },
						{ "NoOfStudents", classroomView.NoOfStudents },
						{ "CreationDate", now },
						{ "ModifiedDate", now },
						{ "CreatedBy", createdBy },
						{ "SchoolId", schoolId },
						{ "IsActive", true }
					};

					classroomsToCreate.Add(classroomDict);
				}

				// Use transaction for bulk insert
				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");

				foreach (var classroomDict in classroomsToCreate)
				{
					await _studentClassCommandRespository.Create(scope.Transaction, scope.Connection, classroomDict);
				}

				await scope.CommitAsync();

				return new BaseResponse
				{
					ResponseCode = ResponseCode.successful,
					ResponseMessage = $"{classroomsToCreate.Count} classroom(s) created successfully",
					Status = "successful",
					Data = new
					{
						ClassroomsCreated = classroomsToCreate.Count,
						ClassroomIds = classroomsToCreate.Select(c => c["Id"]).ToList()
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
				// Log exception here
				// _logger.LogError(ex, "Unexpected error occurred while creating classrooms");

				return new BaseResponse
				{
					ResponseCode = ResponseCode.ErrorOccured,
					ResponseMessage = "An unexpected error occurred while creating classrooms",
					Status = "failed"
				};
			}
		}

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
		private async Task<List<(string Name, string Category, string ClassCategory)>> GetExistingSubjects(
			List<SubjectsDetails> subjects,
			Guid schoolId)
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
		public async Task<BaseResponse> UpdateSchoolId(updateSchoolSubject updateSchoolSubjects)
		{
			try
			{
				if (updateSchoolSubjects.subjectUpdates.Count == 0)
				{
					throw new ArgumentNullException(nameof(updateSchoolSubject));
				};
				var columnInput = new Dictionary<string, object> { { "Id", updateSchoolSubjects.CreatedBy }, { "SchoolId", updateSchoolSubjects.SchoolId } };
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

				var nameConflicts = await CheckNameConflicts(
					updateClassroomView.classroomUpdateViews,
					schoolId
				);

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
						{ "ModifiedBy", modifiedBy },
						{ "SchoolId", schoolId } 
					};

							inputValueList.Add(values);
					}

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				await _studentClassCommandRespository.UpdateBatchByIdAsync(scope.Transaction, scope.Connection, inputValueList);
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

		/// <summary>
		/// Get existing subjects for the school to prevent duplicates
		/// </summary>
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

		public Task<BaseResponse> CreateStudentClass(CreateStudentClassViewModel createStudentClassViewModel, AuthenticatedUserClaims userInfo)
		{
			throw new NotImplementedException();
		}
	}
}
