using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.DTO;

public class SubjectDto
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public string Code { get; set; }
	public bool IsActive { get; set; }
}

public class TopicDto
{
	public Guid Id { get; set; }
	public Guid SubjectId { get; set; }
	public string Name { get; set; }
	public bool IsActive { get; set; }
}

public class SubTopicDto
{
	public Guid Id { get; set; }
	public Guid TopicId { get; set; }
	public string Name { get; set; }
	public bool IsActive { get; set; }
}
public class SubTopicDto2
{
	public Guid SubTopicId { get; set; }
	public string Name { get; set; }
	public bool IsActive { get; set; }
	public string CreatedAt { get; set; }
}
