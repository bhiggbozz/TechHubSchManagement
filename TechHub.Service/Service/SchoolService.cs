using AutoMapper;
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
using TechHub.Core.Entities;
using TechHub.Core.Helper;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;
using TechHub.Service.Service.DatabaseService;
using TechhubMS.util;


namespace TechHub.Service.Service
{
	public class SchoolService : ISchoolService
	{
		private readonly ICommandRespository<School> _schCommandRespository;
		private readonly ICommandRespository<SchoolCode> _schCodeCommandRespository;
		private readonly IQueryRepository<State> _queryrepositoryState;
		private readonly IConfiguration _configuration;
		private readonly IDbTransactionScopeFactory _dbTransactionScopeFactory;
		private readonly string? _connString;

		private readonly IMapper _mapper;
		public SchoolService(ICommandRespository<School> schCommandRespository, IQueryRepository<State> queryRepositoryState , 
			IMapper mapper, ICommandRespository<SchoolCode> schCodeCommandRespository,
			IDbTransactionScopeFactory dbTransactionScopeFactory,IConfiguration configuration)
		{
			_schCommandRespository = schCommandRespository;
			_queryrepositoryState = queryRepositoryState;
			_schCodeCommandRespository= schCodeCommandRespository;
			_configuration = configuration;
			_dbTransactionScopeFactory = dbTransactionScopeFactory;
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
	}
}
