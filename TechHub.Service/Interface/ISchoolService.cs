using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.ViewModel;
using TechHub.Core;

namespace TechHub.Service.Interface
{
	public interface ISchoolService
	{
		Task<BaseResponse> CreateSchool(SchoolViewModel schoolViewModel);
		Task<BaseResponse> GetAllStates(int countryId);
		Task<BaseResponse> UpdateSchoolCode(SchoolCodeViewModel schoolCode);
		Task<BaseResponse> CreateStudentClass(CreateStudentClassViewModel createStudentClassViewModel);
		Task<BaseResponse> CreateSchoolSubjects(CreateSubjectViewModel createSubjectModel);
		Task<BaseResponse> GetAllSubjects(Guid schoolid);
	}
}
