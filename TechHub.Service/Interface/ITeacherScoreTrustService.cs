using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Entities;

namespace TechHub.Service.Interface;

/// <summary>
/// Service for calculating and managing teacher trust scores
/// </summary>
public interface ITeacherTrustScoreService
{
	/// <summary>
	/// Calculate trust score for a teacher at a specific school
	/// 
	/// FORMULA:
	/// TrustScore = ApprovalRate + VolumeBonus - RejectionPenalty
	/// - ApprovalRate = (Approved / Total) * 100
	/// - VolumeBonus = min(TotalSubmitted / 10, 20)
	/// - RejectionPenalty = min(Rejected * 5, 30)
	/// - Final = clamp(TrustScore, 0, 100)
	/// </summary>
	Task<decimal> CalculateTrustScore(Guid teacherId, Guid schoolId);

	/// <summary>
	/// Update teacher trust score after approval or rejection
	/// 
	/// WORKFLOW:
	/// 1. Recalculate trust score
	/// 2. Update metrics (consecutive approvals, streaks, timing)
	/// 3. Update or create TeacherTrustScore record
	/// 4. Record approval/rejection date
	/// </summary>
	Task UpdateAfterApproval(Guid teacherId,Guid schoolId,bool approved,int? approvalTimeMinutes = null);

	/// <summary>
	/// Get teacher trust score record
	/// Returns null if teacher has no approval history
	/// </summary>
	Task<TeacherTrustScore> GetTeacherTrustScore(Guid teacherId, Guid schoolId);
}
