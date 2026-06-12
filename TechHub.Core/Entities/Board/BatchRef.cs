using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities.Board;

public class BatchRef
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

	[BsonElement("boardIndex")]
	public int BoardIndex { get; set; }
	public string? AudioUrl { get; set; }                         
	public List<BoardSwitchEvent> BoardSwitches { get; set; } = new();
}

