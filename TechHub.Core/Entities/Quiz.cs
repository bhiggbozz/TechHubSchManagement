using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class Quiz
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Code { get; set; } = string.Empty;
	public Guid SchoolId { get; set; }
	public Guid CreatedBy { get; set; }
	public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
	public string ModifiedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
	public bool IsActive { get; set; } = true;
}
