using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

/// <summary>
/// Mongo document finalizing a student's group-content board recording — own
/// "group_content_manifests" collection, separate from the teacher's
/// "board_manifests". Id is {groupId}_{studentId}_{contentId} — scoped to the
/// specific GroupLessonContent submission the recording belongs to, so a
/// student recording again for a different submission in the same group can
/// never overwrite or leak into an unrelated one.
/// </summary>
[BsonIgnoreExtraElements]
public class GroupContentManifestDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.String)]
	public string Id { get; set; } = string.Empty;  // {groupId}_{studentId}_{contentId}

	[BsonElement("groupId")]
	public string GroupId { get; set; } = string.Empty;

	[BsonElement("studentId")]
	public string StudentId { get; set; } = string.Empty;

	[BsonElement("contentId")]
	public string ContentId { get; set; } = string.Empty;

	[BsonElement("schoolId")]
	public string SchoolId { get; set; } = string.Empty;

	[BsonElement("status")]
	[BsonRepresentation(BsonType.String)]
	public SessionStatus Status { get; set; } = SessionStatus.Completed;

	[BsonElement("stats")]
	public SessionStats? Stats { get; set; }

	[BsonElement("strokeBatches")]
	public List<BatchRef> StrokeBatches { get; set; } = new();

	[BsonElement("boards")]
	public List<BoardInfo> Boards { get; set; } = new();

	[BsonElement("boardSwitches")]
	public List<BoardSwitchEvent> BoardSwitches { get; set; } = new();

	// Legacy single-file slot — see GroupContentManifestViewModel.AudioFinalUrl.
	[BsonElement("audioFinalUrl")]
	public string? AudioFinalUrl { get; set; }

	// One entry per 60s upload batch — the real audio data (AudioFinalUrl above
	// is never actually populated by the upload pipeline).
	[BsonElement("audioChunks")]
	public List<AudioChunkRef> AudioChunks { get; set; } = new();

	[BsonElement("createdAt")]
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	[BsonElement("updatedAt")]
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
