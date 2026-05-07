using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class BoardStroke
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = "stroke";

    [BsonElement("data")]
    public string Data { get; set; } = string.Empty;

    [BsonElement("color")]
    public string Color { get; set; } = "#000000";

    [BsonElement("width")]
    public int Width { get; set; }

    [BsonElement("currentBoard")]
    public int CurrentBoard { get; set; }

    [BsonElement("timestamp")]
    public long Timestamp { get; set; }

    [BsonElement("duration")]
    public long Duration { get; set; }

    [BsonElement("startTime")]
    public string StartTime { get; set; } = string.Empty;

    [BsonElement("endTime")]
    public string EndTime { get; set; } = string.Empty;
}
