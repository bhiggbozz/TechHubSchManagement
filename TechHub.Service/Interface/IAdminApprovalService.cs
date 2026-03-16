using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;

namespace TechHub.Service.Interface;

	/// <summary>
	/// Service for admin approval workflows
	/// 
	/// RESPONSIBILITIES:
	/// - Get pending approvals with smart sorting
	/// - Provide quick preview data for admin review
	/// - Handle bulk approval operations
	/// - Retrieve AI content analysis
	/// 
	/// FEATURES:
	/// - Smart prioritization (urgent, needs review, routine)
	/// - Teacher trust score integration
	/// - Lightweight previews (thumbnails, AI analysis)
	/// - Bulk operations for efficiency
	/// </summary>
	public interface IAdminApprovalService
	{
		/// <summary>
		/// Get pending approvals with smart sorting
		/// 
		/// RETURNS:
		/// - Urgent: Classes starting < 24 hours
		/// - NeedsReview: New teachers, large files, AI flags
		/// - Routine: Trusted teachers, normal files
		/// 
		/// SORTING OPTIONS:
		/// - priority: Smart sort (urgent → needs review → routine)
		/// - date: Submission date (oldest first)
		/// - teacher: Alphabetical by teacher name
		/// </summary>
		Task<PendingApprovalsResponse> GetPendingApprovals(string sortBy,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Get quick preview data for a class (lightweight)
		/// 
		/// INCLUDES:
		/// - Class metadata (topic, objectives, schedule)
		/// - Teacher trust score and approval history
		/// - Media thumbnails and preview URLs (no full downloads)
		/// - AI analysis results (content flags, quality)
		/// - Key moments in videos
		/// 
		/// OPTIMIZED FOR:
		/// - Fast loading (< 1 second)
		/// - Minimal bandwidth (thumbnails only)
		/// - Admin decision-making
		/// </summary>
		Task<QuickPreviewResponse> GetQuickPreview(Guid classPreparationId,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Bulk approve multiple classes
		/// 
		/// VALIDATION:
		/// - All classes must be Pending
		/// - Admin must have ApproveClasses permission
		/// - Classes must pass eligibility checks
		/// 
		/// WORKFLOW:
		/// - Approve each class individually
		/// - Track success/failure per class
		/// - Update trust scores
		/// - Return summary
		/// </summary>
		Task<BulkApprovalResponse> BulkApprove(BulkApprovalViewModel model,AuthenticatedUserClaims userClaims);

		/// <summary>
		/// Get AI content analysis for a media file
		/// </summary>
		Task<ContentAnalysisResponse> GetContentAnalysis(Guid mediaId);
	}


