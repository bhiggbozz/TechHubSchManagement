using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.classroom;

namespace TechHub.Service.Interface;

public interface IAdminDashboardService
{
    Task<BaseResponse> GetDashboardAsync(AuthenticatedUserClaims claims);
    Task<BaseResponse> GetTeacherActivityAsync(AuthenticatedUserClaims claims, Guid? teacherId = null);
    Task<BaseResponse> GetClassroomPerformanceAsync(AuthenticatedUserClaims claims, Guid? classroomId = null);
    Task<BaseResponse> GetSubjectPerformanceAsync(AuthenticatedUserClaims claims, Guid? subjectId = null);
    Task AggregateSchoolAsync(Guid schoolId);
    Task AggregateAllSchoolsAsync();
}
