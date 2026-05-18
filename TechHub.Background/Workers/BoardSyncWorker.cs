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

			//await Task.Yield();

			try
			{
				InitializeRabbitMQ();
				await ConsumeMessages(stoppingToken);
               // break;
			}
			catch (Exception ex)
			{
				_logger.Fatal(ex, "BoardSyncWorker encountered a fatal error");
				await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
				//throw;
			}
		}
			
    }

    private void InitializeRabbitMQ()
    {
		//var factory = new ConnectionFactory
		//{
		//    HostName = _settings.Host,
		//    Port = _settings.Port,
		//    UserName = _settings.Username,
		//    Password = _settings.Password,
		//    AutomaticRecoveryEnabled = true,
		//    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
		//    DispatchConsumersAsync = true
		//};

		var factory = new ConnectionFactory
		{
			Uri = new Uri(_settings.AmqpUrl),
			AutomaticRecoveryEnabled = true,
			DispatchConsumersAsync = true  // ← required for AsyncEventingBasicConsumer
		};

		_connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: _settings.BoardBatchQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

        _logger.Information(
            "BoardSyncWorker connected to RabbitMQ, Queue: {Queue}",
            _settings.BoardBatchQueue);
    }

    private async Task ConsumeMessages(CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);

            try
            {
                var message = JsonSerializer.Deserialize<BoardBatchMessage>(messageJson);

                if (message == null)
                {
                    _logger.Warning("Received null message, acknowledging and skipping");
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
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

                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                await _repository.SaveBatchAsync(message);

                _channel.BasicAck(ea.DeliveryTag, multiple: false);

                _logger.Information(
                    "Successfully processed batch {BatchIndex} for session {SessionId}",
                    message.BatchIndex,
                    message.SessionId);
            }
            catch (JsonException jsonEx)
            {
                _logger.Error(jsonEx, "Failed to deserialize message, acknowledging to prevent requeue loop");
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing message, nacking with requeue");
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(
            queue: _settings.BoardBatchQueue,
            autoAck: false,
            consumer: consumer);

        _logger.Information("BoardSyncWorker is now consuming messages");

        stoppingToken.Register(() =>
        {
            _logger.Information("BoardSyncWorker stopping...");
            _channel?.Close();
            _connection?.Close();
        });

		await Task.Delay(Timeout.Infinite, stoppingToken);
	}

	public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
