using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Serilog;
using TechHub.Core.Configuration;
using TechHub.Core.Messages;
using TechHub.Service.Interface;

namespace TechHub.Service.Service;

public class BoardPublisherService : IBoardPublisherService, IDisposable
{
    private readonly RabbitMQSettings _settings;
    private readonly ILogger _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private bool _disposed;

    public BoardPublisherService(
        IOptions<RabbitMQSettings> settings,
        ILogger logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = _settings.Host,
            Port = _settings.Port,
            UserName = _settings.Username,
            Password = _settings.Password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: _settings.BoardBatchQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _logger.Information(
            "RabbitMQ BoardPublisherService initialized, Queue: {Queue}",
            _settings.BoardBatchQueue);
    }

    public Task PublishBatchAsync(BoardBatchMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.DeliveryMode = 2;
        properties.ContentType = "application/json";
        properties.MessageId = $"{message.SessionId}_{message.BatchIndex}";

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _settings.BoardBatchQueue,
            basicProperties: properties,
            body: body);

        _logger.Debug(
            "Published batch {BatchIndex} for session {SessionId} to queue {Queue}",
            message.BatchIndex,
            message.SessionId,
            _settings.BoardBatchQueue);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();

        _disposed = true;
    }
}
