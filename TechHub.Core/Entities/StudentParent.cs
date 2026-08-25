using System;

namespace TechHub.Core.Entities;

public class StudentParent
{
	public Guid Id { get; set; }
	public Guid StudentId { get; set; }
	public Guid ParentId { get; set; }
	public Guid SchoolId { get; set; }
	public Guid CreatedBy { get; set; }
	public DateTime CreatedAt { get; set; }
	public bool IsActive { get; set; }
}
