using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class BoardBatch
{
    [BsonElement("batchIndex")]
    public int BatchIndex { get; set; }

	[BsonElement("indexKey")]
	public string IndexKey { get; set; } = string.Empty; 

	[BsonElement("startMs")]
    public long StartMs { get; set; }

    [BsonElement("endMs")]
    public long EndMs { get; set; }

    [BsonElement("strokeCount")]
    public int StrokeCount { get; set; }

    [BsonElement("sizeBytes")]
    public long SizeBytes { get; set; }

    [BsonElement("receivedAt")]
    public DateTime ReceivedAt { get; set; }

    [BsonElement("strokes")]
    public List<BoardStroke> Strokes { get; set; } = new();
}
