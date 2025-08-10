using TechHubComm.Models;
using ConnectionInfo = TechHubComm.Models.ConnectionInfo;

namespace TechHubComm.Services
{
    public class Interfaces
    {
        public interface IVirtualBoardService
        {
            Task PublishBoardUpdateAsync(BoardUpdateMessage message, string contextId);
            Task<bool> ValidateUserPermissionsAsync(string userId, string role, ClassroomContext context);
            Task ClearBoardAsync(ClassroomContext context, string userId);
        }

        public interface IConnectionManager
        {
            Task AddConnectionAsync(string connectionId, string userId, string role, ClassroomContext context);
            Task RemoveConnectionAsync(string connectionId);
            Task<ConnectionInfo?> GetConnectionAsync(string connectionId);
            Task<List<string>> GetConnectionsInGroupAsync(string groupKey);
            Task<List<string>> GetConnectionsInGroupByRoleAsync(string groupKey, string role);
            Task UpdateLastActivityAsync(string connectionId);
            Task<int> GetGroupConnectionCountAsync(string groupKey);
            Task<int> GetTotalConnectionCountAsync();
        }

        public interface IMessageBroker
        {
            Task PublishAsync(string channel, object message);
            Task SubscribeAsync(string channel, Func<string, Task> handler);
            Task UnsubscribeAsync(string channel);
        }

        public interface IBoardHistoryService
        {
            Task StoreMessageAsync(BoardUpdateMessage message);
            Task<List<BoardUpdateMessage>> GetHistoryAsync(ClassroomContext context, int limit = 100);
            Task ClearHistoryAsync(ClassroomContext context);
        }

        public interface IMetricsService
        {
            Task IncrementMessageCountAsync();
            Task RecordConnectionCountAsync(int count);
            Task RecordLatencyAsync(double latency);
            Task<ServerMetrics> GetMetricsAsync();
        }
    }
}
