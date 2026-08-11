using System;

namespace TechHub.Core.Entities;

/// <summary>
/// Feature-flag configuration for a school.
/// One row per (SchoolId, FeatureKey) — decides which premium/capability
/// features a school is privileged to use.
/// </summary>
public class SchoolFeature
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid SchoolId { get; set; }

	/// <summary>
	/// Stable feature identifier, e.g. "ai_image_generation".
	/// See TechHub.Core.Constant.FeatureKeys.
	/// </summary>
	public string FeatureKey { get; set; } = string.Empty;

	/// <summary>
	/// Whether the school is privileged to use this feature.
	/// </summary>
	public bool IsEnabled { get; set; }

	/// <summary>
	/// Optional JSON payload for per-school feature tuning
	/// (e.g. daily quota, allowed agent, model overrides).
	/// </summary>
	public string? ConfigurationJson { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }
	public Guid? CreatedBy { get; set; }
	public bool IsActive { get; set; } = true;
}
