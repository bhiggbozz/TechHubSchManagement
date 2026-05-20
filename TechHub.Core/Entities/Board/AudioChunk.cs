using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class AudioChunk
{
	[BsonElement("url")]
	public string Url { get; set; } = string.Empty;

	[BsonElement("mediaId")]
	public string MediaId { get; set; } = string.Empty;

	[BsonElement("sizeBytes")]
	public long SizeBytes { get; set; }

	[BsonElement("durationMs")]
	public long DurationMs { get; set; }
}
