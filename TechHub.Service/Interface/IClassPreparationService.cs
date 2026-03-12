// Services/Interface/IClassPreparationService.cs

using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel.classroom;

namespace TechHub.Service.Interface
{
	/// <summary>
	/// Service for managing class preparations (create, submit, approve, reject)
	/// </summary>
	public interface IClassPreparationService
	{
		/// <summary>
		/// Save class preparation as draft (create new or update existing)
		/// Teacher can save multiple times before submitting
		/// </summary>
		Task<ClassPreparationResponse> SaveClassPreparation(SaveClassPreparationViewModel model,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Submit class preparation for admin approval
		/// Changes status from Draft/Rejected → Pending
		/// </summary>
		Task<BaseResponse> SubmitForApproval(SubmitForApprovalViewModel model,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Approve class preparation (Admin only)
		/// Changes status → Approved
		/// Moves media to permanent storage
		/// </summary>
		Task<BaseResponse> ApproveClass(ApproveClassViewModel model,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Reject class preparation (Admin only)
		/// Changes status → Rejected
		/// Deletes all associated media
		/// </summary>
		Task<BaseResponse> RejectClass(RejectClassViewModel model,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Get class preparation by ID with full details
		/// </summary>
		Task<ClassPreparationResponse> GetClassPreparationById(Guid id,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Get teacher's class preparations (their own classes)
		/// </summary>
		Task<ClassPreparationsListResponse> GetMyClassPreparations(GetClassPreparationsQuery query,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Get pending approvals (Admin only)
		/// Returns all classes with Status = Pending
		/// </summary>
		Task<ClassPreparationsListResponse> GetPendingApprovals(GetClassPreparationsQuery query,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Delete draft class preparation
		/// Only draft classes can be deleted
		/// Also deletes associated media
		/// </summary>
		Task<BaseResponse> DeleteDraft(Guid id,AuthenticatedUserClaims userClaims);
	}
}