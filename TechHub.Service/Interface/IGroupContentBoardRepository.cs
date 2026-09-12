using TechHub.Core.Entities.Board;
using TechHub.Core.Messages;
using TechHub.Core.ViewModels.Board;

namespace TechHub.Service.Interface;

public interface IGroupContentBoardRepository
{
	Task<bool> BatchExistsAsync(string groupId, string studentId, string contentId, int batchIndex);
	Task SaveBatchAsync(GroupContentBatchMessage message);
	Task SaveManifestAsync(string groupId, string studentId, string contentId, string schoolId, GroupContentManifestViewModel manifest);

	// ── Status lookups — let the frontend tell "resume" from "already recorded" ──
	Task<int?> GetLatestBatchIndexAsync(string groupId, string studentId, string contentId);
	Task<GroupContentManifestDocument?> GetManifestAsync(string groupId, string studentId, string contentId);

	// ── Playback/download ────────────────────────────────────────────────────
	Task<GroupContentBatchDocument?> GetBatchAsync(string groupId, string studentId, string contentId, int batchIndex);
}
