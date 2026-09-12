using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModels.Board;

namespace TechHub.Service.Interface;

public interface IGroupContentBoardSessionService
{
	Task<BaseResponse> PublishBatchAsync(string routeGroupId, string routeContentId, GroupContentBoardBatchViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> SaveManifestAsync(string routeGroupId, string routeContentId, GroupContentManifestViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetStatusAsync(string routeGroupId, string routeContentId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetManifestForViewAsync(string routeGroupId, string routeContentId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetBatchForViewAsync(string routeGroupId, string routeContentId, int batchIndex, AuthenticatedUserClaims claims);
}
