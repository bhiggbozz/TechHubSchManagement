using System;

namespace TechHub.Core.Entities;

public class PlatformLoginHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlatformUserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool PasswordFailed { get; set; }
    public string? DeviceType { get; set; }
    public string? DeviceIp { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}