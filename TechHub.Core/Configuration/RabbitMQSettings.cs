namespace TechHub.Core.Configuration;

public class RabbitMQSettings
{
	public const string SectionName = "RabbitMQ";
	public string AmqpUrl        { get; set; }  // ← add
    public string Host           { get; set; }
    public int    Port           { get; set; }
    public string Username       { get; set; }
    public string Password       { get; set; }
    public string VirtualHost    { get; set; }
    public string BoardBatchQueue { get; set; }
}
