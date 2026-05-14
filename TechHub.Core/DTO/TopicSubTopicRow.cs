using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.DTO;
// Flat DB projection row
public class TopicSubTopicRow
{
	public Guid TopicId { get; set; }
	public string TopicName { get; set; }
	public bool TopicIsActive { get; set; }
	public string TopicCreatedAt { get; set; }
	public Guid SubTopicId { get; set; }
	public string SubTopicName { get; set; }
	public bool SubTopicIsActive { get; set; }
	public string SubTopicCreatedAt { get; set; }
}

// Nested response shape
public class TopicWithSubTopicsDto
{
	public Guid TopicId { get; set; }
	public string TopicName { get; set; }
	public bool IsActive { get; set; }
	public string CreatedAt { get; set; }
	public List<SubTopicDto2> SubTopics { get; set; } = new();
}

//public class SubTopicDto
//{
//	public Guid SubTopicId { get; set; }
//	public string Name { get; set; }
//	public bool IsActive { get; set; }
//	public string CreatedAt { get; set; }
//}

