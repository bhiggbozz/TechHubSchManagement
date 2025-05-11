using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Entities;
using TechHub.Core.ViewModel;

namespace TechHub.Core.Profiles
{
	public class AutoMapperProfile : Profile
	{
		public AutoMapperProfile()
		{
			CreateMap<SchoolViewModel, School>();
		}
	}
}
