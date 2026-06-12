using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;


[BsonIgnoreExtraElements]
public class BoardSession
{
	[BsonId]
	[BsonRepresentation(BsonType.String)]
	public string Id { get; set; } = string.Empty;

	[BsonElement("lessonId")]
	public string LessonId { get; set; } = string.Empty;

	[BsonElement("schoolId")]
	public string SchoolId { get; set; } = string.Empty;

	[BsonElement("teacherId")]
	public string TeacherId { get; set; } = string.Empty;

	[BsonElement("status")]
	[BsonRepresentation(BsonType.String)]
	public SessionStatus Status { get; set; } = SessionStatus.InProgress;

	[BsonElement("audioFinalUrl")]
	public string? AudioFinalUrl { get; set; }

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

	[BsonElement("mediaAssets")]
	public List<MediaAsset> MediaAssets { get; set; } = new();

	[BsonElement("boards")]
	public List<BoardInfo> Boards { get; set; } = new();

	[BsonElement("chapters")]
	public List<Chapter> Chapters { get; set; } = new();

	[BsonElement("batches")]
	public List<BoardBatch> Batches { get; set; } = new();

	[BsonElement("recordedAt")]
	public DateTime? RecordedAt { get; set; }

	[BsonElement("publishedAt")]
	public DateTime? PublishedAt { get; set; }

	[BsonElement("createdAt")]
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	[BsonElement("updatedAt")]
	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	// Add to BoardSession
	[BsonElement("boardSwitches")]
	public List<BoardSwitchEvent> BoardSwitches { get; set; } = new();
}


//public class AudioChunk
//{
//	[BsonElement("url")]
//	public string Url { get; set; }

//	[BsonElement("mediaId")]
//	public string MediaId { get; set; }

//	[BsonElement("sizeBytes")]
//	public long SizeBytes { get; set; }

//	[BsonElement("durationMs")]
//	public long DurationMs { get; set; }
//}

//public class MediaAsset
//{
//	[BsonElement("id")]
//	public string Id { get; set; }

//	[BsonElement("name")]
//	public string Name { get; set; }

//	[BsonElement("type")]
//	public string Type { get; set; }

//	[BsonElement("url")]
//	public string Url { get; set; }
//}

//public class BoardInfo
//{
//	[BsonElement("index")]
//	public int Index { get; set; }

//	[BsonElement("dimensions")]
//	public BoardDimensions Dimensions { get; set; }

//	[BsonElement("strokeCount")]
//	public int StrokeCount { get; set; }
//}

//public class BoardDimensions
//{
//	[BsonElement("width")]
//	public int Width { get; set; }

//	[BsonElement("height")]
//	public int Height { get; set; }
//}


