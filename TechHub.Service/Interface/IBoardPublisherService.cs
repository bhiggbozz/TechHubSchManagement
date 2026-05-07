using TechHub.Core.Messages;

namespace TechHub.Service.Interface;

public interface IBoardPublisherService
{
    Task PublishBatchAsync(BoardBatchMessage message);
}
