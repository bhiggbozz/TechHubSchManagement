using AutoMapper;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Entities;
using TechHub.Core.Helper;
using TechHub.Core.ViewModel;
using TechHub.Service.Interface;
using TechhubMS.util;


namespace TechHub.Service.Service
{
	public class SchoolService : ISchoolService
	{
		private readonly ICommandRespository<School> _schCommandRespository;
		private readonly IMapper _mapper;
		public SchoolService(ICommandRespository<School> schCommandRespository, IMapper mapper)
		{
			_schCommandRespository = schCommandRespository;
			_mapper = mapper;
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
				await _schCommandRespository.Create(school);
				return new BaseResponse { ResponseCode = ResponseCode.successful, ResponseMessage = "object created successfully", Status = "successful" };
			}
			catch (Exception ex)
			{
				return new BaseResponse { ResponseCode = ResponseCode.ErrorOccured, ResponseMessage = ex.Message, Status = "failed" };
			}

		}
	}
}
