using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using TechHub.Core.Configuration;
using TechHub.Core.Messages;
using TechHub.Service.Interface;

namespace TechHub.Background.Workers;

public class BoardSyncWorker : BackgroundService
{
    private readonly RabbitMQSettings _settings;
    private readonly IBoardSessionRepository _repository;
    private readonly ILogger _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public BoardSyncWorker(IOptions<RabbitMQSettings> settings, IBoardSessionRepository repository, ILogger logger)
    {
        _settings = settings.Value;
        _repository = repository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
		while (!stoppingToken.IsCancellationRequested)
        {
			_logger.Information("BoardSyncWorker starting...");

			try
			{
				InitializeRabbitMQ();
				await ConsumeMessages(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "BoardSyncWorker encountered an error, reconnecting in 10s");
				Cleanup();
				await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
			}
		}
    }

    private void Cleanup()
    {
        lock (_lock)
        {
            try { _channel?.Close(); } catch { }
            try { _channel?.Dispose(); } catch { }
            try { _connection?.Close(); } catch { }
            try { _connection?.Dispose(); } catch { }
            _channel = null;
            _connection = null;
        }
    }

    private void InitializeRabbitMQ()
    {
        Cleanup();

		var factory = new ConnectionFactory
		{
			Uri = new Uri(_settings.AmqpUrl),
			AutomaticRecoveryEnabled = true,
			DispatchConsumersAsync = true
		};

        lock (_lock)
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(
                queue: _settings.BoardBatchQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
        }

        _logger.Information(
            "BoardSyncWorker connected to RabbitMQ, Queue: {Queue}",
            _settings.BoardBatchQueue);
    }

    private async Task ConsumeMessages(CancellationToken stoppingToken)
    {
        IModel? channel;
        lock (_lock) { channel = _channel; }

        if (channel is null)
            throw new InvalidOperationException("Channel not initialized");

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.Received += async (model, ea) =>
        {
            IModel? ch;
            lock (_lock) { ch = _channel; }
            if (ch is null) return;

            var body = ea.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);

            try
            {
                var message = JsonSerializer.Deserialize<BoardBatchMessage>(messageJson);

                if (message == null)
                {
                    _logger.Warning("Received null message, acknowledging and skipping");
                    ch.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                _logger.Debug(
                    "Processing batch {BatchIndex} for session {SessionId}",
                    message.BatchIndex,
                    message.SessionId);

                var exists = await _repository.BatchExistsAsync(message.SessionId, message.BatchIndex);

                if (exists)
                {
                    _logger.Warning(
                        "Duplicate batch detected - SessionId: {SessionId}, BatchIndex: {BatchIndex}. Acknowledging without save.",
                        message.SessionId,
                        message.BatchIndex);

                    ch.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                await _repository.SaveBatchAsync(message);

                ch.BasicAck(ea.DeliveryTag, multiple: false);

                _logger.Information(
                    "Successfully processed batch {BatchIndex} for session {SessionId}",
                    message.BatchIndex,
                    message.SessionId);
            }
            catch (JsonException jsonEx)
            {
                _logger.Error(jsonEx, "Failed to deserialize message, acknowledging to prevent requeue loop");
                try { ch.BasicAck(ea.DeliveryTag, multiple: false); } catch { }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing message, nacking with requeue");
                try { ch.BasicNack(ea.DeliveryTag, multiple: false, requeue: true); } catch { }
            }
        };

        channel.BasicConsume(
            queue: _settings.BoardBatchQueue,
            autoAck: false,
            consumer: consumer);

        _logger.Information("BoardSyncWorker is now consuming messages");

        await Task.Delay(Timeout.Infinite, stoppingToken);
	}

	public override void Dispose()
    {
        Cleanup();
        base.Dispose();
    }
}
