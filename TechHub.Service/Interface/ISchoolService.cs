using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.classroom;
using TechHub.Core.ViewModel.school;

namespace TechHub.Service.Interface
{
	public interface ISchoolService
	{
		Task<BaseResponse> CreateSchool(SchoolViewModel schoolViewModel);
		Task<BaseResponse> GetAllStates(int countryId);
		Task<BaseResponse> UpdateSchoolCode(SchoolCodeViewModel schoolCode);
		Task<BaseResponse> CreateStudentClass(CreateStudentClassViewModel createStudentClassViewModel, AuthenticatedUserClaims userInfo);
		Task<BaseResponse> CreateStudentClassV2(CreateStudentClassViewModel createStudentClassViewModel, AuthenticatedUserClaims userInfo);
		Task<BaseResponse> CreateSchoolSubjects(CreateSubjectViewModel createSubjectModel, AuthenticatedUserClaims userClaims);
		Task<BaseResponse> GetAllSubjects(Guid schoolid);
		Task<BaseResponse> RegisterClassroomSubjects(CreateClassroomViewModel createClassroomViewModel, AuthenticatedUserClaims userInfo);
		Task<BaseResponse> UpdateSchoolId(updateSchoolSubject updateSchoolSubject);
		Task<BaseResponse> UpdateSchoolClassroom(UpdateClassroomView updateClassroomView, AuthenticatedUserClaims userInfo);
		Task<BaseResponse> GetSubjectById(Guid subjectId, AuthenticatedUserClaims userClaims);
		Task<BaseResponse> GetClassroomById(Guid classroomId, AuthenticatedUserClaims userClaims);
		Task<BaseResponse> GetSubjectsByClassroom(Guid classroomId, AuthenticatedUserClaims userClaims);
		Task<BaseResponse> AssignTeachersToClassroom(AssignTeacherViewModel assignTeachersViewModel, AuthenticatedUserClaims userInfo);
	}
}
