using System;

namespace TechHub.Core.Entities;

public class StudentGroupMember
{
	public Guid Id { get; set; }
	public Guid GroupId { get; set; }
	public Guid StudentId { get; set; }
	public Guid SchoolId { get; set; }
	public Guid InvitedBy { get; set; }
	public DateTime CreatedAt { get; set; }
	public bool IsActive { get; set; }
}
