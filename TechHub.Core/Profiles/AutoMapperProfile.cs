using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Entities;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;

namespace TechHub.Core.Profiles
{
	public class AutoMapperProfile : Profile
	{
		public AutoMapperProfile()
		{
			CreateMap<SchoolViewModel, School>();
			CreateMap<LoginViewModel, LoginHistory>();
			CreateMap<StateResponse, State>();
			CreateMap<SchoolResponseModel, School>();
			CreateMap<UserViewModel, User>();
		}
	}
}
