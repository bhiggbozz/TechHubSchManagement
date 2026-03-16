using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel;

/// <summary>
/// Teacher trust information for admin preview
/// </summary>
public class TeacherTrustInfo
{
	public string Name { get; set; }
	public decimal TrustScore { get; set; }
	public int TotalApproved { get; set; }
	public int TotalRejected { get; set; }
	public string TrustLevel { get; set; }
}

/// <summary>
/// Media preview (thumbnails only, no full file)
/// </summary>
public class MediaPreviewDto
{
	public Guid MediaId { get; set; }
	public string FileName { get; set; }
	public string MediaType { get; set; }
	public long FileSize { get; set; }
	public int? Duration { get; set; }
	public string ThumbnailUrl { get; set; }
	public string PreviewUrl { get; set; }
	public AIAnalysisDto AIAnalysis { get; set; }
	public List<KeyMomentDto> KeyMoments { get; set; }
}

/// <summary>
/// AI analysis result
/// </summary>
public class AIAnalysisDto
{
	public string Status { get; set; }
	public bool IsEducational { get; set; }
	public bool HasInappropriateContent { get; set; }
	public string Quality { get; set; }
}

/// <summary>
/// Key moment in video
/// </summary>
public class KeyMomentDto
{
	public int TimeSeconds { get; set; }
	public string Label { get; set; }
	public string Timestamp { get; set; }
}

/// <summary>
/// Bulk approval request
/// </summary>
public class BulkApprovalViewModel
{
	public List<Guid> ClassIds { get; set; }
	public string ApprovalNote { get; set; }
}

/// <summary>
/// Bulk approval response
/// </summary>
public class BulkApprovalResponse : BaseResponse
{
	public int SuccessCount { get; set; }
	public int FailCount { get; set; }
	public List<string> Errors { get; set; }
}

/// <summary>
/// Content analysis response
/// </summary>
public class ContentAnalysisResponse : BaseResponse
{
	public Guid MediaId { get; set; }
	public string AnalysisData { get; set; }
	public new string Status { get; set; } // Override base Status
}

/// <summary>
/// Class preparation data transfer object
/// Used for API responses with all formatted fields
/// </summary>
public class ClassPreparationDto
{
	public Guid Id { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// CLASS DETAILS
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string Title { get; set; }
	public string Topic { get; set; }
	public string SubTopic { get; set; }
	public string AimAndObjectives { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// SUBJECT
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// CLASSROOM
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public Guid ClassroomId { get; set; }
	public string ClassroomName { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// TEACHER
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public Guid TeacherId { get; set; }
	public string TeacherName { get; set; }
	public string TeacherEmail { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// SCHEDULING
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string ScheduledDate { get; set; }  // Formatted: yyyy-MM-dd
	public string ScheduledTime { get; set; }  // Formatted: HH:mm
	public int? DurationMinutes { get; set; }
	public string DurationFormatted { get; set; }  // e.g., "1h 30min"

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// CLASS TYPE
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public int ClassType { get; set; }
	public string ClassTypeName { get; set; }  // e.g., "Live", "Recorded"

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// STATUS
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public int Status { get; set; }
	public string StatusName { get; set; }  // e.g., "Draft", "Pending", "Approved"
	public string StatusColor { get; set; }  // e.g., "#green", "#orange"

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// MEDIA
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public List<MediaFileDto> MediaFiles { get; set; }
	public int MediaFilesCount { get; set; }
	public long TotalMediaSizeBytes { get; set; }
	public string TotalMediaSizeFormatted { get; set; }  // e.g., "150 MB"

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// WORKFLOW TRACKING
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string SubmittedDate { get; set; }
	public string SubmittedByName { get; set; }

	public string ApprovedDate { get; set; }
	public string ApprovedByName { get; set; }
	public string ApprovalNotes { get; set; }

	public string RejectedDate { get; set; }
	public string RejectedByName { get; set; }
	public string RejectionReason { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// METADATA
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string CreationDate { get; set; }
	public string ModifiedDate { get; set; }
	public string CreatedByName { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// PERMISSIONS (for frontend)
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public bool CanEdit { get; set; }
	public bool CanDelete { get; set; }
	public bool CanSubmit { get; set; }
	public bool CanApprove { get; set; }
	public bool CanReject { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// ADMIN WORKFLOW FLAGS (optional - for admin dashboard)
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public bool IsUrgent { get; set; }  // Class starts < 24 hours
	public bool NeedsReview { get; set; }  // Low trust score, new teacher
}
/// <summary>
/// Approve class request
/// </summary>
public class ApproveClassViewModel
{
	public Guid ClassPreparationId { get; set; }
	public string ApprovalNotes { get; set; }
}

/// <summary>
/// Reject class request
/// </summary>
public class RejectClassViewModel
{
	public Guid ClassPreparationId { get; set; }
	public string RejectionReason { get; set; }
}

/// <summary>
/// Delete media request
/// </summary>
public class DeleteMediaViewModel
{
	public Guid MediaId { get; set; }
	public string Reason { get; set; }
}

/// <summary>
/// Media file DTO
/// </summary>
public class MediaFileDto
{
	public bool IsTemporary;
	public long FileSizeBytes;
	public string UploadedByName;
	public string OriginalFileName;
	public string FileExtension;

	public Guid MediaId { get; set; }
	public string FileName { get; set; }
	public string DisplayName { get; set; }
	public int MediaType { get; set; }
	public string MediaTypeName { get; set; }
	public string MediaTypeIcon { get; set; }

	public long FileSize { get; set; }
	public string FileSizeFormatted { get; set; }
	public long OriginalSize { get; set; }
	public string OriginalSizeFormatted { get; set; }

	public int? Duration { get; set; }
	public string DurationFormatted { get; set; }

	public double? CompressionRatio { get; set; }

	public string CdnUrl { get; set; }
	public string ThumbnailUrl { get; set; }
	public string PreviewUrl { get; set; }

	public int UploadStatus { get; set; }
	public string UploadStatusName { get; set; }
	public string UploadErrorMessage { get; set; }

	public int AIAnalysisStatus { get; set; }
	public string AIAnalysisData { get; set; }
	public string KeyMoments { get; set; }

	public DateTime? UploadedDate { get; set; }
	public string UploadedBy { get; set; }

	public int DownloadCount { get; set; }
	public DateTime? LastDownloadDate { get; set; }

	public string? MediaKey { get; set; }
	public string? PublicId { get; set; }
	public bool IsDeleted { get; set; }
	public object DurationSeconds { get; set; }
	public long OriginalSizeBytes { get; set; }
}
