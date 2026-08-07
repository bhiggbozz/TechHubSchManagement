using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Platform;

namespace TechHub.Service.Interface;

public interface IPlatformAdminService
{
    /// <summary>
    /// Create a platform user (PlatformSuperAdmin/PlatformAdmin/PlatformUser) subject to
    /// role hierarchy. PlatformSuperAdmin can create all roles; PlatformAdmin can only
    /// create PlatformUser accounts.
    /// </summary>
    Task<BaseResponse> CreatePlatformUserAsync(CreatePlatformAdminViewModel model, AuthenticatedUserClaims claims);

    Task<BaseResponse> GetPlatformUsersAsync(AuthenticatedUserClaims claims);

    Task<BaseResponse> GetPlatformLoginHistoryAsync(Guid? userId, int pageNumber, int pageSize);
}