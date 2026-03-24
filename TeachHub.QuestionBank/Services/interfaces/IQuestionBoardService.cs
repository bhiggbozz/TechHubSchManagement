using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.QuestionBank.Core.ViewModel;

namespace TechHub.QuestionBank.Services.interfaces;

public interface IQuestionBoardService
{
	/// <summary>
	/// Attach a board session to a question
	/// Frontend has already:
	/// → Saved strokes to MongoDB
	/// → Generated PNG snapshot
	/// → Uploaded PNG to CDN
	/// Backend only links the references
	/// </summary>
	Task<AttachBoardSessionResponse> AttachBoardSession(AttachBoardSessionViewModel model,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Remove board session from question
	/// Clears BoardSessionId and SnapshotUrl
	/// Triggers CDN snapshot deletion
	/// MongoDB strokes soft deleted via board system
	/// </summary>
	Task<BaseResponse> DetachBoardSession(DetachBoardSessionViewModel model,AuthenticatedUserClaims userClaims);

	/// <summary>
	/// Get board session reference for a question
	/// Returns BoardSessionId for frontend to
	/// load strokes from MongoDB for editing
	/// Does not return strokes directly
	/// </summary>
	Task<BoardSessionResponse> GetBoardSession(Guid questionId,AuthenticatedUserClaims userClaims);
}
