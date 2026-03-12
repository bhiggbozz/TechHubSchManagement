using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;


	/// <summary>
	/// Media file associated with class preparation
	/// Supports videos, images, documents, and audio
	/// Includes upload status tracking for background processing
	/// </summary>
	public class ClassPreparationMedia
	{
		

		public Guid Id { get; set; } = Guid.NewGuid();

		/// <summary>
		/// Link to ClassPreparation (null until linked)
		/// </summary>
		public Guid? ClassPreparationId { get; set; }

		/// <summary>
		/// Unique media key for identification
		/// Format: {schoolId-8chars}_{yyyyMMddHHmmss}_{sha256-6chars}_{filename}
		/// </summary>
		public string MediaKey { get; set; } = string.Empty;

		/// <summary>
		/// Cloudinary public_id (used for delete/move operations)
		/// </summary>
		public string? PublicId { get; set; }

		// ========================================
		// MEDIA INFORMATION
		// ========================================

		/// <summary>
		/// Media type: 1=Video, 2=Image, 3=Document, 4=Audio
		/// </summary>
		public int MediaType { get; set; }

		public string OriginalFileName { get; set; } = string.Empty;
		public string? DisplayName { get; set; }

		// ========================================
		// FILE SIZE
		// ========================================

		/// <summary>
		/// File size after compression/processing
		/// </summary>
		public long FileSizeBytes { get; set; }

		/// <summary>
		/// Original file size before compression
		/// </summary>
		public long OriginalSizeBytes { get; set; }

		// ========================================
		// MEDIA DETAILS
		// ========================================

		/// <summary>
		/// Duration in seconds (for videos/audio)
		/// </summary>
		public int? DurationSeconds { get; set; }

		public string? MimeType { get; set; }
		public string? FileExtension { get; set; }

		/// <summary>
		/// SHA256 hash for duplicate detection
		/// </summary>
		public string? SHA256Hash { get; set; }

		// ========================================
		// CDN STORAGE
		// ========================================

		public string CdnUrl { get; set; } = string.Empty;
		public string? ThumbnailUrl { get; set; }
		public string? CdnProvider { get; set; } = "Cloudinary";

		/// <summary>
		/// True = temp/pending folder, False = schools folder (permanent)
		/// </summary>
		public bool IsTemporary { get; set; } = true;


		/// <summary>
		/// Upload status: 0=Pending, 1=Uploading, 2=Completed, 3=Failed
		/// </summary>
		public int UploadStatus { get; set; } = 0;

		/// <summary>
		/// Error message if upload failed
		/// </summary>
		public string? UploadErrorMessage { get; set; }


		public int DownloadCount { get; set; } = 0;
		public DateTime? LastDownloadDate { get; set; }
		public Guid? LastDownloadedBy { get; set; }


		public bool IsDeleted { get; set; } = false;
		public DateTime? DeletedDate { get; set; }
		public Guid? DeletedBy { get; set; }
		public string? DeletionReason { get; set; }

		public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
		public Guid SchoolId { get; set; }
		public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
		public string? ModifiedDate { get; set; }
		public Guid CreatedBy { get; set; }
		public bool IsActive { get; set; } = true;
	}

