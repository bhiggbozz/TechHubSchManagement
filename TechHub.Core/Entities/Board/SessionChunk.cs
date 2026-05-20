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

	[BsonElement("events")]
	public List<ChunkEvent> Events { get; set; } = new(); 
}

//public class AudioChunk
//{
//	[BsonElement("url")]
//	public string Url { get; set; } = string.Empty;

//	[BsonElement("mediaId")]
//	public string MediaId { get; set; } = string.Empty;

//	[BsonElement("sizeBytes")]
//	public long SizeBytes { get; set; }

//	[BsonElement("durationMs")]
//	public long DurationMs { get; set; }
//}

