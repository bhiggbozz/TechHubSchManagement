using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class SessionStats
{
	[BsonElement("totalDurationMs")]
	public long TotalDurationMs { get; set; }

	[BsonElement("totalDurationFormatted")]
	public string TotalDurationFormatted { get; set; } = string.Empty;

	[BsonElement("chunkCount")]
	public int ChunkCount { get; set; }

	[BsonElement("chunkDurationMs")]
	public long ChunkDurationMs { get; set; }

	[BsonElement("seekGranularityMs")]
	public long SeekGranularityMs { get; set; }  // ← add

	[BsonElement("totalAudioSizeBytes")]
	public long TotalAudioSizeBytes { get; set; }

	[BsonElement("totalStrokeCount")]
	public int TotalStrokeCount { get; set; }

	[BsonElement("boardCount")]
	public int BoardCount { get; set; }

	[BsonElement("strokeBatchCount")]
	public int StrokeBatchCount { get; set; }  // ← add
}
