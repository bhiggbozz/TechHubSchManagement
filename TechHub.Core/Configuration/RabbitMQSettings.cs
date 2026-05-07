namespace TechHub.Core.Configuration;

public class RabbitMQSettings
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string BoardBatchQueue { get; set; } = "board.batch.queue";
}
