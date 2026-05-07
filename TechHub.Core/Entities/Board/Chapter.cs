using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class Chapter
{
    [BsonElement("timestampMs")]
    public long TimestampMs { get; set; }

    [BsonElement("label")]
    public string Label { get; set; } = string.Empty;
}
