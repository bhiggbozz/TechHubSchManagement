using static TechHubComm.Services.Interfaces;
using TechHubComm.Models;
using StackExchange.Redis;
using TechHubComm.Models;
using System.Text.Json;
using ConnectionInfo = TechHubComm.Models.ConnectionInfo;


namespace TechHubComm.Services
{
       
    public class RedisConnectionManager : IConnectionManager
    {
        private readonly IDatabase _database;
        private readonly ILogger<RedisConnectionManager> _logger;
        private readonly string _serverInstance;
        private const string CONNECTION_KEY_PREFIX = "conn:";
        private const string GROUP_KEY_PREFIX = "group:";
        private const string SERVER_CONNECTIONS_KEY = "server:connections:";

        public RedisConnectionManager(IConnectionMultiplexer redis, ILogger<RedisConnectionManager> logger)
        {
            _database = redis.GetDatabase();
            _logger = logger;
            _serverInstance = Environment.MachineName;
        }

        public async Task AddConnectionAsync(string connectionId, string userId, string role, ClassroomContext context)
        {
            var connectionInfo = new Models.ConnectionInfo
            {
                ConnectionId = connectionId,
                UserId = userId,
                UserRole = role,
                Context = context,
                ConnectedAt = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                ServerInstance = _serverInstance
            };

            var json = JsonSerializer.Serialize(connectionInfo);

            // Store connection info
            await _database.StringSetAsync($"{CONNECTION_KEY_PREFIX}{connectionId}", json, TimeSpan.FromHours(24));

            // Add to group set
            await _database.SetAddAsync($"{GROUP_KEY_PREFIX}{context.GroupKey}", connectionId);

            // Track connections per server
            await _database.SetAddAsync($"{SERVER_CONNECTIONS_KEY}{_serverInstance}", connectionId);

            _logger.LogDebug("Connection {ConnectionId} added to group {GroupKey}", connectionId, context.GroupKey);
        }

        public async Task RemoveConnectionAsync(string connectionId)
        {
            var connectionData = await _database.StringGetAsync($"{CONNECTION_KEY_PREFIX}{connectionId}");

            if (connectionData.HasValue)
            {
                var connectionInfo = JsonSerializer.Deserialize<ConnectionInfo>(connectionData);
                if (connectionInfo != null)
                {
                    // Remove from group
                    await _database.SetRemoveAsync($"{GROUP_KEY_PREFIX}{connectionInfo.Context.GroupKey}", connectionId);

                    // Remove from server tracking
                    await _database.SetRemoveAsync($"{SERVER_CONNECTIONS_KEY}{_serverInstance}", connectionId);
                }
            }

            // Remove connection info
            await _database.KeyDeleteAsync($"{CONNECTION_KEY_PREFIX}{connectionId}");

            _logger.LogDebug("Connection {ConnectionId} removed", connectionId);
        }

        public async Task<Models.ConnectionInfo?> GetConnectionAsync(string connectionId)
        {
            var connectionData = await _database.StringGetAsync($"{CONNECTION_KEY_PREFIX}{connectionId}");

            if (connectionData.HasValue)
            {
                return JsonSerializer.Deserialize<Models.ConnectionInfo>(connectionData);
            }

            return null;
        }

        public async Task<List<string>> GetConnectionsInGroupAsync(string groupKey)
        {
            var connections = await _database.SetMembersAsync($"{GROUP_KEY_PREFIX}{groupKey}");
            return connections.Select(c => c.ToString()).ToList();
        }

        public async Task<List<string>> GetConnectionsInGroupByRoleAsync(string groupKey, string role)
        {
            var allConnections = await GetConnectionsInGroupAsync(groupKey);
            var filteredConnections = new List<string>();

            // Use pipeline for better performance
            var batch = _database.CreateBatch();
            var tasks = allConnections.Select(connectionId =>
                batch.StringGetAsync($"{CONNECTION_KEY_PREFIX}{connectionId}")).ToArray();

            batch.Execute();

            foreach (var task in tasks)
            {
                var connectionData = await task;
                if (connectionData.HasValue)
                {
                    var connectionInfo = JsonSerializer.Deserialize<ConnectionInfo>(connectionData);
                    if (connectionInfo?.UserRole.Equals(role, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        filteredConnections.Add(connectionInfo.ConnectionId);
                    }
                }
            }

            return filteredConnections;
        }

        public async Task UpdateLastActivityAsync(string connectionId)
        {
            var connectionData = await _database.StringGetAsync($"{CONNECTION_KEY_PREFIX}{connectionId}");
            if (connectionData.HasValue)
            {
                var connectionInfo = JsonSerializer.Deserialize<ConnectionInfo>(connectionData);
                if (connectionInfo != null)
                {
                    connectionInfo.LastActivity = DateTime.UtcNow;
                    var json = JsonSerializer.Serialize(connectionInfo);
                    await _database.StringSetAsync($"{CONNECTION_KEY_PREFIX}{connectionId}", json, TimeSpan.FromHours(24));
                }
            }
        }

        public async Task<int> GetGroupConnectionCountAsync(string groupKey)
        {
            return (int)await _database.SetLengthAsync($"{GROUP_KEY_PREFIX}{groupKey}");
        }

        public async Task<int> GetTotalConnectionCountAsync()
        {
            return (int)await _database.SetLengthAsync($"{SERVER_CONNECTIONS_KEY}{_serverInstance}");
        }

        //Task<ConnectionInfo?> IConnectionManager.GetConnectionAsync(string connectionId)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
