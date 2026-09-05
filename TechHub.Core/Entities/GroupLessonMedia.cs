using System;

namespace TechHub.Core.Entities;

public class GroupLessonMedia
{
	public Guid Id { get; set; }
	public Guid GroupContentId { get; set; }
	public Guid SchoolId { get; set; }
	public string FileName { get; set; } = string.Empty;
	public string OriginalFileName { get; set; } = string.Empty;
	public string FileExtension { get; set; } = string.Empty;
	public string MediaType { get; set; } = string.Empty;
	public long FileSizeBytes { get; set; }
	public string CloudinaryUrl { get; set; } = string.Empty;
	public string PublicId { get; set; } = string.Empty;
	public int? Duration { get; set; }
	public string Status { get; set; } = string.Empty;
	public int DisplayOrder { get; set; }
	public DateTime CreatedAt { get; set; }
	public bool IsActive { get; set; }
	public string? MetaData { get; set; }
}
