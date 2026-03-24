using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.Response;
using TechHub.QuestionBank.Core.ViewModel;

namespace TechHub.QuestionBank.Services.interfaces;
public interface IQuestionSyncService
{
	/// <summary>
	/// Sync batch of offline-created questions
	/// Returns server IDs mapped to client IDs
	/// Frontend uses this to clean local storage
	/// </summary>
	Task<SyncResponse> SyncQuestions(SyncQuestionsViewModel model,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Detect conflicts between local and server versions
	/// </summary>
	Task<ConflictCheckResponse> CheckForConflicts(ConflictCheckViewModel model,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Resolve a sync conflict
	/// Teacher chooses local or server version
	/// </summary>
	Task<BaseResponse> ResolveConflict(ResolveConflictViewModel model,AuthenticatedUserClaims userClaims);
}
