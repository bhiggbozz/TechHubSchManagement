using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.Response;
using TechHub.QuestionBank.Core.ViewModel;

namespace TechHub.QuestionBank.Services.interfaces;

public interface IQuestionService
{
	/// <summary>
	/// Create a new question
	/// Accepts offline-created questions with ClientId
	/// for conflict detection on sync
	/// </summary>
	Task<CreateQuestionResponse> CreateQuestion(CreateQuestionViewModel model, AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Batch create multiple questions in one request
	/// Partial success supported — returns per-item results
	/// </summary>
	Task<CreateQuestionsBatchResponse> CreateQuestionsBatch(CreateQuestionsBatchViewModel model, AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Update an existing question
	/// Handles dirty state detection
	/// </summary>
	Task<UpdateQuestionResponse> UpdateQuestion(UpdateQuestionViewModel model, AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Get question by Id
	/// </summary>
	Task<QuestionDetailResponse> GetQuestion(Guid questionId,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Get all questions for a subject
	/// Supports pagination for memory efficiency
	/// </summary>
	Task<QuestionListResponse> GetSubjectQuestions(Guid subjectId,QuestionFilterViewModel filter,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Soft delete question
	/// </summary>
	Task<BaseResponse> DeleteQuestion(Guid questionId,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Publish draft question
	/// </summary>
	Task<BaseResponse> PublishQuestion(Guid questionId,AuthenticatedUserClaims userClaims);
	// Add these three methods to IQuestionService

	/// <summary>
	/// Confirm an AI extracted question
	/// Moves status PendingReview → Draft
	/// </summary>
	Task<BaseResponse> ConfirmQuestion(Guid questionId,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Reject an AI extracted question
	/// Soft deletes the question
	/// </summary>
	Task<BaseResponse> RejectQuestion(Guid questionId,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Get all pending review questions for a scan session
	/// Returns alongside original file url for side by side view
	/// </summary>
	Task<PendingReviewResponse> GetPendingReviewQuestions(Guid scanSessionId,AuthenticatedUserClaims userClaims);
	Task<QuestionListResponse> GetQuestionsByClassroom(Guid classroomId, QuestionFilterViewModelV2 filter, AuthenticatedUserClaims userClaims);
	Task<QuestionListResponse> GetQuestionsBySubTopic(Guid subTopicId, QuestionFilterViewModelV2 filter, AuthenticatedUserClaims userClaims);
	Task<QuestionListResponse> GetQuestionsByClassroomAndSubject(Guid classroomId, Guid subjectId, QuestionFilterViewModelV2 filter, AuthenticatedUserClaims userClaims);
	Task<QuestionListResponse> GetQuestionsByClassroomAndSubTopic(Guid classroomId, Guid subTopicId, QuestionFilterViewModelV2 filter, AuthenticatedUserClaims userClaims);
	Task<QuestionListResponse> GetQuestionsBySubjectAndSubTopic(Guid subjectId, Guid subTopicId, QuestionFilterViewModelV2 filter, AuthenticatedUserClaims userClaims);
	Task<QuestionListResponse> GetQuestionsByClassroomSubjectAndSubTopic(Guid classroomId, Guid subjectId, Guid subTopicId, QuestionFilterViewModelV2 filter, AuthenticatedUserClaims userClaims);
	Task<BaseResponse> GetSubjectQuestionSummary(Guid classroomId, Guid subjectId, AuthenticatedUserClaims userClaims);

	Task<QuestionListResponse> GetQuestionsByClassroomSubjectTopic(Guid classroomId,Guid subjectId,Guid topicId,QuestionFilterViewModelV2 filter,AuthenticatedUserClaims userClaims);


}

