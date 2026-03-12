using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.DTO;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Enums;

namespace TechHub.Core.Helper
{
	public static class MediaExtension
	{
		public static MediaFileDto ToDto(this ClassPreparationMedia media, string uploadedByName = "")
		{
			var dto = new MediaFileDto
			{
				// Identifiers
				Id = media.Id,
				MediaKey = media.MediaKey,
				PublicId = media.PublicId ?? string.Empty,

				// Media information
				MediaType = media.MediaType,
				MediaTypeName = ((MediaType)media.MediaType).ToString(),
				MediaTypeIcon = GetMediaTypeIcon((MediaType)media.MediaType),

				OriginalFileName = media.OriginalFileName,
				DisplayName = media.DisplayName ?? media.OriginalFileName,
				FileExtension = media.FileExtension ?? string.Empty,

				// File size
				FileSizeBytes = media.FileSizeBytes,
				FileSizeFormatted = FormatFileSize(media.FileSizeBytes),

				OriginalSizeBytes = media.OriginalSizeBytes,
				OriginalSizeFormatted = FormatFileSize(media.OriginalSizeBytes),

				CompressionRatio = CalculateCompressionRatio(
					media.OriginalSizeBytes,
					media.FileSizeBytes),

				// Duration
				DurationSeconds = media.DurationSeconds,
				DurationFormatted = FormatDuration(media.DurationSeconds),

				// CDN URLs
				CdnUrl = media.CdnUrl ?? string.Empty,
				ThumbnailUrl = media.ThumbnailUrl,
				CdnProvider = media.CdnProvider ?? "Cloudinary",

				// Storage status
				IsTemporary = media.IsTemporary,
				IsDeleted = media.IsDeleted,

				// Upload status
				UploadStatus = media.UploadStatus,
				UploadStatusName = ((UploadStatus)media.UploadStatus).ToString(),
				UploadErrorMessage = media.UploadErrorMessage,

				// Download tracking
				DownloadCount = media.DownloadCount,
				LastDownloadDate = media.LastDownloadDate,

				// Metadata
				UploadedDate = media.UploadedDate.ToString("yyyy-MM-dd HH:mm:ss"),
				UploadedByName = uploadedByName
			};

			return dto;
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

		/// <summary>
		/// Calculate compression ratio as percentage
		/// </summary>
		/// <param name="originalSize">Original file size in bytes</param>
		/// <param name="compressedSize">Compressed file size in bytes</param>
		/// <returns>Compression percentage (e.g., 90.0 = 90% reduction) or null if not calculable</returns>
		private static double? CalculateCompressionRatio(long originalSize, long compressedSize)
		{
			// Validation: Can't calculate with invalid original size
			if (originalSize <= 0)
				return null;

			// Validation: Can't calculate if not yet uploaded/processed
			if (compressedSize == 0)
				return null;

			// Edge case: File expanded instead of compressed (rare)
			if (compressedSize > originalSize)
				return 0.0;

			// Calculate: (1 - compressed/original) × 100
			// Example: 500MB → 50MB = (1 - 50/500) × 100 = 90%
			var ratio = (1 - ((double)compressedSize / originalSize)) * 100;

			// Round to 1 decimal place for readability
			return Math.Round(ratio, 1);
		}

	}
}
