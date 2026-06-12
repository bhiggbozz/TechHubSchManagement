using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities.Board;

public class ChunkEvent
{
	[BsonElement("type")]
	public string Type { get; set; } = string.Empty;

	[BsonElement("timestampMs")]
	public long TimestampMs { get; set; }

	[BsonElement("mediaAssetId")]
	public string MediaAssetId { get; set; } = string.Empty;
	[BsonElement("fromBoard")]
	public int? FromBoard { get; set; }  // ← add

	[BsonElement("toBoard")]
	public int? ToBoard { get; set; }    // ← add
}

