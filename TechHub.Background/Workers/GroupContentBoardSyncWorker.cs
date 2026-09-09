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

/// <summary>
/// Consumes GroupContentBatchQueue — a dedicated connection/queue/collection kept
/// fully separate from BoardSyncWorker (teacher lesson batches) so this worker can
/// never back up, slow down, or otherwise affect the teacher's live board pipeline.
/// </summary>
public class GroupContentBoardSyncWorker : BackgroundService
{
    private readonly RabbitMQSettings _settings;
    private readonly IGroupContentBoardRepository _repository;
    private readonly ILogger _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public GroupContentBoardSyncWorker(IOptions<RabbitMQSettings> settings, IGroupContentBoardRepository repository, ILogger logger)
    {
        _settings = settings.Value;
        _repository = repository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
		while (!stoppingToken.IsCancellationRequested)
        {
			_logger.Information("GroupContentBoardSyncWorker starting...");

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
				_logger.Error(ex, "GroupContentBoardSyncWorker encountered an error, reconnecting in 10s");
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
                queue: _settings.GroupContentBatchQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
        }

        _logger.Information(
            "GroupContentBoardSyncWorker connected to RabbitMQ, Queue: {Queue}",
            _settings.GroupContentBatchQueue);
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
                var message = JsonSerializer.Deserialize<GroupContentBatchMessage>(messageJson);

                if (message == null)
                {
                    _logger.Warning("Received null group-content message, acknowledging and skipping");
                    ch.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                _logger.Debug(
                    "Processing group-content batch {BatchIndex} for Group {GroupId}, Student {StudentId}",
                    message.BatchIndex,
                    message.GroupId,
                    message.StudentId);

                // SaveBatchAsync upserts by {groupId}_{studentId}_{batchIndex}, so this is
                // safe to call unconditionally: a genuine RabbitMQ redelivery just rewrites
                // the same content (harmless), and a re-recording's batch correctly replaces
                // the previous recording's stale data instead of being dropped as a
                // "duplicate" of it — which is what a pre-check here used to do.
                await _repository.SaveBatchAsync(message);

                ch.BasicAck(ea.DeliveryTag, multiple: false);

                _logger.Information(
                    "Successfully processed group-content batch {BatchIndex} for Group {GroupId}, Student {StudentId}",
                    message.BatchIndex,
                    message.GroupId,
                    message.StudentId);
            }
            catch (JsonException jsonEx)
            {
                _logger.Error(jsonEx, "Failed to deserialize group-content message, acknowledging to prevent requeue loop");
                try { ch.BasicAck(ea.DeliveryTag, multiple: false); } catch { }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing group-content message, nacking with requeue");
                try { ch.BasicNack(ea.DeliveryTag, multiple: false, requeue: true); } catch { }
            }
        };

        channel.BasicConsume(
            queue: _settings.GroupContentBatchQueue,
            autoAck: false,
            consumer: consumer);

        _logger.Information("GroupContentBoardSyncWorker is now consuming messages");

        await Task.Delay(Timeout.Infinite, stoppingToken);
	}

	public override void Dispose()
    {
        Cleanup();
        base.Dispose();
    }
}
