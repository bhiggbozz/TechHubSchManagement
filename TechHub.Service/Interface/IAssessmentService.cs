using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.classroom;

namespace TechHub.Service.Interface;

public interface IAssessmentService
{
	Task<BaseResponse> CreateAssessment(CreateAssessmentViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> AssignAssessment(AssignAssessmentViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetStudentAssessments(AuthenticatedUserClaims claims);
	Task<BaseResponse> GetStudentAssessmentScores(AuthenticatedUserClaims claims);
	Task<BaseResponse> GetAssessmentDetail(Guid assessmentId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetAssessmentDetailByCode(string code, AuthenticatedUserClaims claims);
	Task<BaseResponse> StartAttempt(Guid assessmentId, AuthenticatedUserClaims claims);
	Task<BaseResponse> SubmitAnswer(SubmitAssessmentAnswerViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> SubmitAttempt(Guid attemptId, AuthenticatedUserClaims claims);
	Task<BaseResponse> SubmitAllAnswers(SubmitAssessmentBatchViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetResult(Guid attemptId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetAttemptHistory(Guid assessmentId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetAssignedAssessments(AuthenticatedUserClaims claims);
	Task<BaseResponse> GetTeacherAssessments(AuthenticatedUserClaims claims);
	Task<BaseResponse> GetAssessmentAssignments(Guid assessmentId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetPendingGrading(AuthenticatedUserClaims claims);
	Task<BaseResponse> GradeAnswer(Guid answerId, GradeAssessmentAnswerViewModel model, AuthenticatedUserClaims claims);
}
