using TechHub.Core;
using TechHub.Core.Entities.Board;
using TechHub.Core.Model;
using TechHub.Core.ViewModels.Board;
using TechHub.Core.ViewModels.Board.Manifest;

namespace TechHub.Service.Interface;

public interface IBoardSessionService
{
    Task PublishBatchAsync(string routeSessionId, BoardBatchViewModel model, AuthenticatedUserClaims claims);
    Task<BaseResponse> SaveManifestAsync(string routeSessionId, SessionManifestViewModel model, AuthenticatedUserClaims claims);
    Task<BoardSession?> GetSessionAsync(string sessionId, string schoolId);
	Task<BaseResponse> GetManifest(string sessionId, AuthenticatedUserClaims claims);
	Task<BaseResponse> GetBatch(string sessionId, string indexKey, AuthenticatedUserClaims claims);
}
