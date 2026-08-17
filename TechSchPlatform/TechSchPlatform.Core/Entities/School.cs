namespace TechSchPlatform.Core.Entities;

public class School
{
    public Guid Id { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int CountryId { get; set; }
    public int StateId { get; set; }
    public string? State { get; set; }
    public string Address { get; set; } = string.Empty;
    public bool HasBranch { get; set; }
    public string? LogoUrl { get; set; }
    public string? LogoPublicId { get; set; }
    public bool ISActive { get; set; } = true;
    public Guid? CreatedBy { get; set; }
    public Guid? ModifiedBy { get; set; }
    public string CreationDate { get; set; } = string.Empty;
    public string ModifiedDate { get; set; } = string.Empty;
}