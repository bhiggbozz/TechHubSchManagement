using System;
using System.Collections.Generic;

namespace TechHub.Core.DTO
{
	public class NotificationDto
	{
		public Guid Id { get; set; }
		public string Type { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string Body { get; set; } = string.Empty;
		public string? EntityType { get; set; }
		public Guid? EntityId { get; set; }
		public bool IsRead { get; set; }
		public bool IsDelivered { get; set; }
		public DateTime CreatedAt { get; set; }
	}

	public class NotificationListDto
	{
		public List<NotificationDto> Items { get; set; } = new();
		public int UnreadCount { get; set; }
	}
}
