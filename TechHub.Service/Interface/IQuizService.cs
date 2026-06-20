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
	// ── Existing ─────────────────────────────────────────────────────────────
	Task<BaseResponse> CreateQuiz(CreateQuizViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> AttachQuizToLesson(Guid lessonId, AttachQuizViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetQuizByLesson(Guid lessonId, AuthenticatedUserClaims claims);

	// ── Quiz Config ──────────────────────────────────────────────────────────
	Task<BaseResponse> SaveQuizConfig(QuizConfigViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetQuizConfig(AuthenticatedUserClaims claims);

	// ── Attempt (student) ────────────────────────────────────────────────────
	Task<BaseResponse> StartQuizAttempt(StartQuizViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> SubmitQuizAttempt(Guid attemptId, SubmitQuizViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetQuizResult(Guid attemptId, AuthenticatedUserClaims claims);

	// ── Grading (teacher) ────────────────────────────────────────────────────
	Task<BaseResponse> GetPendingGrades(AuthenticatedUserClaims claims);
	Task<BaseResponse> GradeAnswer(Guid answerId, GradeAnswerViewModel model, AuthenticatedUserClaims claims);

	// ── Analytics ─────────────────────────────────────────────────────────────
	Task<BaseResponse> GetLessonQuizResults(Guid lessonId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetLessonQuizAnalytics(Guid lessonId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetStudentQuizHistory(Guid studentId, AuthenticatedUserClaims claims);
}

