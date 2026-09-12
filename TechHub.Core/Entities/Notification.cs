using System;

namespace TechHub.Core.Entities;

public class Notification
{
	public Guid Id { get; set; }
	public Guid SchoolId { get; set; }
	public Guid RecipientId { get; set; }
	public string Type { get; set; } = string.Empty;
	public string Title { get; set; } = string.Empty;
	public string Body { get; set; } = string.Empty;
	public string? EntityType { get; set; }
	public Guid? EntityId { get; set; }
	public bool IsDelivered { get; set; }
	public bool IsRead { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? ReadAt { get; set; }
}
