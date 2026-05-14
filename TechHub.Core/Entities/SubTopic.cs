using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class SubTopic
{
	public Guid Id { get; set; }
	public Guid TopicId { get; set; }
	public Guid SchoolId { get; set; }
	public Guid ClassroomId { get; set; }  
	public string Name { get; set; }
	public bool IsActive { get; set; } = false;  // ← inactive until approved
	public bool IsDeleted { get; set; } = false;
	public string CreatedAt { get; set; }
	public Guid CreatedBy { get; set; }
}

