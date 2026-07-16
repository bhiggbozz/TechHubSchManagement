using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.classroom;

namespace TechHub.Service.Interface;

public interface IPerformanceDashboardService
{
    Task<BaseResponse> GetNavbarAsync(AuthenticatedUserClaims claims);
    Task<BaseResponse> GetDashboardAsync(AuthenticatedUserClaims claims);
    Task<BaseResponse> GetClassroomDetailAsync(Guid classroomId, AuthenticatedUserClaims claims);
    Task<BaseResponse> GetSubjectDetailAsync(Guid subjectId, AuthenticatedUserClaims claims);
    Task<BaseResponse> GetSubjectClassroomsAsync(Guid subjectId, AuthenticatedUserClaims claims);
    Task<BaseResponse> GetSubjectTopicsAsync(Guid subjectId, Guid? classroomId, AuthenticatedUserClaims claims);
    Task<BaseResponse> GetStudentQuizPerformanceAsync(Guid studentId, AuthenticatedUserClaims claims);
}
