using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Service.Interface;

namespace TechHub.Service.Service;

/// <summary>
/// Teacher trust score service implementation
/// 
/// RESPONSIBILITIES:
/// - Calculate trust scores based on approval history
/// - Track performance metrics (timing, streaks, quality)
/// - Maintain TeacherTrustScore table
/// 
/// BUSINESS RULES:
/// - One record per teacher per school
/// - Score recalculated after each approval/rejection
/// - Consecutive streak resets to 0 on rejection
/// - Average approval time weighted by recent approvals
/// 
/// DATA MODEL:
/// - Uses TeacherTrustScore table (separate from Users)
/// - Allows per-school tracking (teacher may work at multiple schools)
/// - Historical metrics preserved (best streak, fastest/slowest approval)
/// 
/// TRUST SCORE FORMULA:
/// TrustScore = ApprovalRate + VolumeBonus - RejectionPenalty
/// WHERE:
/// - ApprovalRate = (Approved / Total) * 100
/// - VolumeBonus = min(TotalSubmitted / 10, 20)  // Max +20 points
/// - RejectionPenalty = min(Rejected * 5, 30)    // Max -30 points
/// - Final = clamp(TrustScore, 0, 100)
/// 
/// TRUST LEVELS:
/// - 95-100: Excellent (auto-approval eligible)
/// - 80-94:  Good (fast-track review)
/// - 60-79:  Fair (normal review)
/// - 0-59:   Needs improvement (detailed review)
/// </summary>
public class TeacherTrustScoreService : ITeacherTrustScoreService
{
	private readonly IQueryRepository<ClassPreparation> _classQueryRepo;
	private readonly IQueryRepository<TeacherTrustScore> _trustScoreQueryRepo;
	private readonly ICommandRespository<TeacherTrustScore> _trustScoreCommandRepo;
	private readonly ILogger _logger;

	public TeacherTrustScoreService(
		IQueryRepository<ClassPreparation> classQueryRepo,
		IQueryRepository<TeacherTrustScore> trustScoreQueryRepo,
		ICommandRespository<TeacherTrustScore> trustScoreCommandRepo,
		ILogger logger)
	{
		_classQueryRepo = classQueryRepo;
		_trustScoreQueryRepo = trustScoreQueryRepo;
		_trustScoreCommandRepo = trustScoreCommandRepo;
		_logger = logger;
	}

