namespace TechHub.Core.Entities;

/// <summary>
/// Audit trail for platform-level actions (school edits, school approvals/rejections,
/// platform user creation, etc.). Kept as a dedicated table so platform "who did what"
/// can be traced and answers the audit requirement for school operations.
/// </summary>
public class PlatformAuditLog
{
    public Guid Id { get; set; }

    /// <summary>Platform user who performed the action.</summary>
    public Guid ActorId { get; set; }

    /// <summary>Actor display name captured at time of action.</summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Actor platform role (PlatformSuperAdmin/PlatformAdmin/PlatformUser).</summary>
    public string ActorRole { get; set; } = string.Empty;

    /// <summary>Action type (ApproveSchool, RejectSchool, EditSchool, CreatePlatformUser, ...).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Entity affected (School, SchoolRegistrationRequest, PlatformUser, ...).</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Id of the affected entity.</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Human readable description of the change.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>JSON snapshot of the change (before/after values, etc.).</summary>
    public string? DetailsJson { get; set; }

    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}