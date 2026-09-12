using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

/// <summary>
/// One 60s uploaded audio segment for a group-content recording — chunkIndex is
/// the upload-batch index, same granularity/numbering as BatchRef.BatchIndex.
/// </summary>
public class AudioChunkRef
{
	[BsonElement("chunkIndex")]
	public int ChunkIndex { get; set; }

	[BsonElement("url")]
	public string Url { get; set; } = string.Empty;

	[BsonElement("mediaId")]
	public string? MediaId { get; set; }
}
