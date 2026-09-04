using System;

namespace TechHub.Core.Entities;

public class PasswordChangeConfirmation
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public Guid SchoolId { get; set; }
	public string Token { get; set; } = string.Empty;
	public string PendingHashPassword { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public bool IsUsed { get; set; }
}
