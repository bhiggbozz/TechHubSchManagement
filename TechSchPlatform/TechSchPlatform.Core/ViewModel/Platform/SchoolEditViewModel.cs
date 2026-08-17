namespace TechSchPlatform.Core.ViewModel.Platform;

public class SchoolEditViewModel
{
    public string? SchoolName { get; set; }
    public string? Location { get; set; }
    public int CountryId { get; set; }
    public int StateId { get; set; }
    public string? State { get; set; }
    public string? Address { get; set; }
    public bool? HasBranch { get; set; }
    public bool? IsActive { get; set; }
    public string? LogoUrl { get; set; }
    public string? LogoPublicId { get; set; }
}