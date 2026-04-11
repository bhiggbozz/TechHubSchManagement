using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class RefreshTokens
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public Guid SchoolId { get; set; }
	public string Token { get; set; }
	public DateTime ExpiresAt { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? RevokedAt { get; set; }
	public string ReplacedByToken { get; set; }
	public bool IsRevoked { get; set; }

	
	//public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

	
	//public bool IsActive => !IsRevoked && !IsExpired;
}
