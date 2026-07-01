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
	Task<BaseResponse> GetStudentQuizDisplay(Guid lessonId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetStudentQuizDisplayByCode(string quizCode, AuthenticatedUserClaims claims);
	Task<BaseResponse> StartQuizAttempt(StartQuizViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> SubmitQuizAttempt(Guid attemptId, SubmitQuizViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetQuizResult(Guid attemptId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetQuizAttemptStatusByCode(string quizCode, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetSubjectQuizzes(Guid subjectId, AuthenticatedUserClaims claims);

	// ── Assessments ──────────────────────────────────────────────────────────
	Task<BaseResponse> CreateAssessment(CreateAssessmentViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> ConfigureQuiz(ConfigureQuizViewModel model, AuthenticatedUserClaims claims);

	// ── Grading (teacher) ────────────────────────────────────────────────────
	Task<BaseResponse> GetPendingGrades(AuthenticatedUserClaims claims);
	Task<BaseResponse> GetGradingDetailAsync(AuthenticatedUserClaims claims);
	Task<BaseResponse> GradeAnswer(Guid answerId, GradeAnswerViewModel model, AuthenticatedUserClaims claims);

	// ── Analytics ─────────────────────────────────────────────────────────────
	Task<BaseResponse> GetLessonQuizResults(Guid lessonId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetLessonQuizAnalytics(Guid lessonId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetStudentQuizHistory(Guid studentId, AuthenticatedUserClaims claims);

	// ── Assessment Sets ───────────────────────────────────────────────────────
	Task<BaseResponse> CreateAssessmentSet(CreateAssessmentSetViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> UpdateAssessmentSet(Guid id, UpdateAssessmentSetViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> DeleteAssessmentSet(Guid id, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetAssessmentSets(AuthenticatedUserClaims claims);
	Task<BaseResponse> GetAssessmentSet(Guid id, AuthenticatedUserClaims claims);
	Task<BaseResponse> AttachAssessmentSetToLesson(Guid lessonId, AttachAssessmentSetViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetLessonAssessmentSet(Guid lessonId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetLessonAssessmentConfig(Guid lessonId, AuthenticatedUserClaims claims);
}

