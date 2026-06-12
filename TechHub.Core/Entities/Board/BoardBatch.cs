using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class BoardBatch
{
	[BsonElement("batchIndex")]
	public int BatchIndex { get; set; }

	[BsonElement("boardIndex")]
	public int BoardIndex { get; set; }  // ← add

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

	[BsonElement("boardSwitches")]
	public List<BoardSwitchEvent> BoardSwitches { get; set; } = new();
}

public class BoardSwitchEvent
{
	[BsonElement("fromBoard")]
	public int FromBoard { get; set; }

	[BsonElement("toBoard")]
	public int ToBoard { get; set; }

	[BsonElement("timestampMs")]
	public long TimestampMs { get; set; }  // global session time when switch happened
}
