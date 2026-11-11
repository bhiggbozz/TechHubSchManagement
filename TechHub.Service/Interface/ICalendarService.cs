using System;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Calendar;

namespace TechHub.Service.Interface
{
	public interface ICalendarService
	{
		/// <summary>Preset starting points (three-term, British, two-semester, custom). Names only — no dates.</summary>
		BaseResponse GetTemplates();

		/// <summary>All of the caller's school's sessions (newest first), each with its terms.</summary>
		Task<BaseResponse> GetSessionsAsync(AuthenticatedUserClaims claims);

		/// <summary>
		/// One session with its terms. With no id: the session containing today, else the
		/// next upcoming one, else the most recent past one.
		/// </summary>
		Task<BaseResponse> GetSessionAsync(Guid? sessionId, AuthenticatedUserClaims claims);

		Task<BaseResponse> CreateSessionAsync(CreateAcademicSessionViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> UpdateSessionAsync(Guid sessionId, UpdateAcademicSessionViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> DeleteSessionAsync(Guid sessionId, AuthenticatedUserClaims claims);

		/// <summary>
		/// Stamps the academic session/term on this school's existing quizzes, assessments and attempts
		/// that were created before the calendar existed (matched by their WAT date). Safe to run again:
		/// only rows still missing a session/term are touched, and nothing already stamped is changed.
		/// </summary>
		Task<BaseResponse> BackfillRecordsAsync(AuthenticatedUserClaims claims);

		Task<BaseResponse> AddTermAsync(Guid sessionId, TermInputViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> UpdateTermAsync(Guid termId, TermInputViewModel model, AuthenticatedUserClaims claims);
		Task<BaseResponse> DeleteTermAsync(Guid termId, AuthenticatedUserClaims claims);
	}
}
