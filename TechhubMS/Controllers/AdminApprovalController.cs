using global::TechHub.Core.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using TechHub.Service.Extension;
using TechHub.Service.Interface;
using TechhubMS.util;
namespace TechhubMS.Controllers;




	/// <summary>
	/// Admin approval workflow controller
	/// 
	/// FEATURES:
	/// - Smart pending approvals list (urgent, needs review, routine)
	/// - Quick preview (thumbnails, AI analysis, trust scores)
	/// - Bulk approval for efficiency
	/// - AI content analysis retrieval
	/// 
	/// AUTHORIZATION:
	/// - Administrators and SuperAdministrators only
	/// - ApproveClasses permission required for Administrators
	/// </summary>
	[ApiController]
	[Route("api/admin")]
	[Authorize(Roles = "Administrator,SuperAdministrator")]
	public class AdminApprovalController : ControllerBase
	{
		private readonly IAdminApprovalService _adminApprovalService;

		public AdminApprovalController(IAdminApprovalService adminApprovalService)
		{
			_adminApprovalService = adminApprovalService;
		}

		/// <summary>
		/// Get pending approvals with smart sorting
		/// 
		/// RETURNS:
		/// - Urgent: Classes starting < 24 hours
		/// - NeedsReview: New teachers, low trust scores
		/// - Routine: Trusted teachers, normal files
		/// 
		/// QUERY PARAMS:
		/// - sortBy: "priority" (default), "date", "teacher"
		/// </summary>
		[HttpGet("pending-approvals")]
		public async Task<IActionResult> GetPendingApprovals([FromQuery] string sortBy = "priority")
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _adminApprovalService.GetPendingApprovals(sortBy, userClaims);
			return Ok(result);
		}

		/// <summary>
		/// Get quick preview for a class (lightweight)
		/// 
		/// INCLUDES:
		/// - Class metadata
		/// - Teacher trust score and history
		/// - Media thumbnails (no full downloads)
		/// - AI analysis results
		/// - Key moments in videos
		/// 
		/// OPTIMIZED FOR:
		/// - Fast loading (< 1 second)
		/// - Minimal bandwidth (thumbnails only)
		/// </summary>
		[HttpGet("quick-preview/{classId}")]
		public async Task<IActionResult> GetQuickPreview(Guid classId)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _adminApprovalService.GetQuickPreview(classId, userClaims);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				_ => StatusCode(StatusCodes.Status500InternalServerError, result)
			};
		}

		/// <summary>
		/// Bulk approve multiple classes
		/// 
		/// USE CASES:
		/// - Approve 20+ routine classes at once
		/// - End-of-week bulk processing
		/// - Clearing backlog
		/// 
		/// VALIDATION:
		/// - All classes must be Pending
		/// - Admin must have permission
		/// - Individual failures don't block others
		/// </summary>
		[HttpPost("bulk-approve")]
		public async Task<IActionResult> BulkApprove([FromBody] BulkApprovalViewModel model)
		{
			var userClaims = User.GetAuthenticatedUserClaims();
			var result = await _adminApprovalService.BulkApprove(model, userClaims);
			return Ok(result);
		}

		/// <summary>
		/// Get AI content analysis for a media file
		/// 
		/// RETURNS:
		/// - Content flags (inappropriate, educational)
		/// - Quality metrics
		/// - Analysis status
		/// </summary>
		[HttpGet("content-analysis/{mediaId}")]
		public async Task<IActionResult> GetContentAnalysis(Guid mediaId)
		{
			var result = await _adminApprovalService.GetContentAnalysis(mediaId);

			return result.ResponseCode switch
			{
				ResponseCode.successful => Ok(result),
				ResponseCode.NotFound => NotFound(result),
				_ => StatusCode(StatusCodes.Status500InternalServerError, result)
			};
		}
	}


