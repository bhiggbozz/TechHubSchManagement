using System;

namespace TechHub.Core.Entities;

/// <summary>
/// Audit trail for staff-initiated student password resets — who reset whom, and
/// when. Exists because students have no self-service forgot-password path; every
/// reset is staff-initiated, so this is the record of who acted on whose behalf.
/// </summary>
public class StudentPasswordResetLog
{
	public Guid Id { get; set; }
	public Guid StudentId { get; set; }
	public Guid SchoolId { get; set; }
	public Guid ResetBy { get; set; }
	public string ResetByName { get; set; } = string.Empty;
	public string ResetByRole { get; set; } = string.Empty;
	public DateTime CreatedAt { get; set; }
}
