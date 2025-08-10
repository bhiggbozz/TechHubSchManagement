using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Text.Json;
using TechHubComm.Models;
using static TechHubComm.Services.Interfaces;
using ConnectionInfo = TechHubComm.Models.ConnectionInfo;

namespace TechHubComm.Hub
{
    public class VirtualBoardHub : Microsoft.AspNetCore.SignalR.Hub
    {
        private readonly IVirtualBoardService _boardService;
        private readonly IConnectionManager _connectionManager;
        private readonly IBoardHistoryService _historyService;
        private readonly IMessageBroker _messageBroker;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<VirtualBoardHub> _logger;

        public VirtualBoardHub(
            IVirtualBoardService boardService,
            IConnectionManager connectionManager,
            IBoardHistoryService historyService,
            IMessageBroker messageBroker,
            IMetricsService metricsService,
            ILogger<VirtualBoardHub> logger)
        {
            _boardService = boardService;
            _connectionManager = connectionManager;
            _historyService = historyService;
            _messageBroker = messageBroker;
            _metricsService = metricsService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {


            // 1. Extract and validate input (secure alternative to query params)
            var (userId, role, schoolId) = await SecureGetUserContextAsync();
            var context = ExtractContextFromPath();

            if (context == null || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(schoolId))
            {
                await Clients.Caller.SendAsync("Error", "Invalid connection parameters");
                Context.Abort();
                return;
            }

            // 2. Parallelize I/O-bound work
            var validationTask = _boardService.ValidateUserPermissionsAsync(userId, role, context);
            var historyTask = _historyService.GetHistoryAsync(context);

            if (!await validationTask)
            {
                await Clients.Caller.SendAsync("Error", "Access denied");
                Context.Abort();
                return;
            }

            // 3. Atomic connection setup
            var groupKey = context.GroupKey;
            await Task.WhenAll(
                _connectionManager.AddConnectionAsync(Context.ConnectionId, userId, role, context),
                Groups.AddToGroupAsync(Context.ConnectionId, groupKey)
            );

            // 4. Fire-and-forget background tasks (with error handling)
            _ = SafeExecuteAsync(async () =>
            {
                await _messageBroker.SubscribeAsync(
                    $"board:{context.ClassroomId}:{context.SubjectId}:{context.TopicId}",
                    async (message) =>
                    {
                        var boardMessage = JsonSerializer.Deserialize<BoardUpdateMessage>(message);
                        if (boardMessage != null)
                            await Clients.Group(groupKey).SendAsync("BoardUpdate", boardMessage);
                    });
            });

            // 5. Respond to caller and group
            var history = await historyTask;
            await Task.WhenAll(
                Clients.Caller.SendAsync("BoardHistory", history),
                Clients.OthersInGroup(groupKey).SendAsync("UserJoined", new { userId, role })
            );

            // 6. Metrics (non-blocking)
            _ = SafeExecuteAsync(async () =>
            {
                var connectionCount = await _connectionManager.GetTotalConnectionCountAsync();
                await _metricsService.RecordConnectionCountAsync(connectionCount);
            });

            _logger.LogInformation("User {UserId} connected to {GroupKey}", userId, groupKey);
            await base.OnConnectedAsync();


            //var context = ExtractContextFromPath();
            //var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString() ?? "";
            //var role = Context.GetHttpContext()?.Request.Query["role"].ToString() ?? "Student";
            //var schoolId = Context.GetHttpContext()?.Request.Query["schoolId"].ToString() ?? "";

            //if (context == null || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(schoolId))
            //{
            //    await Clients.Caller.SendAsync("Error", "Invalid connection parameters");
            //    Context.Abort();
            //    return;
            //}

            //context.SchoolId = schoolId;

            //// Validate permissions
            //var hasPermission = await _boardService.ValidateUserPermissionsAsync(userId, role, context);
            //if (!hasPermission)
            //{
            //    await Clients.Caller.SendAsync("Error", "Access denied");
            //    Context.Abort();
            //    return;
            //}

            //// Add connection
            //await _connectionManager.AddConnectionAsync(Context.ConnectionId, userId, role, context);
            //await Groups.AddToGroupAsync(Context.ConnectionId, context.GroupKey);

            //// Subscribe to cross-server messages
            //await _messageBroker.SubscribeAsync($"board:{context.ClassroomId}:{context.SubjectId}:{context.TopicId}",
            //    async (message) =>
            //    {
            //        var boardMessage = System.Text.Json.JsonSerializer.Deserialize<BoardUpdateMessage>(message);
            //        if (boardMessage != null)
            //        {
            //            await Clients.Group(context.GroupKey).SendAsync("BoardUpdate", boardMessage);
            //        }
            //    });

            //// Send board history
            //var history = await _historyService.GetHistoryAsync(context);
            //await Clients.Caller.SendAsync("BoardHistory", history);

            //// Notify others
            //await Clients.OthersInGroup(context.GroupKey).SendAsync("UserJoined", new { userId, role });

            //// Update metrics
            //var connectionCount = await _connectionManager.GetTotalConnectionCountAsync();
            //await _metricsService.RecordConnectionCountAsync(connectionCount);

            //_logger.LogInformation("User {UserId} from school {SchoolId} connected to {GroupKey}",
            //    userId, schoolId, context.GroupKey);

            //await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionInfo = await _connectionManager.GetConnectionAsync(Context.ConnectionId);

            if (connectionInfo is not null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, connectionInfo?.Context.GroupKey);
                await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);

                // Unsubscribe from cross-server messages
                await _messageBroker.UnsubscribeAsync($"board:{connectionInfo?.Context.ClassroomId}:{connectionInfo?.Context.SubjectId}:{connectionInfo?.Context.TopicId}");

                // Notify others
                await Clients.OthersInGroup(connectionInfo?.Context.GroupKey)
                    .SendAsync("UserLeft", new { userId = connectionInfo?.UserId, role = connectionInfo?.UserRole });

                // Update metrics
                var connectionCount = await _connectionManager.GetTotalConnectionCountAsync();
                await _metricsService.RecordConnectionCountAsync(connectionCount);

                _logger.LogInformation("User {UserId} disconnected from {GroupKey}",
                    connectionInfo?.UserId, connectionInfo?.Context.GroupKey);
            }

