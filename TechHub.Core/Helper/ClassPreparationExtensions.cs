using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.ViewModel;

namespace TechHub.Core.Helper;

public static class ClassPreparationExtensions
{
	/// <summary>
	/// Convert ClassPreparation entity to ClassPreparationDto
	/// 
	/// INCLUDES:
	/// - All entity fields with formatting
	/// - Related entity names (subject, classroom, teacher)
	/// - Media file information
	/// - Workflow tracking (submitted, approved, rejected)
	/// - Permission flags for frontend (can edit, delete, submit, etc.)
	/// </summary>
	public static ClassPreparationDto ToDto(this ClassPreparation classPrep,string subjectName = null,string classroomName = null,string teacherName = null,string teacherEmail = null,List<MediaFileDto> mediaFiles = null,string submittedByName = null,string approvedByName = null,string rejectedByName = null,string createdByName = null,Guid? currentUserId = null)
	{
		var status = (ClassPreparationStatus)classPrep.Status;
		var classType = (ClassType)classPrep.ClassType;

		// Calculate total media size
		var totalMediaSize = mediaFiles?.Sum(m => m.FileSize) ?? 0;

		var dto = new ClassPreparationDto
		{
			Id = classPrep.Id,

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// CLASS DETAILS
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			Title = classPrep.Title,
			Topic = classPrep.Topic,
			SubTopic = classPrep.SubTopic,
			AimAndObjectives = classPrep.AimAndObjectives,

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// SUBJECT
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			SubjectId = classPrep.SubjectId,
			SubjectName = subjectName ?? "Unknown",

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// CLASSROOM
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			ClassroomId = classPrep.ClassroomId,
			ClassroomName = classroomName ?? "Unknown",

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// TEACHER
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			TeacherId = classPrep.TeacherId,
			TeacherName = teacherName ?? "Unknown",
			TeacherEmail = teacherEmail,

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// SCHEDULING
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			ScheduledDate = classPrep.ScheduledDate?.ToString("yyyy-MM-dd"),
			ScheduledTime = classPrep.ScheduledTime?.ToString(@"hh\:mm"),
			DurationMinutes = classPrep.DurationMinutes,
			DurationFormatted = FormatDuration(classPrep.DurationMinutes),

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// CLASS TYPE
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			ClassType = classPrep.ClassType,
			ClassTypeName = classType.ToString(),

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// STATUS
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			Status = classPrep.Status,
			StatusName = status.ToString(),
			StatusColor = GetStatusColor(status),

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// MEDIA
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			MediaFiles = mediaFiles ?? new List<MediaFileDto>(),
			MediaFilesCount = mediaFiles?.Count ?? 0,
			TotalMediaSizeBytes = totalMediaSize,
			TotalMediaSizeFormatted = FormatFileSize(totalMediaSize),

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// WORKFLOW TRACKING
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			SubmittedDate = classPrep.SubmittedForApprovalDate?.ToString("yyyy-MM-dd HH:mm:ss"),
			SubmittedByName = submittedByName,

			ApprovedDate = classPrep.ApprovedDate?.ToString("yyyy-MM-dd HH:mm:ss"),
			ApprovedByName = approvedByName,
			//ApprovalNotes = classPrep.ApprovalNotes,

			RejectedDate = classPrep.RejectedDate?.ToString("yyyy-MM-dd HH:mm:ss"),
			RejectedByName = rejectedByName,
			RejectionReason = classPrep.RejectionReason,

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// METADATA
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			CreationDate = classPrep.CreationDate,
			ModifiedDate = classPrep.ModifiedDate,
			CreatedByName = createdByName ?? "",

			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
			// PERMISSIONS (for frontend button visibility)
			// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

			CanEdit = CanEdit(classPrep, currentUserId),
			CanDelete = CanDelete(classPrep, currentUserId),
			CanSubmit = CanSubmit(classPrep, currentUserId),
			CanApprove = CanApprove(classPrep, currentUserId),
			CanReject = CanReject(classPrep, currentUserId)
		};

		return dto;
	
	}

	private static string? FormatDuration(int? totalMinutes)
	{
		if (!totalMinutes.HasValue || totalMinutes.Value <= 0)
			return null;

		var hours = totalMinutes.Value / 60;
		var minutes = totalMinutes.Value % 60;

		if (hours > 0 && minutes > 0)
			return $"{hours} hour{(hours > 1 ? "s" : "")} {minutes} minute{(minutes > 1 ? "s" : "")}";
		else if (hours > 0)
			return $"{hours} hour{(hours > 1 ? "s" : "")}";
		else
			return $"{minutes} minute{(minutes > 1 ? "s" : "")}";
	}

	private static string FormatFileSize(long bytes)
	{
		string[] sizes = { "B", "KB", "MB", "GB" };
		double len = bytes;
		int order = 0;
		while (len >= 1024 && order < sizes.Length - 1)
		{
			order++;
			len = len / 1024;
		}
		return $"{len:0.##} {sizes[order]}";
	}

