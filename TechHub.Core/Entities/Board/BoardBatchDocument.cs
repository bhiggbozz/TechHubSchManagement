using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities.Board;

[BsonIgnoreExtraElements]
public class BoardBatchDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.String)]
	public string Id { get; set; } = string.Empty;  // sessionId_batchIndex

	[BsonElement("sessionId")]
	public string SessionId { get; set; } = string.Empty;

	[BsonElement("schoolId")]
	public string SchoolId { get; set; } = string.Empty;

	[BsonElement("batchIndex")]
	public int BatchIndex { get; set; }

	[BsonElement("boardIndex")]
	public int BoardIndex { get; set; }

	[BsonElement("startMs")]
	public long StartMs { get; set; }

	[BsonElement("endMs")]
	public long EndMs { get; set; }

	[BsonElement("strokeCount")]
	public int StrokeCount { get; set; }

	[BsonElement("receivedAt")]
	public DateTime ReceivedAt { get; set; }

	[BsonElement("strokes")]
	public List<BoardStroke> Strokes { get; set; } = new();
	public string? AudioUrl { get; set; }                         
	public List<BoardSwitchEvent> BoardSwitches { get; set; } = new();
}

