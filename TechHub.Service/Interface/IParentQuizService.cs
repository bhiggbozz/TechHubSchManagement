using System;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.ParentQuizModels;

namespace TechHub.Service.Interface
{
	/// <summary>
	/// Practice quizzes a parent sets for their own child. Objective questions only (multiple
	/// choice / true-false), so they mark themselves; kept apart from teacher assessments so they
	/// never affect school rankings or performance reports.
	/// </summary>
	public interface IParentQuizService
	{
		// ── Parent ────────────────────────────────────────────────────────────

		/// <summary>The child's subjects and topics with how many usable questions each has, by difficulty.</summary>
		Task<BaseResponse> GetChildSubjectsAsync(Guid studentId, AuthenticatedUserClaims claims);

		/// <summary>The parent's saved defaults (built-in defaults until they save their own).</summary>
		Task<BaseResponse> GetSettingsAsync(AuthenticatedUserClaims claims);

		Task<BaseResponse> SaveSettingsAsync(ParentQuizSettingsViewModel model, AuthenticatedUserClaims claims);

		/// <summary>Builds and assigns a quiz. Only child + subject are required; the rest comes from the saved settings.</summary>
		Task<BaseResponse> CreateAsync(CreateParentQuizViewModel model, AuthenticatedUserClaims claims);

		/// <summary>The parent's quizzes, newest first; optionally for one child.</summary>
		Task<BaseResponse> ListForParentAsync(Guid? studentId, AuthenticatedUserClaims claims);

		/// <summary>Score plus every question with the child's pick and the correct answer. Only once the child has finished.</summary>
		Task<BaseResponse> GetResultForParentAsync(Guid quizId, AuthenticatedUserClaims claims);

		/// <summary>Cancel a quiz the child hasn't started.</summary>
		Task<BaseResponse> CancelAsync(Guid quizId, AuthenticatedUserClaims claims);

		// ── Student ───────────────────────────────────────────────────────────

		/// <summary>Quizzes the child's parents have set (not cancelled), newest first.</summary>
		Task<BaseResponse> ListForStudentAsync(AuthenticatedUserClaims claims);

		/// <summary>Starts the quiz, or resumes it if already started. Returns the questions without answers.</summary>
		Task<BaseResponse> StartAsync(Guid quizId, AuthenticatedUserClaims claims);

		/// <summary>Submits all answers at once, marks them and returns the result.</summary>
		Task<BaseResponse> SubmitAsync(Guid quizId, SubmitParentQuizViewModel model, AuthenticatedUserClaims claims);

		Task<BaseResponse> GetResultForStudentAsync(Guid quizId, AuthenticatedUserClaims claims);
	}
}