	/// <summary>
	/// Calculate trust score for a teacher at a specific school
	/// 
	/// FORMULA:
	/// TrustScore = ApprovalRate + VolumeBonus - RejectionPenalty
	/// 
	/// WHERE:
	/// - ApprovalRate = (Approved / Total) * 100
	/// - VolumeBonus = min(TotalSubmitted / 10, 20)  // Max +20 points
	/// - RejectionPenalty = min(Rejected * 5, 30)    // Max -30 points
	/// - Final = clamp(TrustScore, 0, 100)
	/// 
	/// EXAMPLES:
	/// 
	/// New Teacher (1 class, approved):
	/// - ApprovalRate: 100%
	/// - VolumeBonus: 0.1 (1/10)
	/// - Penalty: 0
	/// - Score: 100.1 → 100 (clamped)
	/// 
	/// Experienced Teacher (50 classes, 48 approved, 2 rejected):
	/// - ApprovalRate: 96%
	/// - VolumeBonus: 5 (50/10, capped at 20)
	/// - Penalty: 10 (2*5)
	/// - Score: 96 + 5 - 10 = 91
	/// 
	/// High Volume Teacher (100 classes, 95 approved, 5 rejected):
	/// - ApprovalRate: 95%
	/// - VolumeBonus: 10 (100/10, capped at 20)
	/// - Penalty: 25 (5*5)
	/// - Score: 95 + 10 - 25 = 80
	/// 
	/// Poor Performer (10 classes, 5 approved, 5 rejected):
	/// - ApprovalRate: 50%
	/// - VolumeBonus: 1 (10/10)
	/// - Penalty: 25 (5*5)
	/// - Score: 50 + 1 - 25 = 26
	/// </summary>
	public async Task<decimal> CalculateTrustScore(Guid teacherId, Guid schoolId)
	{
		try
		{
			_logger.Debug(
				"Calculating trust score - TeacherId: {TeacherId}, SchoolId: {SchoolId}",
				teacherId,
				schoolId);

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// STEP 1: QUERY APPROVAL HISTORY
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// Get all classes that have been approved or rejected
			// Status: 2 (Approved) or 3 (Rejected)

			var query = $@"
                    SELECT 
                        COUNT(*) as TotalSubmitted,
                        SUM(CASE WHEN Status = {(int)ClassPreparationStatus.Approved} THEN 1 ELSE 0 END) as TotalApproved,
                        SUM(CASE WHEN Status = {(int)ClassPreparationStatus.Rejected} THEN 1 ELSE 0 END) as TotalRejected
                    FROM ClassPreparation
                    WHERE TeacherId = '{teacherId}'
                    AND SchoolId = '{schoolId}'
                    AND Status IN ({(int)ClassPreparationStatus.Approved}, {(int)ClassPreparationStatus.Rejected})
                    AND IsActive = 1";

			var results = await _classQueryRepo.GetByQuery(query);
			var result = results.FirstOrDefault();

			if (result == null)
			{
				_logger.Debug("No approval history found - returning score 0");
				return 0;
			}

			// Extract counts using reflection (Dapper dynamic result)
			var totalSubmittedProp = result.GetType().GetProperty("TotalSubmitted");
			var totalApprovedProp = result.GetType().GetProperty("TotalApproved");
			var totalRejectedProp = result.GetType().GetProperty("TotalRejected");

			var totalSubmitted = Convert.ToInt32(totalSubmittedProp?.GetValue(result) ?? 0);
			var totalApproved = Convert.ToInt32(totalApprovedProp?.GetValue(result) ?? 0);
			var totalRejected = Convert.ToInt32(totalRejectedProp?.GetValue(result) ?? 0);

			if (totalSubmitted == 0)
			{
				_logger.Debug("No classes submitted - returning score 0");
				return 0;
			}

			_logger.Debug(
				"Approval history - Total: {Total}, Approved: {Approved}, Rejected: {Rejected}",
				totalSubmitted,
				totalApproved,
				totalRejected);

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// STEP 2: CALCULATE APPROVAL RATE
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			decimal approvalRate = (decimal)totalApproved / totalSubmitted * 100;

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// STEP 3: CALCULATE VOLUME BONUS
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// Reward high-volume teachers
			// 1 point per 10 classes, capped at 20 points

			decimal volumeBonus = Math.Min(totalSubmitted / 10m, 20);

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// STEP 4: CALCULATE REJECTION PENALTY
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// Penalize rejections
			// 5 points per rejection, capped at 30 points

			decimal rejectionPenalty = Math.Min(totalRejected * 5m, 30);

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// STEP 5: CALCULATE FINAL SCORE
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			decimal trustScore = approvalRate + volumeBonus - rejectionPenalty;

			// Clamp to 0-100 range
			trustScore = Math.Max(0, Math.Min(100, trustScore));

			_logger.Information(
				"Trust score calculated - TeacherId: {TeacherId}, Score: {Score} (ApprovalRate: {Rate}%, VolumeBonus: {Bonus}, Penalty: {Penalty})",
				teacherId,
				trustScore,
				approvalRate,
				volumeBonus,
				rejectionPenalty);

			return trustScore;
		}
		catch (Exception ex)
		{
			_logger.Error(
				ex,
				"Error calculating trust score - TeacherId: {TeacherId}, SchoolId: {SchoolId}",
				teacherId,
				schoolId);

			// Return 0 on error (safe default)
			return 0;
		}
	}

