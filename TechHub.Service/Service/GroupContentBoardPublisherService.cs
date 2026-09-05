using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Serilog;
using TechHub.Core.Configuration;
using TechHub.Core.Messages;
using TechHub.Service.Interface;

namespace TechHub.Service.Service;

/// <summary>
/// Publishes to GroupContentBatchQueue — a distinct queue (and connection) from
/// BoardPublisherService's BoardBatchQueue, so a burst of student submissions can
/// never delay or starve the teacher's live board queue on the same broker.
/// </summary>
public class GroupContentBoardPublisherService : IGroupContentBoardPublisherService, IDisposable
{
	private readonly RabbitMQSettings _settings;
	private readonly ILogger _logger;
	private IConnection? _connection;
	private IModel? _channel;
	private bool _disposed;
	private readonly object _lock = new();

	public GroupContentBoardPublisherService(
		IOptions<RabbitMQSettings> settings,
		ILogger logger)
	{
		_settings = settings.Value;
		_logger = logger;
	}

	private void EnsureConnected()
	{
		if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
			return;

		lock (_lock)
		{
			if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
				return;

			_logger.Information("Connecting to RabbitMQ (group content) - {AmqpUrl}", _settings.AmqpUrl);

			var factory = new ConnectionFactory
			{
				Uri = new Uri(_settings.AmqpUrl),
				AutomaticRecoveryEnabled = true,
				NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
			};

			_connection = factory.CreateConnection();
			_channel = _connection.CreateModel();

			_channel.QueueDeclare(
				queue: _settings.GroupContentBatchQueue,
				durable: true,
				exclusive: false,
				autoDelete: false,
				arguments: null);

			_logger.Information(
				"RabbitMQ connected - Queue: {Queue}", _settings.GroupContentBatchQueue);
		}
	}

	public Task PublishBatchAsync(GroupContentBatchMessage message)
	{
		EnsureConnected();

		var json = JsonSerializer.Serialize(message);
		var body = Encoding.UTF8.GetBytes(json);
		var properties = _channel!.CreateBasicProperties();

		properties.Persistent = true;
		properties.DeliveryMode = 2;
		properties.ContentType = "application/json";
		properties.MessageId = $"{message.GroupId}_{message.StudentId}_{message.BatchIndex}";

		_channel.BasicPublish(
			exchange: string.Empty,
			routingKey: _settings.GroupContentBatchQueue,
			basicProperties: properties,
			body: body);

		_logger.Information(
			"Published group-content batch {BatchIndex} for Group {GroupId}, Student {StudentId} to queue {Queue}",
			message.BatchIndex, message.GroupId, message.StudentId, _settings.GroupContentBatchQueue);

		return Task.CompletedTask;
	}

	public void Dispose()
	{
		if (_disposed) return;
		try { _channel?.Close(); _channel?.Dispose(); } catch { }
		try { _connection?.Close(); _connection?.Dispose(); } catch { }
		_disposed = true;
	}
}
