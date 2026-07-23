namespace TechHub.Core.ViewModel.Platform;

public class RegistrationRequestResponse
{
    public Guid Id { get; set; }
    public string SchoolName { get; set; }
    public string Location { get; set; }
    public string Address { get; set; }
    public string TenantIdentifier { get; set; }
    public string SchoolCode { get; set; }
    public string? LogoUrl { get; set; }
    public string? LogoPublicId { get; set; }
    public string AdminFirstName { get; set; }
    public string AdminLastName { get; set; }
    public string AdminEmail { get; set; }
    public string AdminUsername { get; set; }
    public string Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}
