using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class StrokeSummary
{
    [BsonElement("count")]
    public int Count { get; set; }

    [BsonElement("sizeBytes")]
    public long SizeBytes { get; set; }
}
