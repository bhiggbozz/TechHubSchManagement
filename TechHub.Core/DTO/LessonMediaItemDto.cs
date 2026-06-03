using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.DTO;

public class LessonMediaItemDto
{
	public Guid LessonContentId { get; set; }
	public Guid MediaId { get; set; }
	public string MediaName { get; set; }
	public string Url { get; set; }
	public string MediaType { get; set; }
	public string FileExtension { get; set; }
	public long FileSizeBytes { get; set; }
	public int DisplayOrder { get; set; }
}
