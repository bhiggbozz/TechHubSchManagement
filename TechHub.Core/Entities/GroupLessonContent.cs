using System;

namespace TechHub.Core.Entities;

public class GroupLessonContent
{
	public Guid Id { get; set; }
	public Guid SchoolId { get; set; }
	public Guid GroupId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid? TopicId { get; set; }
	public string? SubTopic { get; set; }
	public string Aim { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public Guid CreatedBy { get; set; }
	public Guid? ApprovedBy { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public Guid? RejectedBy { get; set; }
	public string? RejectionReason { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? ModifiedAt { get; set; }
}
