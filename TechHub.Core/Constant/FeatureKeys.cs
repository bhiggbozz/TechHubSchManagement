namespace TechHub.Core.Constant;

/// <summary>
/// Well-known feature keys stored in the SchoolFeature table.
/// A school must have the key present (IsEnabled = 1, IsActive = 1) before
/// the corresponding capability can be used.
/// </summary>
public static class FeatureKeys
{
	/// <summary>AI-generated teaching images attached to lessons.</summary>
	public const string ImageGeneration = "ai_image_generation";
}
