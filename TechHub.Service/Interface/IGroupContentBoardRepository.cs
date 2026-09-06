using TechHub.Core.Entities.Board;
using TechHub.Core.Messages;
using TechHub.Core.ViewModels.Board;

namespace TechHub.Service.Interface;

public interface IGroupContentBoardRepository
{
	Task<bool> BatchExistsAsync(string groupId, string studentId, int batchIndex);
	Task SaveBatchAsync(GroupContentBatchMessage message);
	Task SaveManifestAsync(string groupId, string studentId, string schoolId, GroupContentManifestViewModel manifest);

	// ── Status lookups — let the frontend tell "resume" from "already submitted" ──
	Task<int?> GetLatestBatchIndexAsync(string groupId, string studentId);
	Task<GroupContentManifestDocument?> GetManifestAsync(string groupId, string studentId);

	// ── Playback/download ────────────────────────────────────────────────────
	Task<GroupContentBatchDocument?> GetBatchAsync(string groupId, string studentId, int batchIndex);
}
