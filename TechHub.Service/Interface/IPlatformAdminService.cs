using System.Threading.Tasks;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Platform;

namespace TechHub.Service.Interface;

public interface IPlatformAdminService
{
    Task<BaseResponse> CreatePlatformAdminAsync(CreatePlatformAdminViewModel model, AuthenticatedUserClaims claims);
}
