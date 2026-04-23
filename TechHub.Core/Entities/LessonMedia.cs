using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class LessonMedia
{
	public Guid Id { get; set; }
	public Guid LessonContentId { get; set; }
	public Guid SchoolId { get; set; }
	public string FileName { get; set; }
	public string OriginalFileName { get; set; }
	public string FileExtension { get; set; }
	public string MediaType { get; set; }
	public long FileSizeBytes { get; set; }
	public string CloudinaryUrl { get; set; }
	public string PublicId { get; set; }
	public int? Duration { get; set; }
	public string Status { get; set; }
	public int DisplayOrder { get; set; }
	public DateTime CreatedAt { get; set; }
	public bool IsActive { get; set; }
}

