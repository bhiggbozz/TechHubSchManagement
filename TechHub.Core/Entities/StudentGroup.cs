using System;

namespace TechHub.Core.Entities;

public class StudentGroup
{
	public Guid Id { get; set; }
	public Guid SchoolId { get; set; }
	public Guid ClassroomId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty;
	public Guid CreatedBy { get; set; }
	public DateTime CreatedAt { get; set; }
	public Guid? ApprovedBy { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public Guid? RejectedBy { get; set; }
	public string? RejectionReason { get; set; }
	public DateTime? ModifiedAt { get; set; }
	public bool IsActive { get; set; }
}
