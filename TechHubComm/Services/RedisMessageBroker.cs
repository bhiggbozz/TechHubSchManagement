using static TechHubComm.Services.Interfaces;
using StackExchange.Redis;
using System.Text.Json;
using System.Collections.Concurrent;

namespace TechHubComm.Services
{
        public class RedisMessageBroker : IMessageBroker
        {
            private readonly IConnectionMultiplexer _redis;
            private readonly ISubscriber _subscriber;
            private readonly ILogger<RedisMessageBroker> _logger;
            private readonly ConcurrentDictionary<string, Func<string, Task>> _handlers;

            public RedisMessageBroker(IConnectionMultiplexer redis, ILogger<RedisMessageBroker> logger)
            {
                _redis = redis;
                _subscriber = redis.GetSubscriber();
                _logger = logger;
                _handlers = new ConcurrentDictionary<string, Func<string, Task>>();
            }

            public async Task PublishAsync(string channel, object message)
            {
                var json = JsonSerializer.Serialize(message);
                await _subscriber.PublishAsync(channel, json);
                _logger.LogDebug("Message published to channel {Channel}", channel);
            }

            public async Task SubscribeAsync(string channel, Func<string, Task> handler)
            {
                _handlers.TryAdd(channel, handler);
                await _subscriber.SubscribeAsync(channel, async (ch, message) =>
                {
                    try
                    {
                        await handler(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling message on channel {Channel}", channel);
                    }
                });

                _logger.LogDebug("Subscribed to channel {Channel}", channel);
            }

            public async Task UnsubscribeAsync(string channel)
            {
                _handlers.TryRemove(channel, out _);
                await _subscriber.UnsubscribeAsync(channel);
                _logger.LogDebug("Unsubscribed from channel {Channel}", channel);
            }
        }
    }

