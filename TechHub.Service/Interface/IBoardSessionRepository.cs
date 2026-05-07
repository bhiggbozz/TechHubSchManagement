using TechHub.Core.Entities.Board;
using TechHub.Core.Messages;
using TechHub.Core.ViewModels.Board.Manifest;

namespace TechHub.Service.Interface;

public interface IBoardSessionRepository
{
    Task SaveBatchAsync(BoardBatchMessage message);
    Task SaveManifestAsync(string sessionId, SessionManifestViewModel manifest);
    Task<BoardSession?> GetSessionAsync(string sessionId, string schoolId);
    Task UpdateAudioFinalUrlAsync(string sessionId, string audioFinalUrl);
    Task MarkCompletedAsync(string sessionId);
    Task<bool> BatchExistsAsync(string sessionId, int batchIndex);
}
