using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class ClassPreparationMedia
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid? ClassPreparationId { get; set; }

	public string MediaKey { get; set; } = string.Empty;
	public string? PublicId { get; set; }

	public int MediaType { get; set; }
	public string OriginalFileName { get; set; } = string.Empty;
	public string? DisplayName { get; set; }

	public long FileSizeBytes { get; set; }
	public long? OriginalSizeBytes { get; set; }
	public int? DurationSeconds { get; set; }
	public string? MimeType { get; set; }
	public string? FileExtension { get; set; }

	public string? SHA256Hash { get; set; }

	public string CdnUrl { get; set; } = string.Empty;
	public string? ThumbnailUrl { get; set; }
	public string? CdnProvider { get; set; }

	public bool IsTemporary { get; set; }

	public int? DownloadCount { get; set; }
	public DateTime? LastDownloadDate { get; set; }
	public Guid? LastDownloadedBy { get; set; }

	public bool IsDeleted { get; set; }
	public DateTime? DeletedDate { get; set; }
	public Guid? DeletedBy { get; set; }
	public string? DeletionReason { get; set; }

	public DateTime UploadedDate { get; set; }
	public Guid SchoolId { get; set; }
	public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
	public Guid CreatedBy { get; set; }
	public bool IsActive { get; set; }
}

