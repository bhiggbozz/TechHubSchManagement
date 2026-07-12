using System;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;

namespace TechHub.Service.Interface;

public interface IStudentDashboardService
{
    Task<BaseResponse> GetStudentSummaryAsync(AuthenticatedUserClaims claims);
    Task<BaseResponse> MarkLessonAsWatchedAsync(Guid lessonId, AuthenticatedUserClaims claims);
    Task<BaseResponse> GetStudentSubjectScoresAsync(AuthenticatedUserClaims claims);
    Task<BaseResponse> GetStudentSubTopicScoresAsync(AuthenticatedUserClaims claims);
}
