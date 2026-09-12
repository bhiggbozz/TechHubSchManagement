using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

/// <summary>
/// Mongo document for student group-content board batches — stored in its own
/// "group_content_batches" collection, separate from the teacher's "board_batches".
///
/// Id is {groupId}_{studentId}_{contentId}_{batchIndex} — scoped to the specific
/// GroupLessonContent submission, so a second recording for a different
/// submission in the same group can never overwrite this one. Still a plain
/// _id point lookup (Mongo indexes _id automatically), so retrieval stays
/// index-based, not a scan.
/// </summary>
[BsonIgnoreExtraElements]
public class GroupContentBatchDocument
{
	[BsonId]
	[BsonRepresentation(BsonType.String)]
	public string Id { get; set; } = string.Empty;  // {groupId}_{studentId}_{contentId}_{batchIndex}

	[BsonElement("groupId")]
	public string GroupId { get; set; } = string.Empty;

	[BsonElement("schoolId")]
	public string SchoolId { get; set; } = string.Empty;

	[BsonElement("studentId")]
	public string StudentId { get; set; } = string.Empty;

	[BsonElement("contentId")]
	public string ContentId { get; set; } = string.Empty;

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

	[BsonElement("audioUrl")]
	public string? AudioUrl { get; set; }

	[BsonElement("boardSwitches")]
	public List<BoardSwitchEvent> BoardSwitches { get; set; } = new();
}
