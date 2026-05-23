using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Model;

public class JobStatusRow
{
	public Guid JobId { get; set; }
	public string Status { get; set; }  
	public string QuestionType { get; set; }
	public int ExtractedCount { get; set; }  
	public string? FailureReason { get; set; } 
	public int AttemptCount { get; set; }
	public string CreatedAt { get; set; }
	public string? CompletedAt { get; set; }
	public string? SubTopicName { get; set; }
	public string? TopicName { get; set; }
	public Guid? TopicId { get; set; }
}

