using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class EmailTemplate
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Subject { get; set; } = string.Empty;	
	public string HtmlBody { get; set; } = string.Empty;
	public bool IsActive { get; set; } = true;
	public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
	public string ModifiedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}

