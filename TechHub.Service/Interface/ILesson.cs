using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;

namespace TechHub.Service.Interface;

public interface ILessonService
{
	Task<BaseResponse> SubmitLesson(SubmitLessonViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetLessonsByClassroom(Guid classroomId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetLessonById(Guid lessonId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetPendingApprovals(AuthenticatedUserClaims claims);
	Task<BaseResponse> RespondToLesson(Guid lessonId, bool approved, string rejectionReason, AuthenticatedUserClaims claims);
}

