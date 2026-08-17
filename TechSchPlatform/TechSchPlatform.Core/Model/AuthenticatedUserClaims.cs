namespace TechSchPlatform.Core.Model;

public class AuthenticatedUserClaims
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? Username { get; set; }
}