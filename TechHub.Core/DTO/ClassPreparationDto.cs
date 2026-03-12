using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.DTO;

/// <summary>
/// Class preparation details DTO
/// </summary>
public class ClassPreparationDto
{
	public Guid Id { get; set; }

	// Class details
	public string Title { get; set; } = string.Empty;
	public string Topic { get; set; } = string.Empty;
	public string? SubTopic { get; set; }
	public string AimAndObjectives { get; set; } = string.Empty;

	// Subject details
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; } = string.Empty;

	// Classroom details
	public Guid ClassroomId { get; set; }
	public string ClassroomName { get; set; } = string.Empty;

	// Teacher details
	public Guid TeacherId { get; set; }
	public string TeacherName { get; set; } = string.Empty;
	public string? TeacherEmail { get; set; }

	// Scheduling
	public string? ScheduledDate { get; set; }  // "2025-03-08"
	public string? ScheduledTime { get; set; }  // "14:30"
	public int? DurationMinutes { get; set; }
	public string? DurationFormatted { get; set; }  // "1 hour 30 minutes"

	// Class type
	public int ClassType { get; set; }
	public string ClassTypeName { get; set; } = string.Empty;

	// Status
	public int Status { get; set; }
	public string StatusName { get; set; } = string.Empty;
	public string StatusColor { get; set; } = string.Empty;  // For UI: "yellow", "green", "red"

	// Media files
	public List<MediaFileDto> MediaFiles { get; set; } = new();
	public int MediaFilesCount { get; set; }
	public long TotalMediaSizeBytes { get; set; }
	public string TotalMediaSizeFormatted { get; set; } = string.Empty;

	// Workflow tracking
	public string? SubmittedDate { get; set; }
	public string? SubmittedByName { get; set; }

	public string? ApprovedDate { get; set; }
	public string? ApprovedByName { get; set; }

	public string? RejectedDate { get; set; }
	public string? RejectedByName { get; set; }
	public string? RejectionReason { get; set; }

	// Metadata
	public string CreationDate { get; set; } = string.Empty;
	public string ModifiedDate { get; set; } = string.Empty;
	public string CreatedByName { get; set; } = string.Empty;

	// Permissions (for frontend to show/hide buttons)
	public bool CanEdit { get; set; }
	public bool CanDelete { get; set; }
	public bool CanSubmit { get; set; }
	public bool CanApprove { get; set; }
	public bool CanReject { get; set; }
}

/// <summary>
/// Media file details DTO
/// </summary>
public class MediaFileDto
{
	
	public Guid Id { get; set; }
	public string MediaKey { get; set; } = string.Empty;
	public string PublicId { get; set; } = string.Empty;

	public int MediaType { get; set; }
	public string MediaTypeName { get; set; } = string.Empty;
	public string MediaTypeIcon { get; set; } = string.Empty;

	public string OriginalFileName { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string FileExtension { get; set; } = string.Empty;

	public long FileSizeBytes { get; set; }
	public string FileSizeFormatted { get; set; } = string.Empty;

	public long OriginalSizeBytes { get; set; }
	public string OriginalSizeFormatted { get; set; } = string.Empty;

	public double? CompressionRatio { get; set; }

	public int? DurationSeconds { get; set; }
	public string? DurationFormatted { get; set; }

	public string CdnUrl { get; set; } = string.Empty;
	public string? ThumbnailUrl { get; set; }
	public string CdnProvider { get; set; } = "Cloudinary";

	public bool IsTemporary { get; set; }
	public bool IsDeleted { get; set; }

	public int UploadStatus { get; set; }
	public string UploadStatusName { get; set; } = string.Empty;
	public string? UploadErrorMessage { get; set; }

	public int DownloadCount { get; set; }
	public DateTime? LastDownloadDate { get; set; }

	public string UploadedDate { get; set; } = string.Empty;
	public string UploadedByName { get; set; } = string.Empty;
}

