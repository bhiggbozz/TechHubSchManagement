namespace TechSchPlatform.Core.ViewModel.Platform;

public class CreateSchoolViewModel
{
    public string SchoolName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int CountryId { get; set; }
    public int StateId { get; set; }
    public string? State { get; set; }
    public string Address { get; set; } = string.Empty;
    public bool HasBranch { get; set; }
    public bool ISActive { get; set; } = true;
    public string? LogoUrl { get; set; }
    public string? LogoPublicId { get; set; }
}