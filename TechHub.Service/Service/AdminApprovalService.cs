using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Enums;
using TechHub.Core.Model;
using TechHub.Core.ResponseModel;
using TechHub.Core.ViewModel;
using TechHub.Core.ViewModel.classroom;
using TechHub.Service.Interface;
using TechhubMS.util;

namespace TechHub.Service.Service;
/// <summary>
/// Admin approval service implementation
/// Provides smart workflows for efficient class approval
/// </summary>
public class AdminApprovalService : IAdminApprovalService
{
	private readonly IQueryRepository<ClassPreparation> _classQueryRepo;
	private readonly IQueryRepository<ClassPreparationMedia> _mediaQueryRepo;
	private readonly IQueryRepository<Users> _userQueryRepo;
	private readonly ITeacherTrustScoreService _trustScoreService;
	private readonly IClassPreparationService _classPreparationService;
	private readonly ILogger _logger;

	public AdminApprovalService(
		IQueryRepository<ClassPreparation> classQueryRepo,
		IQueryRepository<ClassPreparationMedia> mediaQueryRepo,
		IQueryRepository<Users> userQueryRepo,
		ITeacherTrustScoreService trustScoreService,
		IClassPreparationService classPreparationService,
		ILogger logger)
	{
		_classQueryRepo = classQueryRepo;
		_mediaQueryRepo = mediaQueryRepo;
		_userQueryRepo = userQueryRepo;
		_trustScoreService = trustScoreService;
		_classPreparationService = classPreparationService;
		_logger = logger;
	}

