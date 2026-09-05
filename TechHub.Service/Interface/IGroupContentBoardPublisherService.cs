using TechHub.Core.Messages;

namespace TechHub.Service.Interface;

public interface IGroupContentBoardPublisherService
{
	Task PublishBatchAsync(GroupContentBatchMessage message);
}
