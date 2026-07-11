using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Platform;

namespace TechHub.Service.Interface;

public interface IPlatformAuthService
{
    Task<BaseResponse> LoginAsync(LoginPlatformViewModel model);
}
