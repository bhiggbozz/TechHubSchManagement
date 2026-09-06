using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModels.Board;

namespace TechHub.Service.Interface;

public interface IGroupContentBoardSessionService
{
	Task<BaseResponse> PublishBatchAsync(string routeGroupId, GroupContentBoardBatchViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> SaveManifestAsync(string routeGroupId, GroupContentManifestViewModel model, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetStatusAsync(string routeGroupId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetManifestForViewAsync(string routeGroupId, string targetStudentId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetBatchForViewAsync(string routeGroupId, string targetStudentId, int batchIndex, AuthenticatedUserClaims claims);
}