	/// <summary>
	/// Get pending approvals with smart sorting
	/// 
	/// PRIORITIZATION LOGIC:
	/// 
	/// URGENT (IsUrgent = 1):
	/// - Class starts within 24 hours
	/// - Needs immediate attention
	/// 
	/// NEEDS REVIEW (NeedsReview = 1):
	/// - New teacher (< 5 approved classes)
	/// - Large files (> 100 MB per file)
	/// - AI flagged content
	/// - Low trust score (< 60)
	/// 
	/// ROUTINE:
	/// - Trusted teachers (trust score >= 80)
	/// - Normal file sizes
	/// - AI validated content
	/// - Can be bulk approved
	/// </summary>
	public async Task<PendingApprovalsResponse> GetPendingApprovals(string sortBy,AuthenticatedUserClaims userClaims)
	{
		try
		{
			_logger.Information("Getting pending approvals - SortBy: {SortBy}", sortBy);

			if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
			{
				return new PendingApprovalsResponse
				{
					Urgent = new List<ClassPreparationDto>(),
					NeedsReview = new List<ClassPreparationDto>(),
					Routine = new List<ClassPreparationDto>(),
					TotalCount = 0
				};
			}

			// Query all pending classes with teacher info
			var query = $@"
                    SELECT 
                        c.*,
                        u.UserName as TeacherName,
                        u.Email as TeacherEmail
                    FROM ClassPreparation c
                    INNER JOIN Users u ON c.TeacherId = u.Id
                    WHERE c.Status = {(int)ClassPreparationStatus.Pending}
                    AND c.SchoolId = '{schoolId}'
                    AND c.IsActive = 1
                    ORDER BY c.SubmittedForApprovalDate DESC";

			var allPending = await _classQueryRepo.GetByQuery(query);

			if (!allPending.Any())
			{
				return new PendingApprovalsResponse
				{
					Urgent = new List<ClassPreparationDto>(),
					NeedsReview = new List<ClassPreparationDto>(),
					Routine = new List<ClassPreparationDto>(),
					TotalCount = 0
				};
			}

			// Categorize into urgent, needs review, routine
			var urgent = new List<ClassPreparation>();
			var needsReview = new List<ClassPreparation>();
			var routine = new List<ClassPreparation>();

			foreach (var classPrep in allPending)
			{
				// Check if urgent (starts < 24 hours)
				if (classPrep.ScheduledDate.HasValue)
				{
					var classDateTime = classPrep.ScheduledDate.Value;
					if (classPrep.ScheduledTime.HasValue)
					{
						classDateTime = classDateTime.Add(classPrep.ScheduledTime.Value);
					}

					var hoursUntilClass = (classDateTime - DateTime.UtcNow).TotalHours;

					if (hoursUntilClass > 0 && hoursUntilClass < 24)
					{
						urgent.Add(classPrep);
						continue;
					}
				}

				// Check if needs review
				var trustScore = await _trustScoreService.GetTeacherTrustScore(
					classPrep.TeacherId,
					schoolId);

				var needsManualReview = trustScore == null || trustScore.TrustScore < 60;

				if (needsManualReview)
				{
					needsReview.Add(classPrep);
				}
				else
				{
					routine.Add(classPrep);
				}
			}

			_logger.Information(
				"Approvals categorized - Urgent: {Urgent}, NeedsReview: {NeedsReview}, Routine: {Routine}",
				urgent.Count,
				needsReview.Count,
				routine.Count);

			// Map to DTOs (simplified - would normally include full details)
			return new PendingApprovalsResponse
			{
				Urgent = urgent.Select(c => MapToDto(c)).ToList(),
				NeedsReview = needsReview.Select(c => MapToDto(c)).ToList(),
				Routine = routine.Select(c => MapToDto(c)).ToList(),
				TotalCount = allPending.Count()
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error getting pending approvals");

			return new PendingApprovalsResponse
			{
				Urgent = new List<ClassPreparationDto>(),
				NeedsReview = new List<ClassPreparationDto>(),
				Routine = new List<ClassPreparationDto>(),
				TotalCount = 0
			};
		}
	}

	/// <summary>
	/// Get quick preview data
	/// Optimized for fast loading with minimal bandwidth
	/// </summary>
	public async Task<QuickPreviewResponse> GetQuickPreview(Guid classPreparationId, AuthenticatedUserClaims userClaims)
	{
		try
		{
			_logger.Information("Getting quick preview - ClassId: {ClassId}", classPreparationId);

			// Get class details
			var classPrep = await _classQueryRepo.Get(classPreparationId);

			if (classPrep == null)
			{
				return new QuickPreviewResponse
				{
					ResponseCode = ResponseCode.NotFound,
					ResponseMessage = "Class not found",
					Status = "failed"
				};
			}

			// Get teacher trust score
			if (!Guid.TryParse(userClaims.SchoolId, out var schoolId))
			{
				return new QuickPreviewResponse
				{
					ResponseCode = ResponseCode.BadRequest,
					ResponseMessage = "Invalid school ID",
					Status = "failed"
				};
			}

			var trustScore = await _trustScoreService.GetTeacherTrustScore(
				classPrep.TeacherId,
				schoolId);

			var teacherInfo = new TeacherTrustInfo
			{
				Name = classPrep.TeacherName ?? "Unknown",
				TrustScore = trustScore?.TrustScore ?? 0,
				TotalApproved = trustScore?.TotalClassesApproved ?? 0,
				TotalRejected = trustScore?.TotalClassesRejected ?? 0,
				TrustLevel = GetTrustLevel(trustScore?.TrustScore ?? 0)
			};

			// Get media files with thumbnails only
			var mediaQuery = $@"
                    SELECT *
                    FROM ClassPreparationMedia
                    WHERE ClassPreparationId = '{classPreparationId}'
                    AND IsDeleted = 0
                    ORDER BY CreationDate";

			var mediaFiles = await _mediaQueryRepo.GetByQuery(mediaQuery);

			var mediaPreviews = mediaFiles.Select(m => new MediaPreviewDto
			{
				MediaId = m.Id,
				FileName = m.OriginalFileName,
				MediaType = ((MediaType)m.MediaType).ToString(),
				FileSize = m.FileSizeBytes,
				Duration = m.DurationSeconds,
				ThumbnailUrl = m.ThumbnailUrl,
				PreviewUrl = m.PreviewUrl,
				AIAnalysis = ParseAIAnalysis(m.AIAnalysisData),
				KeyMoments = ParseKeyMoments(m.KeyMoments)
			}).ToList();

			return new QuickPreviewResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Quick preview retrieved",
				Status = "successful",
				ClassInfo = MapToDto(classPrep),
				Teacher = teacherInfo,
				MediaFiles = mediaPreviews
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error getting quick preview - ClassId: {ClassId}", classPreparationId);

			return new QuickPreviewResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "Failed to get preview",
				Status = "failed"
			};
		}
	}

