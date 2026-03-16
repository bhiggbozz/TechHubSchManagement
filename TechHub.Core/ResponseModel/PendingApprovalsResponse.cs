using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.DTO;
using TechHub.Core.ViewModel;

namespace TechHub.Core.ResponseModel;

/// <summary>
/// Pending approvals organized by priority
/// </summary>
public class PendingApprovalsResponse : BaseResponse
{
	public List<ClassPreparationDto> Urgent { get; set; }
	public List<ClassPreparationDto> NeedsReview { get; set; }
	public List<ClassPreparationDto> Routine { get; set; }
	public int TotalCount { get; set; }
}

/// <summary>
/// Quick preview response (lightweight)
/// </summary>
public class QuickPreviewResponse : BaseResponse
{
	public ClassPreparationDto ClassInfo { get; set; }
	public TeacherTrustInfo Teacher { get; set; }
	public List<MediaPreviewDto> MediaFiles { get; set; }
}

