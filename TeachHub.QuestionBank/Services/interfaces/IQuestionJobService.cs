using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.Response;
using TechHub.QuestionBank.Core.ViewModel;

namespace TechHub.QuestionBank.Services.interfaces;

	public interface IQuestionJobService
	{
		/// <summary>
		/// Step 1 — Teacher uploads image
		/// Saves to Cloudinary temp
		/// Logs job as Pending
		/// Returns JobId immediately (~200ms)
		/// </summary>
		Task<SubmitJobResponse> SubmitJob(IFormFile image,SubmitQuestionJobViewModel model,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Step 2 — Teacher polls this
		/// Returns Pending | Processing | Completed | Failed
		/// When Completed → QuestionId is populated
		/// </summary>
		Task<JobStatusResponse> GetJobStatus(
			Guid jobId,
			AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Returns all jobs for this teacher
		/// Lightweight list — no question content
		/// Teacher sees their upload history
		/// </summary>
		Task<JobListResponse> GetMyJobs(AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Called when teacher wants to retry a failed job
		/// Resets Status to Pending
		/// Background worker picks it up again
		/// </summary>
		Task<BaseResponse> RetryJob(Guid jobId,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Step 3 — Teacher fetches processed question
		/// Only available when Status = Completed
		/// Returns HTML + ContentParts for preview and edit
		/// </summary>
		Task<QuestionPreviewResponse> GetQuestionPreview(Guid jobId,AuthenticatedUserClaims userClaims);
		Task<BaseResponse> GetJobStatuses(Guid classroomId, Guid subjectId, Guid? topicId, Guid? subTopicId, AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Called by background worker only
		/// Picks up next Pending job
		/// Calls Claude, saves HTML + options to DB
		/// Updates job Status → Completed or Failed
		/// </summary>
		Task ProcessNextPendingJob();
	}


//public interface ISubTopicService
//{
//    // Subject
//    Task<CreateSubjectResponse> CreateSubject(
//        CreateSubjectViewModel model,
//        AuthenticatedUserClaims userClaims);
 
//    Task<SubjectListResponse> GetSubjects(
//        AuthenticatedUserClaims userClaims);
 
//    // Topic
//    Task<CreateTopicResponse> CreateTopic(
//        CreateTopicViewModel model,
//        AuthenticatedUserClaims userClaims);
 
//    Task<TopicListResponse> GetTopics(
//        Guid subjectId,
//        AuthenticatedUserClaims userClaims);
 
//    // SubTopic
//    Task<CreateSubTopicResponse> CreateSubTopic(
//        CreateSubTopicViewModel model,
//        AuthenticatedUserClaims userClaims);
 
//    Task<SubTopicListResponse> GetSubTopics(
//        Guid topicId,
//        AuthenticatedUserClaims userClaims);
//}
 

