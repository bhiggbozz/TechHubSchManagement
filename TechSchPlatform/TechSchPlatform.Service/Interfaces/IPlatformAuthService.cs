using TechSchPlatform.Core;
using TechSchPlatform.Core.ViewModel.Platform;

namespace TechSchPlatform.Service.Interfaces;

public interface IPlatformAuthService
{
    Task<BaseResponse> LoginAsync(LoginPlatformViewModel model);
}