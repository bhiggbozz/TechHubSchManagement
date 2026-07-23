namespace TechHub.Core.Entities;

public class SchoolRegistrationRequest
{
    public Guid Id { get; set; }
    public string SchoolName { get; set; }
    public string Location { get; set; }
    public int CountryId { get; set; }
    public int StateId { get; set; }
    public string? State { get; set; }
    public string Address { get; set; }
    public bool HasBranch { get; set; }
    public string TenantIdentifier { get; set; }
    public string SchoolCode { get; set; }
    public string? LogoUrl { get; set; }
    public string? LogoPublicId { get; set; }
    public string AdminFirstName { get; set; }
    public string? AdminMiddleName { get; set; }
    public string AdminLastName { get; set; }
    public string AdminEmail { get; set; }
    public string AdminUsername { get; set; }
    public string AdminPassword { get; set; }
    public string Status { get; set; } = "Pending";
    public string? RejectionReason { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}
