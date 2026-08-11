using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel;

namespace TechHub.Service.Interface;

/// <summary>
/// Manages which features a school is privileged to use (SchoolFeature table).
/// </summary>
public interface ISchoolFeatureService
{
	/// <summary>
	/// Creates or updates a school feature flag. Idempotent per (SchoolId, FeatureKey).
	/// </summary>
	Task<BaseResponse> SetFeatureAsync(SchoolFeatureViewModel model, AuthenticatedUserClaims claims);

	/// <summary>
	/// Lists features for a school. When <paramref name="schoolId"/> is null the
	/// caller's own school is used.
	/// </summary>
	Task<BaseResponse> GetFeaturesAsync(Guid? schoolId, AuthenticatedUserClaims claims);

	/// <summary>
	/// Returns whether a feature is enabled and active for a school.
	/// </summary>
	Task<bool> IsFeatureEnabledAsync(Guid schoolId, string featureKey);

	/// <summary>
	/// Feature-enabled response used by endpoint gates.
	/// </summary>
	Task<BaseResponse> CheckFeatureAsync(Guid schoolId, string featureKey);
}
