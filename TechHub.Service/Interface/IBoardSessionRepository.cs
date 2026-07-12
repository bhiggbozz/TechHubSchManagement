using TechHub.Core.Entities.Board;
using TechHub.Core.Messages;
using TechHub.Core.ViewModels.Board;
using TechHub.Core.ViewModels.Board.Manifest;

namespace TechHub.Service.Interface;

public interface IBoardSessionRepository
{
	Task SaveBatchAsync(BoardBatchMessage message);
	Task SaveManifestAsync(string sessionId, SessionManifestViewModel manifest);
	Task<BoardManifest?> GetManifestAsync(string sessionId, string schoolId);
	Task<BoardBatchDocument?> GetBatchByIndexKeyAsync(string indexKey);
	Task<bool> BatchExistsAsync(string sessionId, int batchIndex);
	Task UpdateAudioFinalUrlAsync(string sessionId, string audioFinalUrl);
	Task MarkCompletedAsync(string sessionId);
	Task<BoardManifest?> GetSessionAsync(string sessionId, string schoolId);
	Task SaveStudentBatchAsync(string sessionId, int boardIndex, List<StrokeViewModel> strokes, string schoolId, string studentId);
	Task<BoardBatchDocument?> GetStudentBatchAsync(string sessionId, int boardIndex);
}
