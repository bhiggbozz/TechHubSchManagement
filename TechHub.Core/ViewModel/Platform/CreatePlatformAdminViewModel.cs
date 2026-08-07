using TechHub.Core.Enum;

namespace TechHub.Core.ViewModel.Platform;

public class CreatePlatformAdminViewModel
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Role { get; set; } = PlatformRole.PlatformAdmin.ToString();
}