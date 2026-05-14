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

	public object Payload { get; set; }

	public LessonApprovalPayload LessonPayload { get; set; }

	// Everything else uses this — populated for all other OperationTypes
	public ApprovalPayloadSummary Summary { get; set; }
	// Lesson-specific enrichment (null for non-lesson operations)
	public LessonSummaryDto Lesson { get; set; }
	public SyllabusSummaryDto Syllabus { get; set; }
	public ExaminationSummaryDto Examination { get; set; }
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

//public class LessonSummaryDto
//{
//	public Guid LessonId { get; set; }
//	public string Aim { get; set; }
//	public string Description { get; set; }
//	public string SubjectName { get; set; }
//	public string TopicName { get; set; }
//	public string ClassName { get; set; }
//	public int MediaCount { get; set; }
//}

public class ApprovalPayloadSummary
{
	public string Title { get; set; }
	public string SubjectName { get; set; }
	public string ClassName { get; set; }
	public string Description { get; set; }
	public string Term { get; set; }
	public string ExamDate { get; set; }
	public int? TotalMarks { get; set; }
	public string UserRole { get; set; }
	public string UserName { get; set; }
	public List<Guid> EntityIds { get; set; } = new(); 
}

// ── Lesson-specific — richer shape ───────────────────────────────────────────
public class LessonApprovalPayload
{
	public Guid LessonId { get; set; }
	public string Aim { get; set; }
	public string Description { get; set; }
	public string SubjectName { get; set; }
	public string TopicName { get; set; }
	public string SubTopic { get; set; }
	public string ClassName { get; set; }
	public int MediaCount { get; set; }
	public bool HasRecording { get; set; }

	// Two-layer approval tracking
	public string HeadTeacherApprovalStatus { get; set; }  // Pending | Approved | Rejected
	public string AdminApprovalStatus { get; set; }  // Pending | Approved | Rejected | NotRequired
	public string CurrentApprovalLayer { get; set; }  // HeadTeacher | Admin
}

public class SyllabusSummaryDto
{
	public Guid SyllabusId { get; set; }
	public string Title { get; set; }
	public string SubjectName { get; set; }
	public string ClassName { get; set; }
	public string Term { get; set; }
}

public class ExaminationSummaryDto
{
	public Guid ExaminationId { get; set; }
	public string Title { get; set; }
	public string SubjectName { get; set; }
	public string ClassName { get; set; }
	public string ExamDate { get; set; }
	public int TotalMarks { get; set; }
}

// Flat DB row — base approval fields only
public class ApprovalBaseRow
{
	public Guid Id { get; set; }
	public string OperationType { get; set; }
	public string EntityType { get; set; }
	public Guid? EntityId { get; set; }
	public string Status { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public string Payload { get; set; }
	public string RequestedByName { get; set; }
	public string RequestedByEmail { get; set; }
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
