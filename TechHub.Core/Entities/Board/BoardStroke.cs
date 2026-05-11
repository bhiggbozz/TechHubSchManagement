using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

//public class BoardStroke
//{
//    [BsonElement("id")]
//    public string Id { get; set; } = string.Empty;

//    [BsonElement("sessionId")]
//    public string SessionId { get; set; } = string.Empty;

//    [BsonElement("type")]
//    public string Type { get; set; } = "stroke";

//    [BsonElement("data")]
//    public string Data { get; set; } = string.Empty;

//    [BsonElement("color")]
//    public string Color { get; set; } = "#000000";

//    [BsonElement("width")]
//    public int Width { get; set; }

//    [BsonElement("currentBoard")]
//    public int CurrentBoard { get; set; }

//    [BsonElement("timestamp")]
//    public long Timestamp { get; set; }

//    [BsonElement("duration")]
//    public long Duration { get; set; }

//    [BsonElement("startTime")]
//    public string StartTime { get; set; } = string.Empty;

//    [BsonElement("endTime")]
//    public string EndTime { get; set; } = string.Empty;
//}

// Stroke inside a batch — compact field names matching frontend
public class BoardStroke
{
	[BsonElement("id")]
	public string Id { get; set; }

	[BsonElement("pts")]
	public List<List<double>> Pts { get; set; }  // [[x,y], [x,y], ...]

	[BsonElement("c")]
	public string Color { get; set; }     // hex color

	[BsonElement("w")]
	public double Width { get; set; }     // stroke width

	[BsonElement("ts")]
	public long Timestamp { get; set; }     // ms from session start

	[BsonElement("currentBoard")]
	public int CurrentBoard { get; set; }

	[BsonElement("sessionId")]
	public string SessionId { get; set; }
}

// One batch — 1 minute of board activity
public class BoardSessionBatch
{
	[BsonElement("batchIndex")]
	public int BatchIndex { get; set; }

	[BsonElement("startMs")]
	public long StartMs { get; set; }

	[BsonElement("endMs")]
	public long EndMs { get; set; }

	[BsonElement("strokeCount")]
	public int StrokeCount { get; set; }

	[BsonElement("boardIndex")]
	public int BoardIndex { get; set; }

	[BsonElement("strokes")]
	public List<BoardStroke> Strokes { get; set; } = new();

	[BsonElement("receivedAt")]
	public DateTime ReceivedAt { get; set; }
}