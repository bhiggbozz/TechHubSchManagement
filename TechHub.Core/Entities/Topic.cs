using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class Topic
{
	public Guid Id { get; set; }
	public Guid SubjectId { get; set; }
	public Guid SchoolId { get; set; }
	public Guid ClassroomId { get; set; }  
	public string Name { get; set; }
	public bool IsActive { get; set; } = false;  // ← default false, active only after approval
	public bool IsDeleted { get; set; } = false;
	public string CreatedAt { get; set; }
	public Guid CreatedBy { get; set; }
}

