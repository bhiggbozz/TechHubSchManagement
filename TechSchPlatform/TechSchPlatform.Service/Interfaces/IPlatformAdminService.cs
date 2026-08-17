using TechSchPlatform.Core;
using TechSchPlatform.Core.Model;
using TechSchPlatform.Core.ViewModel.Platform;

namespace TechSchPlatform.Service.Interfaces;

public interface IPlatformAdminService
{
    Task<BaseResponse> CreatePlatformUserAsync(CreatePlatformAdminViewModel model, AuthenticatedUserClaims claims);
    Task<BaseResponse> GetPlatformUsersAsync(AuthenticatedUserClaims claims);
    Task<BaseResponse> GetPlatformLoginHistoryAsync(Guid? userId, int pageNumber, int pageSize);
}