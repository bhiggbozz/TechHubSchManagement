using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.DTO;

public class ApprovalItemDto
{
	public Guid Id { get; set; }
	public string OperationType { get; set; }   
	public string EntityType { get; set; }   
	public Guid? EntityId { get; set; }
	public string Status { get; set; }   
	public DateTime CreatedAt { get; set; }
	public DateTime ExpiresAt { get; set; }

	// Requestor info — so the HT knows who submitted it
	public string RequestedByName { get; set; }
	public string RequestedByEmail { get; set; }

	// Lesson-specific enrichment (null for non-lesson operations)
	public LessonSummaryDto Lesson { get; set; }
}
public class LessonSummaryDto
{
	public Guid LessonId { get; set; }
	public string Aim { get; set; }
	public string Description { get; set; }
	public string SubjectName { get; set; }
	public string TopicName { get; set; }
	public string ClassName { get; set; }
	public int MediaCount { get; set; }
	
}

public class ApprovalRespondViewModel
{
	public bool Approved { get; set; }
	public string? RejectionReason { get; set; }  
}
public class ApprovalItemRow
{
	public Guid Id { get; set; }
	public string OperationType { get; set; }
	public string EntityType { get; set; }
	public Guid? EntityId { get; set; }
	public string Status { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public string RequestedByName { get; set; }
	public string RequestedByEmail { get; set; }
	public Guid? LessonId { get; set; }
	public string Aim { get; set; }
	public string Description { get; set; }
	public string SubjectName { get; set; }  // ← correct spelling
	public string TopicName { get; set; }
	public string ClassName { get; set; }
	public int MediaCount { get; set; }
}
