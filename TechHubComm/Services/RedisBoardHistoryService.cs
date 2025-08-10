using static TechHubComm.Services.Interfaces;
using StackExchange.Redis;
using System.Text.Json;
using static TechHubComm.Services.Interfaces;
using TechHubComm.Models;


namespace TechHubComm.Services
{
    
    public class RedisBoardHistoryService : IBoardHistoryService
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisBoardHistoryService> _logger;
        private const string HISTORY_KEY_PREFIX = "history:";
        private const int MAX_HISTORY_SIZE = 1000;

        public RedisBoardHistoryService(IConnectionMultiplexer redis, ILogger<RedisBoardHistoryService> logger)
        {
            _database = redis.GetDatabase();
            _logger = logger;
        }

        public async Task StoreMessageAsync(BoardUpdateMessage message)
        {
            var groupKey = message.ClassroomId + ":" + message.SubjectId + ":" + message.TopicId;
            var historyKey = $"{HISTORY_KEY_PREFIX}{groupKey}";
            var json = JsonSerializer.Serialize(message);

            // Use Redis list for ordered history
            await _database.ListLeftPushAsync(historyKey, json);

            // Trim to max size
            await _database.ListTrimAsync(historyKey, 0, MAX_HISTORY_SIZE - 1);

            // Set expiration
            await _database.KeyExpireAsync(historyKey, TimeSpan.FromHours(24));

            _logger.LogDebug("Message stored in history for {GroupKey}", groupKey);
        }

        public async Task<List<BoardUpdateMessage>> GetHistoryAsync(ClassroomContext context, int limit = 100)
        {
            var historyKey = $"{HISTORY_KEY_PREFIX}{context.GroupKey}";
            var messages = await _database.ListRangeAsync(historyKey, 0, limit - 1);

            var result = new List<BoardUpdateMessage>();
            foreach (var message in messages.Reverse()) // Reverse to get chronological order
            {
                try
                {
                    var boardMessage = JsonSerializer.Deserialize<BoardUpdateMessage>(message);
                    if (boardMessage != null)
                    {
                        result.Add(boardMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize board message");
                }
            }

            return result;
        }

        public async Task ClearHistoryAsync(ClassroomContext context)
        {
            var historyKey = $"{HISTORY_KEY_PREFIX}{context.GroupKey}";
            await _database.KeyDeleteAsync(historyKey);
            _logger.LogDebug("History cleared for {GroupKey}", context.GroupKey);
        }
    }
}