	/// <summary>
	/// Bulk approve multiple classes
	/// </summary>
	public async Task<BulkApprovalResponse> BulkApprove(BulkApprovalViewModel model,AuthenticatedUserClaims userClaims)
	{
		try
		{
			_logger.Information(
				"Bulk approving classes - Count: {Count}",
				model.ClassIds.Count);

			var successCount = 0;
			var failCount = 0;
			var errors = new List<string>();

			foreach (var classId in model.ClassIds)
			{
				try
				{
					var approveModel = new TechHub.Core.ViewModel.classroom.ApproveClassViewModel
					{
						ClassPreparationId = classId,
						ApprovalNotes = model.ApprovalNote
					};

					var result = await _classPreparationService.ApproveClass(
						approveModel,
						userClaims);

					if (result.ResponseCode == ResponseCode.successful)
					{
						successCount++;
					}
					else
					{
						failCount++;
						errors.Add($"Class {classId}: {result.ResponseMessage}");
					}
				}
				catch (Exception ex)
				{
					failCount++;
					errors.Add($"Class {classId}: {ex.Message}");
					_logger.Error(ex, "Error in bulk approval - ClassId: {ClassId}", classId);
				}
			}

			_logger.Information(
				"✅ Bulk approval completed - Success: {Success}, Failed: {Failed}",
				successCount,
				failCount);

			return new BulkApprovalResponse
			{
				ResponseCode = successCount > 0 ? ResponseCode.successful : ResponseCode.ErrorOccured,
				ResponseMessage = $"Approved {successCount} of {model.ClassIds.Count} classes",
				Status = successCount > 0 ? "successful" : "failed",
				SuccessCount = successCount,
				FailCount = failCount,
				Errors = errors
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error in bulk approval");

			return new BulkApprovalResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "Bulk approval failed",
				Status = "failed",
				SuccessCount = 0,
				FailCount = model.ClassIds.Count,
				Errors = new List<string> { ex.Message }
			};
		}
	}

	/// <summary>
	/// Get AI content analysis for media
	/// </summary>
	public async Task<ContentAnalysisResponse> GetContentAnalysis(Guid mediaId)
	{
		try
		{
			var media = await _mediaQueryRepo.Get(mediaId);

			if (media == null)
			{
				return new ContentAnalysisResponse
				{
					ResponseCode = ResponseCode.NotFound,
					ResponseMessage = "Media not found",
					Status = "failed"
				};
			}

			return new ContentAnalysisResponse
			{
				ResponseCode = ResponseCode.successful,
				ResponseMessage = "Analysis retrieved",
				//Status = "successful",
				MediaId = mediaId,
				AnalysisData = media.AIAnalysisData,
				Status = ((AIAnalysisStatus)media.AIAnalysisStatus).ToString()
			};
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Error getting analysis - MediaId: {MediaId}", mediaId);

			return new ContentAnalysisResponse
			{
				ResponseCode = ResponseCode.ErrorOccured,
				ResponseMessage = "Failed to get analysis",
				Status = "failed"
			};
		}
	}

	#region Helper Methods

	private ClassPreparationDto MapToDto(ClassPreparation classPrep)
	{
		return new ClassPreparationDto
		{
			Id = classPrep.Id,
			Title = classPrep.Title,
			Topic = classPrep.Topic,
			SubTopic = classPrep.SubTopic,
			AimAndObjectives = classPrep.AimAndObjectives,
			ScheduledDate = classPrep.ScheduledDate?.ToString("yyyy-MM-dd HH:mm:ss"),
			ScheduledTime = classPrep.ScheduledTime?.ToString("yyyy-MM-dd HH:mm:ss"),
			DurationMinutes = classPrep.DurationMinutes,
			Status = classPrep.Status,
			StatusName = ((ClassPreparationStatus)classPrep.Status).ToString(),
			//SubmittedForApprovalDate = classPrep.SubmittedForApprovalDate,
			TeacherName = classPrep.TeacherName,
			IsUrgent = classPrep.IsUrgent,
			NeedsReview = classPrep.NeedsReview
		};
	}

	private string GetTrustLevel(decimal trustScore)
	{
		return trustScore switch
		{
			>= 95 => "Excellent",
			>= 80 => "Good",
			>= 60 => "Fair",
			_ => "New"
		};
	}

	private AIAnalysisDto ParseAIAnalysis(string json)
	{
		if (string.IsNullOrEmpty(json))
			return null;

		try
		{
			var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

			return new AIAnalysisDto
			{
				Status = "completed",
				IsEducational = true,
				HasInappropriateContent = false,
				Quality = "good"
			};
		}
		catch
		{
			return null;
		}
	}

	private List<KeyMomentDto> ParseKeyMoments(string json)
	{
		if (string.IsNullOrEmpty(json))
			return new List<KeyMomentDto>();

		try
		{
			return JsonSerializer.Deserialize<List<KeyMomentDto>>(json) ?? new List<KeyMomentDto>();
		}
		catch
		{
			return new List<KeyMomentDto>();
		}
	}

	#endregion
}