            await base.OnDisconnectedAsync(exception);
        }

        [HubMethodName("SendBoardUpdate")]
        public async Task SendBoardUpdateAsync(BoardUpdateMessage message)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Validate connection
                var connectionInfo = await _connectionManager.GetConnectionAsync(Context.ConnectionId);
                if (connectionInfo is null)
                {
                    await Clients.Caller.SendAsync("Error", "Connection not found");
                    return;
                }

                // Update activity (fire-and-forget)
                _ = _connectionManager.UpdateLastActivityAsync(Context.ConnectionId);

                // Enrich message
                message = EnrichMessage(message, connectionInfo);

                // Handle clear action
                if (message.Action.Type == "clear")
                {
                    if (!IsTeacher(connectionInfo))
                    {
                        await Clients.Caller.SendAsync("Error", "Insufficient permissions");
                        return;
                    }

                    await _boardService.ClearBoardAsync(connectionInfo.Context, connectionInfo.UserId);
                    return;
                }

                // Publish and record metrics in parallel
                await Task.WhenAll(
                    _boardService.PublishBoardUpdateAsync(message, Context.ConnectionId),
                    _metricsService.RecordLatencyAsync(stopwatch.ElapsedMilliseconds)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Board update failed for {ConnectionId}", Context.ConnectionId);
                await Clients.Caller.SendAsync("Error", "Failed to process update");
            }
        }

        [HubMethodName("RequestBoardHistory")]
        public async Task RequestBoardHistoryAsync()
        {
            var connectionInfo = await _connectionManager.GetConnectionAsync(Context.ConnectionId);
            if (connectionInfo is not null)
            {
                var history = await _historyService.GetHistoryAsync(connectionInfo?.Context);
                await Clients.Caller.SendAsync("BoardHistory", history);
            }
        }

        [HubMethodName("ClearBoard")]
        public async Task ClearBoardAsync()
        {
            var connectionInfo = await _connectionManager.GetConnectionAsync(Context.ConnectionId);
            if (connectionInfo?.UserRole.Equals("Teacher", StringComparison.OrdinalIgnoreCase) == true)
            {
                await _boardService.ClearBoardAsync(connectionInfo?.Context, connectionInfo?.UserId);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "Only teachers can clear the board");
            }
        }

        [HubMethodName("GetConnectionStats")]
        public async Task GetConnectionStatsAsync()
        {
            var connectionInfo = await _connectionManager.GetConnectionAsync(Context.ConnectionId);
            if (connectionInfo is not null)
            {
                var groupCount = await _connectionManager.GetGroupConnectionCountAsync(connectionInfo?.Context.GroupKey);
                var totalCount = await _connectionManager.GetTotalConnectionCountAsync();
                var metrics = await _metricsService.GetMetricsAsync();

                await Clients.Caller.SendAsync("ConnectionStats", new
                {
                    GroupConnections = groupCount,
                    TotalConnections = totalCount,
                    ServerMetrics = metrics
                });
            }
        }

        // Extract context from the connection URL path
        private ClassroomContext? ExtractContextFromPath()
        {
            try
            {
                var httpContext = Context.GetHttpContext();
                var path = httpContext?.Request.Path.Value;

                if (string.IsNullOrEmpty(path))
                    return null;

                // Expected path format: /virtualboard/{classroomId}/{subjectId}/{topicId}
                // or query parameters: ?classroomId=123&subjectId=456&topicId=789

                // First try to get from query parameters
                var query = httpContext?.Request.Query;
                if (query != null)
                {
                    var classroomIdQuery = query["classroomId"].ToString();
                    var subjectIdQuery = query["subjectId"].ToString();
                    var topicIdQuery = query["topicId"].ToString();

                    if (!string.IsNullOrEmpty(classroomIdQuery) &&
                        !string.IsNullOrEmpty(subjectIdQuery) &&
                        !string.IsNullOrEmpty(topicIdQuery))
                    {
                        return new ClassroomContext
                        {
                            ClassroomId = classroomIdQuery,
                            SubjectId = subjectIdQuery,
                            TopicId = topicIdQuery,
                           // GroupKey = $"board_{classroomIdQuery}_{subjectIdQuery}_{topicIdQuery}"
                        };
                    }
                }

                // If not in query, try to extract from path
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

                // Expected: ["virtualboard", "{classroomId}", "{subjectId}", "{topicId}"]
                if (segments.Length >= 4 && segments[0].Equals("virtualboard", StringComparison.OrdinalIgnoreCase))
                {
                    var classroomId = segments[1];
                    var subjectId = segments[2];
                    var topicId = segments[3];

                    return new ClassroomContext
                    {
                        ClassroomId = classroomId,
                        SubjectId = subjectId,
                        TopicId = topicId,
                        //GroupKey = $"board_{classroomId}_{subjectId}_{topicId}"
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract context from path");
                return null;
            }
        }

        private async Task<(string userId, string role, string schoolId)> SecureGetUserContextAsync()
        {
            var httpContext = Context.GetHttpContext();
            // Prefer headers/JWT over query params
            return (
                httpContext?.Request.Headers["UserId"].ToString() ?? "",
                httpContext?.Request.Headers["Role"].ToString() ?? "Student",
                httpContext?.Request.Headers["SchoolId"].ToString() ?? ""
            );
        }
        private async Task SafeExecuteAsync(Func<Task> action)
        {
            try { await action(); }
            catch (Exception ex) { _logger.LogError(ex, "Background task failed"); }
        }
        private BoardUpdateMessage EnrichMessage(BoardUpdateMessage message, Models.ConnectionInfo connectionInfo)
        {
            var enriched = new BoardUpdateMessage
            {
                // Copy all properties from original message
               // Content = message.Content,
                Action = message.Action,
                // ... other existing properties

                // Add enrichment
                UserId = connectionInfo.UserId,
                UserRole = connectionInfo.UserRole,
                ClassroomId = connectionInfo.Context.ClassroomId,
                SubjectId = connectionInfo.Context.SubjectId,
                TopicId = connectionInfo.Context.TopicId,
                Timestamp = DateTime.UtcNow
            };
            return enriched;

        }
        private bool IsTeacher(ConnectionInfo connectionInfo)
        {
            // Case-insensitive check for "Teacher" role
            return string.Equals(connectionInfo?.UserRole, "Teacher", StringComparison.OrdinalIgnoreCase);
        }
    }
}