using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.DTO;

/// <summary>
/// Teacher's class preparation summary
/// </summary>
public class TeacherClassSummaryDto
{
	public int TotalClasses { get; set; }
	public int DraftClasses { get; set; }
	public int PendingClasses { get; set; }
	public int ApprovedClasses { get; set; }
	public int RejectedClasses { get; set; }
	public int CompletedClasses { get; set; }

	public int TotalMediaFiles { get; set; }
	public long TotalMediaSizeBytes { get; set; }
	public string TotalMediaSizeFormatted { get; set; } = string.Empty;

	public double AverageApprovalTimeHours { get; set; }
	public double ApprovalRate { get; set; }  // Percentage
}

/// <summary>
/// Admin approval queue summary
/// </summary>
public class ApprovalQueueSummaryDto
{
	public int PendingCount { get; set; }
	public int TodaySubmittedCount { get; set; }
	public int WeekSubmittedCount { get; set; }

	public List<ClassPreparationDto> RecentPending { get; set; } = new();
	public List<ClassPreparationDto> HighPriorityPending { get; set; } = new();

	public double AverageProcessingTimeHours { get; set; }
}
