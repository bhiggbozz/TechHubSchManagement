using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.DTO;

public class StatusCountRow
{
	public int Status { get; set; }
	public int QuestionCount { get; set; }
}

public class SubTopicDetailCountRow
{
	public Guid SubTopicId { get; set; }
	public string SubTopicName { get; set; }
	public Guid TopicId { get; set; }
	public string TopicName { get; set; }
	public int QuestionCount { get; set; }
	public int DraftCount { get; set; }
	public int PublishedCount { get; set; }
	public int PendingReviewCount { get; set; }
}

