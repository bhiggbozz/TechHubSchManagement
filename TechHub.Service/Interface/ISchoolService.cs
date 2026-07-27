using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.classroom;
using TechHub.Core.ViewModel.Platform;
using TechHub.Core.ViewModel.school;
using TechHub.QuestionBank.Core.DTO;

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
		Task<ClassroomDetails> GetAllClassrooms(AuthenticatedUserClaims userInfo, int pageNumber = 1, int pageSize = 50);
		Task<SubjectsListResponse> GetSubjectsByClassCategory(int classCategory,AuthenticatedUserClaims userInfo,int pageNumber = 1,int pageSize = 50);
		Task<SubjectsListResponse> GetSubjectsBySubjectCategory(int subjectCategory,AuthenticatedUserClaims userInfo,int pageNumber = 1,int pageSize = 50);

		Task<SubjectsListResponse> GetAllSubjects(AuthenticatedUserClaims userInfo,int? classCategory = null,int? subjectCategory = null,int pageNumber = 1,int pageSize = 50);
		Task<BaseResponse> UpdateClassroomTeachers(UpdateClassroomTeachersViewModel updateClassroomTeachersViewModel,AuthenticatedUserClaims userInfo);
		Task<BaseResponse> UpdateSchoolLogoAsync(IFormFile logo, AuthenticatedUserClaims userClaims);

		Task<CreateTopicResponse> CreateTopic(CreateTopicViewModel model,AuthenticatedUserClaims userClaims);

		Task<TopicListResponse> GetTopics(Guid subjectId,AuthenticatedUserClaims userClaims);

		// SubTopic
		Task<CreateSubTopicResponse> CreateSubTopic(CreateSubTopicViewModel model,AuthenticatedUserClaims userClaims);

		Task<SubTopicListResponse> GetSubTopics(Guid topicId,AuthenticatedUserClaims userClaims);
		Task<BaseResponse> GetClassroomCurriculum(Guid classroomId, AuthenticatedUserClaims userClaims);
		Task<BaseResponse> GetSubjectCurriculum(Guid subjectId, Guid classroomId, AuthenticatedUserClaims userClaims);
		Task<BaseResponse> GetTopicsWithSubTopics(Guid subjectId, Guid classroomId, AuthenticatedUserClaims claims);
		Task<BaseResponse> AddSubTopicsToTopic(AddSubTopicsViewModel model, AuthenticatedUserClaims userClaims);

		Task<BaseResponse> GetStudentsByClassroom(Guid classroomId, AuthenticatedUserClaims claims);
		Task<BaseResponse> GetStudentsBySubject(Guid subjectId, AuthenticatedUserClaims claims);
		Task<BaseResponse> UpdateTeacherClassroom(Guid teacherId, UpdateTeacherClassroomViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> CreateTopicsWithSubTopics(CreateTopicsWithSubTopicsViewModel model, AuthenticatedUserClaims userClaims);
		Task<BaseResponse> GetSubjectStatsAsync(Guid subjectId, Guid classroomId, AuthenticatedUserClaims userClaims);
		Task<BaseResponse> ProvisionSchool(ProvisionSchoolViewModel model, AuthenticatedUserClaims claims);

		Task<BaseResponse> SubmitRegistrationRequest(SchoolRegistrationRequestViewModel model);
		Task<BaseResponse> GetRegistrationRequests(string? statusFilter, AuthenticatedUserClaims? claims);
		Task<BaseResponse> ApproveRegistrationRequest(Guid requestId, AuthenticatedUserClaims claims);
		Task<BaseResponse> RejectRegistrationRequest(Guid requestId, string reason, AuthenticatedUserClaims claims);
		Task<BaseResponse> GetAllSchoolsWithStatus();
		Task<BaseResponse> GetPendingSchoolIds();
		Task<BaseResponse> GetSchoolApprovalStatus(Guid schoolId);
	}
}
