using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

/// <summary>
/// Teacher performance tracking model
/// 
/// PURPOSE:
/// - Track teacher approval history and quality metrics
/// - Calculate trust scores for intelligent approval workflows
/// - Enable fast-track approvals for trusted teachers
/// 
/// BUSINESS RULES:
/// - One record per teacher per school
/// - Trust score: 0-100 (higher = more trusted)
/// - Recalculated after each approval/rejection
/// - Used for admin dashboard prioritization
/// 
/// CALCULATION:
/// TrustScore = (Approved/Total)*100 + VolumeBonus - RejectionPenalty
/// - Base score: Approval rate percentage
/// - Volume bonus: Up to +20 points for high volume (1 point per 10 classes)
/// - Rejection penalty: -5 points per rejection (max -30)
/// - Final score clamped to 0-100 range
/// 
/// TRUST LEVELS:
/// - 95-100: Excellent (auto-approval eligible)
/// - 80-94:  Good (fast-track review)
/// - 60-79:  Fair (normal review)
/// - 0-59:   Needs improvement (detailed review)
/// </summary>
public class TeacherTrustScore
{
	/// <summary>Primary key</summary>
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>Foreign key to Users table (the teacher)</summary>
	public Guid TeacherId { get; set; }

	/// <summary>Foreign key to School table (allows per-school tracking)</summary>
	public Guid SchoolId { get; set; }

	/// <summary>
	/// Calculated trust score: 0.00 to 100.00
	/// Higher score = more trusted teacher
	/// </summary>
	public decimal TrustScore { get; set; } = 0.00m;

	/// <summary>Total number of classes submitted for approval</summary>
	public int TotalClassesSubmitted { get; set; } = 0;

	/// <summary>Number of classes approved</summary>
	public int TotalClassesApproved { get; set; } = 0;

	/// <summary>Number of classes rejected</summary>
	public int TotalClassesRejected { get; set; } = 0;

	/// <summary>
	/// Average time (minutes) admins take to approve this teacher's classes
	/// Lower value indicates trusted teacher (admins review quickly)
	/// </summary>
	public int AverageApprovalTimeMinutes { get; set; } = 0;

	/// <summary>Fastest approval time recorded (minutes)</summary>
	public int? FastestApprovalMinutes { get; set; }

	/// <summary>Slowest approval time recorded (minutes)</summary>
	public int? SlowestApprovalMinutes { get; set; }

	/// <summary>
	/// Current consecutive approvals (resets to 0 on rejection)
	/// High streak indicates consistent quality
	/// </summary>
	public int ConsecutiveApprovals { get; set; } = 0;

	/// <summary>Best approval streak ever achieved (historical high)</summary>
	public int BestApprovalStreak { get; set; } = 0;

	/// <summary>Last date teacher had a class rejected</summary>
	public DateTime? LastRejectionDate { get; set; }

	/// <summary>Last date teacher had a class approved</summary>
	public DateTime? LastApprovalDate { get; set; }

	/// <summary>When trust score was last calculated</summary>
	public DateTime LastCalculatedDate { get; set; } = DateTime.UtcNow;

	/// <summary>Record creation timestamp</summary>
	public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

	/// <summary>Last modification timestamp</summary>
	public string? ModifiedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}

		// Navigation properties (if using EF Core)
		// public virtual User Teacher { get; set; }
		// public virtual School School { get; set; }

