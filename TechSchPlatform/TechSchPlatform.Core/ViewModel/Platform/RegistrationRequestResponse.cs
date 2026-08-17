namespace TechSchPlatform.Core.ViewModel.Platform;

public class RegistrationRequestResponse
{
    public Guid Id { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TenantIdentifier { get; set; } = string.Empty;
    public string SchoolCode { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? LogoPublicId { get; set; }
    public string AdminFirstName { get; set; } = string.Empty;
    public string AdminLastName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminUsername { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}