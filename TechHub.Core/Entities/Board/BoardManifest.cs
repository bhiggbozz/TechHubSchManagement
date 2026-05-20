using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities.Board;

[BsonIgnoreExtraElements]
public class BoardManifest
{
	[BsonId]
	[BsonRepresentation(BsonType.String)]
	public string Id { get; set; } = string.Empty;  // sessionId

	[BsonElement("lessonId")]
	public string LessonId { get; set; } = string.Empty;

	[BsonElement("schoolId")]
	public string SchoolId { get; set; } = string.Empty;

	[BsonElement("teacherId")]
	public string TeacherId { get; set; } = string.Empty;

	[BsonElement("status")]
	[BsonRepresentation(BsonType.String)]
	public SessionStatus Status { get; set; } = SessionStatus.InProgress;

	[BsonElement("version")]
	public string Version { get; set; } = "2.0";

	[BsonElement("teacher")]
	public TeacherInfo? Teacher { get; set; }

	[BsonElement("lesson")]
	public LessonInfo? Lesson { get; set; }

	[BsonElement("stats")]
	public SessionStats? Stats { get; set; }

	[BsonElement("chunks")]
	public List<SessionChunk> Chunks { get; set; } = new();

	[BsonElement("chapters")]
	public List<Chapter> Chapters { get; set; } = new();

	[BsonElement("mediaAssets")]
	public List<MediaAsset> MediaAssets { get; set; } = new();

	[BsonElement("boards")]
	public List<BoardInfo> Boards { get; set; } = new();

	[BsonElement("batchRefs")]
	public List<BatchRef> BatchRefs { get; set; } = new();

	[BsonElement("audioFinalUrl")]
	public string? AudioFinalUrl { get; set; }

	[BsonElement("createdAt")]
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	[BsonElement("updatedAt")]
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

