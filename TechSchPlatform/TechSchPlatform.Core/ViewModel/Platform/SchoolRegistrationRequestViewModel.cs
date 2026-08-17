namespace TechSchPlatform.Core.ViewModel.Platform;

public class SchoolRegistrationRequestViewModel
{
    public string SchoolName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int CountryId { get; set; }
    public int StateId { get; set; }
    public string? State { get; set; }
    public string Address { get; set; } = string.Empty;
    public bool HasBranch { get; set; }
    public string TenantIdentifier { get; set; } = string.Empty;
    public string SchoolCode { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? LogoPublicId { get; set; }
    public string AdminFirstName { get; set; } = string.Empty;
    public string? AdminMiddleName { get; set; }
    public string AdminLastName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminUsername { get; set; } = string.Empty;
}