	private static string GetStatusColor(ClassPreparationStatus status)
	{
		return status switch
		{
			ClassPreparationStatus.Draft => "gray",
			ClassPreparationStatus.Pending => "yellow",
			ClassPreparationStatus.Approved => "green",
			ClassPreparationStatus.Rejected => "red",
			ClassPreparationStatus.InProgress => "blue",
			ClassPreparationStatus.Completed => "purple",
			_ => "gray"
		};
	}

	private static string GetMediaTypeIcon(MediaType mediaType)
	{
		return mediaType switch
		{
			MediaType.Video => "video",
			MediaType.Audio => "audio",
			MediaType.Image => "image",
			MediaType.Document => "document",
			_ => "file"
		};
	}

	#region Permission Helpers

	private static bool CanEdit(ClassPreparation classPrep, Guid? currentUserId)
	{
		// Only teacher can edit their own draft or rejected classes
		if (!currentUserId.HasValue) return false;

		if (classPrep.TeacherId != currentUserId.Value) return false;

		return classPrep.Status == (int)ClassPreparationStatus.Draft ||
				classPrep.Status == (int)ClassPreparationStatus.Rejected;
	}

	private static bool CanDelete(ClassPreparation classPrep, Guid? currentUserId)
	{
		// Only teacher can delete their own draft
		if (!currentUserId.HasValue) return false;

		return classPrep.TeacherId == currentUserId.Value &&
				classPrep.Status == (int)ClassPreparationStatus.Draft;
	}

	private static bool CanSubmit(ClassPreparation classPrep, Guid? currentUserId)
	{
		// Only teacher can submit their own draft or rejected classes
		if (!currentUserId.HasValue) return false;

		if (classPrep.TeacherId != currentUserId.Value) return false;

		return classPrep.Status == (int)ClassPreparationStatus.Draft ||
				classPrep.Status == (int)ClassPreparationStatus.Rejected;
	}

	private static bool CanApprove(ClassPreparation classPrep, Guid? currentUserId)
	{
		// This will be checked in service layer with AdminPermissions
		// Here we just check if it's pending
		return classPrep.Status == (int)ClassPreparationStatus.Pending;
	}

	private static bool CanReject(ClassPreparation classPrep, Guid? currentUserId)
	{
		// This will be checked in service layer with AdminPermissions
		// Here we just check if it's pending
		return classPrep.Status == (int)ClassPreparationStatus.Pending;
	}

	

	#endregion
}



/// <summary>
/// Map ClassPreparationMedia entity to DTO
/// </summary>
//public static MediaFileDto ToDto(this ClassPreparationMedia media,string? uploadedByName = null)
//{
//	var compressionRatio = media.OriginalSizeBytes.HasValue && media.OriginalSizeBytes > 0
//		? (1 - ((double)media.FileSizeBytes / media.OriginalSizeBytes.Value)) * 100
//		: (double?)null;

//	return new MediaFileDto
//	{
//		Id = media.Id,

//		// Identifiers
//		MediaKey = media.MediaKey,
//		PublicId = media.PublicId ?? string.Empty,

//		// Type
//		MediaType = media.MediaType,
//		MediaTypeName = ((MediaType)media.MediaType).ToString(),
//		MediaTypeIcon = GetMediaTypeIcon((MediaType)media.MediaType),

//		// File details
//		OriginalFileName = media.OriginalFileName,
//		DisplayName = media.DisplayName ?? media.OriginalFileName,
//		FileExtension = media.FileExtension ?? string.Empty,

//		// Size
//		FileSizeBytes = media.FileSizeBytes,
//		FileSizeFormatted = FormatFileSize(media.FileSizeBytes),
//		OriginalSizeBytes = media.OriginalSizeBytes,
//		OriginalSizeFormatted = media.OriginalSizeBytes.HasValue
//			? FormatFileSize(media.OriginalSizeBytes.Value)
//			: null,
//		CompressionRatio = compressionRatio,

//		// Duration
//		DurationSeconds = media.DurationSeconds,
//		DurationFormatted = FormatDuration(media.DurationSeconds),

//		// URLs
//		CdnUrl = media.CdnUrl,
//		ThumbnailUrl = media.ThumbnailUrl,

//		// Status
//		IsTemporary = media.IsTemporary,
//		IsDeleted = media.IsDeleted,
//		DeletionReason = media.DeletionReason,

//		// Tracking
//		DownloadCount = media.DownloadCount ?? 0,
//		LastDownloadDate = media.LastDownloadDate?.ToString("yyyy-MM-dd HH:mm:ss"),

//		// Metadata
//		UploadedDate = media.UploadedDate.ToString("yyyy-MM-dd HH:mm:ss"),
//		UploadedByName = uploadedByName ?? ""
//	};
//}


//public static class MediaExtensions
//{
	/// <summary>
	/// Map ClassPreparationMedia entity to DTO
	/// </summary>
	

	

	
	//private static string? FormatDuration(int? totalSeconds)
	//{
	//	if (!totalSeconds.HasValue || totalSeconds.Value <= 0)
	//		return null;

	//	var ts = TimeSpan.FromSeconds(totalSeconds.Value);

	//	if (ts.TotalHours >= 1)
	//		return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
	//	else
	//		return $"{ts.Minutes}:{ts.Seconds:D2}";
	//}

	
//}


