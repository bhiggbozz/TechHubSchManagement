using Microsoft.AspNetCore.SignalR;
using TechHubComm.Hub;
using TechHubComm.Models;
using static TechHubComm.Services.Interfaces;

namespace TechHubComm.Services
{
    public class VirtualBoardService : IVirtualBoardService
    {
        private readonly IBoardHistoryService _historyService;
        private readonly IMessageBroker _messageBroker;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<VirtualBoardService> _logger;
        private readonly IConnectionManager _connectionManager;
        private readonly IHubContext<VirtualBoardHub> _hubContext;

        public VirtualBoardService(
            IBoardHistoryService historyService,
            IMessageBroker messageBroker,
            IMetricsService metricsService,
            ILogger<VirtualBoardService> logger,IConnectionManager connectionManager, IHubContext<VirtualBoardHub> hubContext)
        {
            _historyService = historyService;
            _messageBroker = messageBroker;
            _metricsService = metricsService;
            _logger = logger;
            _connectionManager = connectionManager;
            _hubContext = hubContext;
        }

            public async Task PublishBoardUpdateAsync(BoardUpdateMessage message, string contextId)
            {
                string? groupKey = string.Empty;
               try
               {
                        // 1. Validate message and connection (sync checks first)
                   if (string.IsNullOrEmpty(message.ClassroomId)||string.IsNullOrEmpty(message.SubjectId)||string.IsNullOrEmpty(message.TopicId))
                   {
                          throw new ArgumentException("Invalid classroom context");
                   }

                var connectionInfo = await _connectionManager.GetConnectionAsync(contextId);
                if (connectionInfo == null) throw new InvalidOperationException("Invalid connection");

                // 2. Generate group key from context (reuse connectionInfo's context)
                groupKey = $"{connectionInfo.Context.ClassroomId}:{connectionInfo.Context.SubjectId}:{connectionInfo.Context.TopicId}";

                // 3. Parallelize all I/O operations
                var storeTask = _historyService.StoreMessageAsync(message);
                var brokerTask = _messageBroker.PublishAsync($"board:{groupKey}", message);
                var metricsTask = _metricsService.IncrementMessageCountAsync();

                // 4. Wait for storage and broker to complete (metrics can lag)
                await Task.WhenAll(storeTask, brokerTask)
                    .WaitAsync(TimeSpan.FromSeconds(5));

                // 5. Broadcast to SignalR group AFTER ensuring DB persistence
                await _hubContext.Clients.Group(groupKey).SendAsync("BoardUpdate", message);

                // 6. Metrics can complete in background
                _ = metricsTask.ContinueWith(t => 
                    _logger.LogWarning("Metrics delayed: {Exception}", t.Exception),
                    TaskContinuationOptions.OnlyOnFaulted
                );

                _logger.LogDebug("Update processed for {GroupKey}", groupKey);
               }
                catch (TimeoutException tex)
                {
                    _logger.LogWarning(tex, "Processing timed out for {ConnectionId}", contextId);
                    await _hubContext.Clients.Group(groupKey).SendAsync("Error", "Operation timed out");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Update failed for {ConnectionId}", contextId);
                    await _hubContext.Clients.Group(groupKey).SendAsync("Error", "Failed to process update");
                }
                //try
                //{
                //    // Validate message
                //    if (string.IsNullOrEmpty(message.ClassroomId) ||
                //        string.IsNullOrEmpty(message.SubjectId) ||
                //        string.IsNullOrEmpty(message.TopicId))
                //    {
                //        throw new ArgumentException("Invalid classroom context");
                //    }

                //    // Store in history
                //    await _historyService.StoreMessageAsync(message);

                //    // Publish to message broker for cross-server communication
                //    await _messageBroker.PublishAsync($"board:{message.ClassroomId}:{message.SubjectId}:{message.TopicId}", message);

                //    // Record metrics
                //    await _metricsService.IncrementMessageCountAsync();

                //    _logger.LogDebug("Published board update for {GroupKey}",
                //        $"{message.ClassroomId}:{message.SubjectId}:{message.TopicId}");
                //}
                //catch (Exception ex)
                //{
                //    _logger.LogError(ex, "Error publishing board update");
                //    throw;
                //}
            }

        public async Task<bool> ValidateUserPermissionsAsync(string userId, string role, ClassroomContext context)
        {
            // Implement your authorization logic here
            // Consider caching permissions in Redis for better performance
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(role))
                return false;

            // Teachers can always draw
            if (role.Equals("Teacher", StringComparison.OrdinalIgnoreCase))
                return true;

            // Add your student permission logic here
            // You might want to check against a database or cache
            return true; // Placeholder
        }

        public async Task ClearBoardAsync(ClassroomContext context, string userId)
        {
            await _historyService.ClearHistoryAsync(context);

            var clearMessage = new BoardUpdateMessage
            {
                ClassroomId = context.ClassroomId,
                SubjectId = context.SubjectId,
                TopicId = context.TopicId,
                UserId = userId,
                UserRole = "Teacher",
                Action = new BoardAction { Type = "clear" }
            };

            await _messageBroker.PublishAsync($"board:{context.ClassroomId}:{context.SubjectId}:{context.TopicId}", clearMessage);

            _logger.LogInformation("Board cleared for {GroupKey} by user {UserId}", context.GroupKey, userId);
        }
    }
}
