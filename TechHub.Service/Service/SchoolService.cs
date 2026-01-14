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
		private readonly IQueryRepository<State> _queryrepositoryState;
		private readonly IQueryRepository<Subjects> _queryrepositorySubject;
		private readonly IQueryRepository<Users> _queryrepositoryUser;
		private readonly IConfiguration _configuration;
		private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;
		private readonly string? _connString;

		private readonly IMapper _mapper;
		public SchoolService(ICommandRespository<School> schCommandRespository, IQueryRepository<State> queryRepositoryState , 
			IMapper mapper, ICommandRespository<SchoolCode> schCodeCommandRespository, ICommandRespository<Classroom> studentClassCommandRespository,
			IDbTransactionScopeFactory dbTransactionScopeFactory, IQueryRepository<Users> queryrepositoryUser, ICommandRespository<Subjects> subjectCommandRespository,
			IQueryRepository<Subjects> queryrepositorySubject,IConfiguration configuration)
		{
			_schCommandRespository = schCommandRespository;
			_queryrepositoryState = queryRepositoryState;
			_schCodeCommandRespository= schCodeCommandRespository;
			_studentClassCommandRespository = studentClassCommandRespository;
			_subjectCommandRespository = subjectCommandRespository;
			_queryrepositorySubject = queryrepositorySubject;
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
		public async Task<BaseResponse> CreateStudentClassV2(CreateStudentClassViewModel createStudentClassViewModel)
		{
			try
			{
				if (createStudentClassViewModel == null)
				{
					throw new ArgumentNullException(nameof(createStudentClassViewModel));
				}
				var columnInput = new Dictionary<string, object> { { "Id", createStudentClassViewModel.CreatedBy }, { "SchoolId", createStudentClassViewModel.SchoolId } };
				string query = "select * from Users  where Id = @Id and SchoolId = @SchoolId";
				var user = await _queryrepositoryUser.SelectByColumns(query, columnInput);
				if (user == null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This user does not exist", Status = "successful" };
				}
				var mappedSchClass = _mapper.Map<Classroom>(createStudentClassViewModel);
				var inputValue = new Dictionary<string, object> { { "Id", mappedSchClass.Id}, {"Name", mappedSchClass.Name },
				{"CreationDate", mappedSchClass.CreationDate }, {"ModifiedDate", mappedSchClass.ModifiedDate }, {"CreatedBy", mappedSchClass.CreatedBy },
				{ "SchoolId", mappedSchClass.SchoolId}, {"NoOfStudents", mappedSchClass.NoOfStudents } };
				await _studentClassCommandRespository.Create(mappedSchClass);
				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "object updated successfully", Status = "successful" };

			}
			catch (ArgumentNullException ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = ex.Message, Status = "falied" };
			}
			catch (SqlException ex)
			{
				if (ex.Message.ToLower().Contains("duplicate"))
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "class exists", Status = "failed" };
				}
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
			}
			catch (Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
			}


		}

		public async Task<BaseResponse> CreateStudentClass(CreateStudentClassViewModel createStudentClassViewModel)
		{
			try
			{
				if (createStudentClassViewModel.classrooms.Count == 0)
				{
					throw new ArgumentNullException(nameof(createStudentClassViewModel));
				}
				var columnInput = new Dictionary<string, object> { { "Id", createStudentClassViewModel.CreatedBy }, { "SchoolId", createStudentClassViewModel.SchoolId } };
				string query = "select * from Users  where Id = @Id and SchoolId = @SchoolId";
				var user = await _queryrepositoryUser.SelectByColumns(query, columnInput);
				if (user == null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This user does not exist", Status = "successful" };
				}
				if (!user.IsActive)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This user is not active", Status = "failed" };
				}
				var inputValueList = new List<Dictionary<string, object>>();
				foreach(var classroom in createStudentClassViewModel.classrooms)
				{
					var mappedSchClass = _mapper.Map<Classroom>(classroom);
					var inputValue = new Dictionary<string, object> { { "Id", mappedSchClass.Id}, {"Name", mappedSchClass.Name },
				{"CreationDate", mappedSchClass.CreationDate }, {"ModifiedDate", mappedSchClass.ModifiedDate }, {"CreatedBy", mappedSchClass.CreatedBy },
				{ "SchoolId", mappedSchClass.SchoolId}, {"NoOfStudents", mappedSchClass.NoOfStudents }, { "IsActive", mappedSchClass.IsActive} };
					inputValueList.Add(inputValue);
				}
				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				//await _schCommandRespository.Create(school);
				//await _schCommandRespository.Create(scope.Transaction, scope.Connection, insertDict);
				await _studentClassCommandRespository.CreateBatchAsync(scope.Transaction, scope.Connection, inputValueList);
				await scope.CommitAsync();
				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "object updated successfully", Status = "successful" };
				//await _studentClassCommandRespository.Create(mappedSchClass);
				//return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "object updated successfully", Status = "successful" };

			}
			catch (ArgumentNullException ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = ex.Message, Status = "falied" };
			}
			catch (SqlException ex)
			{
				if (ex.Message.ToLower().Contains("duplicate"))
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "class exists", Status = "failed" };
				}
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
			}
			catch (Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
			}


		}
		public async Task<BaseResponse> CreateSchoolSubjects(CreateSubjectViewModel createSubjectModel)
		{
			try
			{
				if (createSubjectModel == null)
				{
					throw new ArgumentNullException(nameof(createSubjectModel));
				}
				var columnInput = new Dictionary<string, object> { { "Id", createSubjectModel.CreatedBy }, { "SchoolId", createSubjectModel.SchoolId } };
				string query = "select * from Users  where Id = @Id and SchoolId = @SchoolId";
				var user = await _queryrepositoryUser.SelectByColumns(query, columnInput);
				if (user == null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This user does not exist", Status = "failed" };
				}
				if (!user.IsActive)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "This user is not active", Status = "failed" };
				}
				if(!createSubjectModel.Subjects.Any())
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "Subject List cannot be empty", Status = "failed" };
				}

				var mappedSubjectObj = _mapper.Map<Subjects>(createSubjectModel);
				var inputValuesLIst = new List<Dictionary<string, object>>();
				foreach(var subject in createSubjectModel.Subjects)
				{
					var inputValue = new Dictionary<string, object> { { "Id",Guid.NewGuid()}, {"Subject", subject.Subject},
					{"CreationDate", mappedSubjectObj.CreationDate }, {"ModifiedDate", mappedSubjectObj.ModifiedDate }, {"CreatedBy", mappedSubjectObj.CreatedBy },
					{ "SchoolId", mappedSubjectObj.SchoolId},{ "Category", subject.Category}, { "IsActive", subject.status} };
					inputValuesLIst.Add(inputValue);
				}
				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				//await _schCommandRespository.Create(school);
				//await _schCommandRespository.Create(scope.Transaction, scope.Connection, insertDict);
				await _subjectCommandRespository.CreateBatchAsync(scope.Transaction, scope.Connection, inputValuesLIst);
				await scope.CommitAsync();
				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "object updated successfully", Status = "successful" };

			}
			catch (ArgumentNullException ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = ex.Message, Status = "falied" };
			}
			catch (SqlException ex)
			{
				if (ex.Message.ToLower().Contains("duplicate"))
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "class exists", Status = "failed" };
				}
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
			}
			catch (Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
			}

		}
		public async Task<BaseResponse> GetAllSubjects(Guid schoolid)
		{
			try
			{
				//var keyValue = new KeyValuePair<string, object> ("schoolId",  Guid.Parse(schoolid) );
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

		public async Task<BaseResponse> RegisterClassroomSubjects(CreateClassroomViewModel createClassroomViewModel)
		{
			try
			{
				if (createClassroomViewModel == null)
				{
					throw new ArgumentNullException(nameof(createClassroomViewModel));
				};

				var mappedClassroom = _mapper.Map<Classroom>(createClassroomViewModel);
				var inputValues = new List<Dictionary<string, object>>();
				if (!createClassroomViewModel.SubjectId.Any())
				{
					return new BaseResponse { ResponseCode = ResponseCode.BadRequest, ResponseMessage = "No Subject selected", Status = "failed" };

				}
				var userInputValues = new Dictionary<string, object> { { "SchoolId", createClassroomViewModel.SchoolId }, { "Id", createClassroomViewModel.CreatedBy } };
				var user = await _queryrepositoryUser.GetBy(userInputValues);
				if (user == null)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = ResponseMessage.UserDoesNotExist, Status = "failed" };
				}
				if (!user.IsActive)
				{
					return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = ResponseMessage.UserNotActive, Status = "failed" };
				}
				foreach (var subjectId in createClassroomViewModel.SubjectId)
				{
					var classroomSubject = new ClassroomSubjects();
					var rowInputValue = new Dictionary<string, object> { { "id", classroomSubject.Id}, { "CreationDate", classroomSubject.CreationDate}, { "ModifiedDate", classroomSubject.ModifiedDate},
					{ "ClassroomId",createClassroomViewModel.ClassroomId },{ "SubjectId", subjectId }, { "SchoolId", createClassroomViewModel.SchoolId}, { "CreatedBy", createClassroomViewModel.CreatedBy} };
					inputValues.Add(rowInputValue);
				}

				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				//await _schCommandRespository.Create(school);
				await _schCommandRespository.CreateBatchAsync(scope.Transaction, scope.Connection, inputValues);
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
		public async Task<BaseResponse> UpdateSchoolId(updateSchoolSubject updateSchoolSubjects)
		{
			try
			{
				if (updateSchoolSubjects.subjectUpdates.Count == 0)
				{
					throw new ArgumentNullException(nameof(updateSchoolSubject));
				};
				var columnInput = new Dictionary<string, object> { { "Id", updateSchoolSubjects.CreatedBy }, { "SchoolId", updateSchoolSubjects.SchoolId } };
				string query = "select * from Users  where Id = @Id and SchoolId = @SchoolId";
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
		
		public async Task<BaseResponse> UpdateSchoolClassroom(UpdateClassroomView updateClassroomView)
		{
			try
			{
				if (updateClassroomView?.classroomUpdateViews.Count == 0)
				{
					throw new ArgumentNullException(nameof(UpdateClassroomView));
				}
				var columnInput = new Dictionary<string, object> { { "Id", updateClassroomView.CreatedBy }, { "SchoolId", updateClassroomView.SchoolId } };
				string query = "select * from Users  where Id = @Id and SchoolId = @SchoolId";
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
				foreach (var classroom in updateClassroomView?.classroomUpdateViews)
				{
					var values = new Dictionary<string, object> { { "Name", classroom.Name }, { "IsActive", classroom.IsActive },
					{ "ModifiedDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},{"CreatedBy",updateClassroomView.CreatedBy },{ "Id", classroom.Id}  };
					inputValueList.Add(values);
				}
				using var scope = _dbTransactionScopeFactory.Create("DbConnectionString");
				await _studentClassCommandRespository.UpdateBatchByIdAsync(scope.Transaction, scope.Connection, inputValueList);
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



	}
}