	/// <summary>
	/// Update teacher trust score after approval or rejection
	/// 
	/// WORKFLOW:
	/// 1. Get existing TeacherTrustScore record (or create new)
	/// 2. Recalculate trust score
	/// 3. Update metrics (counts, streaks, timing)
	/// 4. Save to database
	/// 
	/// METRICS UPDATED:
	/// - TrustScore: Recalculated from approval history
	/// - TotalClassesApproved/Rejected: Incremented
	/// - ConsecutiveApprovals: Incremented if approved, reset to 0 if rejected
	/// - BestApprovalStreak: Updated if current streak beats record
	/// - AverageApprovalTimeMinutes: Recalculated weighted average
	/// - FastestApprovalMinutes: Updated if this approval is faster
	/// - SlowestApprovalMinutes: Updated if this approval is slower
	/// - LastApprovalDate/LastRejectionDate: Current timestamp
	/// 
	/// ERROR HANDLING:
	/// - If update fails, logs error but doesn't throw
	/// - Approval/rejection still succeeds even if trust score update fails
	/// - Trust score is nice-to-have, not critical for core workflow
	/// </summary>
	public async Task UpdateAfterApproval(
		Guid teacherId,
		Guid schoolId,
		bool approved,
		int? approvalTimeMinutes = null)
	{
		try
		{
			_logger.Information(
				"Updating trust score - TeacherId: {TeacherId}, SchoolId: {SchoolId}, Approved: {Approved}",
				teacherId,
				schoolId,
				approved);

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// STEP 1: GET OR CREATE TRUST SCORE RECORD
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			var existingScore = await GetTeacherTrustScore(teacherId, schoolId);
			var isNewRecord = existingScore == null;

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// STEP 2: RECALCULATE TRUST SCORE
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			var newTrustScore = await CalculateTrustScore(teacherId, schoolId);

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// STEP 3: UPDATE METRICS
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			if (isNewRecord)
			{
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// CREATE NEW RECORD
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var newRecord = new TeacherTrustScore
				{
					Id = Guid.NewGuid(),
					TeacherId = teacherId,
					SchoolId = schoolId,
					TrustScore = newTrustScore,
					TotalClassesSubmitted = 1,
					TotalClassesApproved = approved ? 1 : 0,
					TotalClassesRejected = approved ? 0 : 1,
					ConsecutiveApprovals = approved ? 1 : 0,
					BestApprovalStreak = approved ? 1 : 0,
					AverageApprovalTimeMinutes = approvalTimeMinutes ?? 0,
					FastestApprovalMinutes = approvalTimeMinutes,
					SlowestApprovalMinutes = approvalTimeMinutes,
					LastApprovalDate = approved ? DateTime.UtcNow : (DateTime?)null,
					LastRejectionDate = approved ? (DateTime?)null : DateTime.UtcNow,
					LastCalculatedDate = DateTime.UtcNow,
					CreationDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
					ModifiedDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
				};

				await _trustScoreCommandRepo.Create(newRecord);

				_logger.Information(
					"✅ Created new trust score record - TeacherId: {TeacherId}, Score: {Score}",
					teacherId,
					newTrustScore);
			}
			else
			{
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
				// UPDATE EXISTING RECORD
				// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

				var consecutiveApprovals = approved
					? existingScore.ConsecutiveApprovals + 1
					: 0;

				var bestStreak = Math.Max(existingScore.BestApprovalStreak, consecutiveApprovals);

				// Recalculate average approval time (weighted)
				var totalApprovals = existingScore.TotalClassesApproved + (approved ? 1 : 0);
				var newAverage = totalApprovals > 0
					? ((existingScore.AverageApprovalTimeMinutes * existingScore.TotalClassesApproved) + (approvalTimeMinutes ?? 0)) / totalApprovals
					: 0;

				var updateDict = new Dictionary<string, object>
					{
						{ "TrustScore", newTrustScore },
						{ "TotalClassesSubmitted", existingScore.TotalClassesSubmitted + 1 },
						{ "ConsecutiveApprovals", consecutiveApprovals },
						{ "BestApprovalStreak", bestStreak },
						{ "AverageApprovalTimeMinutes", newAverage },
						{ "LastCalculatedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
						{ "ModifiedDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
					};

				if (approved)
				{
					updateDict.Add("TotalClassesApproved", existingScore.TotalClassesApproved + 1);
					updateDict.Add("LastApprovalDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

					// Update fastest/slowest
					if (approvalTimeMinutes.HasValue)
					{
						if (!existingScore.FastestApprovalMinutes.HasValue ||
							approvalTimeMinutes.Value < existingScore.FastestApprovalMinutes.Value)
						{
							updateDict.Add("FastestApprovalMinutes", approvalTimeMinutes.Value);
						}

						if (!existingScore.SlowestApprovalMinutes.HasValue ||
							approvalTimeMinutes.Value > existingScore.SlowestApprovalMinutes.Value)
						{
							updateDict.Add("SlowestApprovalMinutes", approvalTimeMinutes.Value);
						}
					}
				}
				else
				{
					updateDict.Add("TotalClassesRejected", existingScore.TotalClassesRejected + 1);
					updateDict.Add("LastRejectionDate", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
				}

				await _trustScoreCommandRepo.UpdateTableColumnById(
					updateDict,
					new KeyValuePair<string, object>("Id", existingScore.Id));

				_logger.Information(
					"✅ Updated trust score - TeacherId: {TeacherId}, NewScore: {NewScore}, Consecutive: {Consecutive}",
					teacherId,
					newTrustScore,
					consecutiveApprovals);
			}
		}
		catch (Exception ex)
		{
			// Log error but don't throw
			// Trust score update failure shouldn't block approval
			_logger.Error(
				ex,
				"⚠️ Error updating trust score - TeacherId: {TeacherId}, SchoolId: {SchoolId}. Approval succeeded anyway.",
				teacherId,
				schoolId);
		}
	}

	/// <summary>
	/// Get teacher trust score record
	/// 
	/// RETURNS:
	/// - Complete TeacherTrustScore record if exists
	/// - Null if teacher has no approval history yet
	/// 
	/// USED BY:
	/// - Admin dashboard (display trust badge)
	/// - Quick preview (show teacher performance)
	/// - Approval workflows (check eligibility for bulk approval)
	/// </summary>
	public async Task<TeacherTrustScore> GetTeacherTrustScore(Guid teacherId, Guid schoolId)
	{
		try
		{
			var query = $@"
                    SELECT TOP 1 *
                    FROM TeacherTrustScore
                    WHERE TeacherId = '{teacherId}'
                    AND SchoolId = '{schoolId}'";

			var results = await _trustScoreQueryRepo.GetByQuery(query);
			return results.FirstOrDefault();
		}
		catch (Exception ex)
		{
			_logger.Error(
				ex,
				"Error getting trust score - TeacherId: {TeacherId}, SchoolId: {SchoolId}",
				teacherId,
				schoolId);

			return null;
		}
	}
}

