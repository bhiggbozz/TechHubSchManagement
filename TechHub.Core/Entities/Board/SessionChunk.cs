using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class SessionChunk
{
    [BsonElement("index")]
    public int Index { get; set; }

    [BsonElement("startMs")]
    public long StartMs { get; set; }

    [BsonElement("endMs")]
    public long EndMs { get; set; }

    [BsonElement("audio")]
    public AudioChunk Audio { get; set; } = new();

    [BsonElement("strokes")]
    public StrokeSummary Strokes { get; set; } = new();

    [BsonElement("events")]
    public List<object> Events { get; set; } = new();
}
