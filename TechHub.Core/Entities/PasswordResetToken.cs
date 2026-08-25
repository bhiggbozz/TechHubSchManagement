using System;

namespace TechHub.Core.Entities;

public class PasswordResetToken
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public Guid SchoolId { get; set; }
	public string Token { get; set; }
	public DateTime ExpiresAt { get; set; }
	public DateTime CreatedAt { get; set; }
	public bool IsUsed { get; set; }
}
