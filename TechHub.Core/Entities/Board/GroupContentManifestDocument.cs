using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

/// <summary>
/// Mongo document finalizing a student's group-content board recording — own
/// "group_content_manifests" collection, separate from the teacher's
/// "board_manifests". Id is {groupId}_{studentId}, same key convention as
/// GroupContentBatchDocument — no invented sessionId.
/// </summary>
[BsonIgnoreExtraElements]
public class GroupContentManifestDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.String)]
	public string Id { get; set; } = string.Empty;  // {groupId}_{studentId}

	[BsonElement("groupId")]
	public string GroupId { get; set; } = string.Empty;

	[BsonElement("studentId")]
	public string StudentId { get; set; } = string.Empty;

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

	[BsonElement("audioFinalUrl")]
	public string? AudioFinalUrl { get; set; }

	[BsonElement("createdAt")]
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	[BsonElement("updatedAt")]
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
