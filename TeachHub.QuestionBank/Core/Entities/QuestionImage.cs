using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Entities;

public class QuestionImage
{
	public Guid Id { get; set; }
	public Guid QuestionId { get; set; }
	public Guid JobId { get; set; }
	public Guid SchoolId { get; set; }
	public string Label { get; set; } = string.Empty;
	public string CloudinaryUrl { get; set; } = string.Empty;
	public string PublicId { get; set; } = string.Empty;
	public int DisplayOrder { get; set; }
	public string CreatedAt { get; set; } = string.Empty;
}
