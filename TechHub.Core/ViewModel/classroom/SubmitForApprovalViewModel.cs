using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.classroom;

/// <summary>
/// Submit class preparation for admin approval
/// </summary>
public class SubmitForApprovalViewModel
{
	[Required(ErrorMessage = "Class preparation ID is required")]
	public Guid ClassPreparationId { get; set; }
}


/// <summary>
/// Approve class preparation (Admin only)
/// </summary>
public class ApproveClassViewModel
{
	[Required(ErrorMessage = "Class preparation ID is required")]
	public Guid ClassPreparationId { get; set; }

	[MaxLength(500)]
	public string? ApprovalNotes { get; set; }
}

/// <summary>
/// Reject class preparation (Admin only)
/// </summary>
public class RejectClassViewModel
{
	[Required(ErrorMessage = "Class preparation ID is required")]
	public Guid ClassPreparationId { get; set; }

	[Required(ErrorMessage = "Rejection reason is required")]
	[MinLength(10, ErrorMessage = "Rejection reason must be at least 10 characters")]
	[MaxLength(500, ErrorMessage = "Rejection reason cannot exceed 500 characters")]
	public string RejectionReason { get; set; } = string.Empty;
}

/// <summary>
/// Delete media file
/// </summary>
public class DeleteMediaViewModel
{
	[Required]
	public Guid MediaId { get; set; }

	[MaxLength(500)]
	public string? Reason { get; set; }
}
