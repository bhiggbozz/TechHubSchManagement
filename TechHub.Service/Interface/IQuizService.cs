using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.classroom;

namespace TechHub.Service.Interface;

public interface IQuizService
{
	Task<BaseResponse> CreateQuiz(CreateQuizViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> AttachQuizToLesson(Guid lessonId, AttachQuizViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetQuizByLesson(Guid lessonId, AuthenticatedUserClaims claims);
}

