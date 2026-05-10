using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

//public class SessionManifestViewModel
//{
//    [Required]
//    [JsonPropertyName("version")]
//    public string Version { get; set; } = "2.0";

//    [Required]
//    [JsonPropertyName("session")]
//    public SessionInfoViewModel Session { get; set; } = new();

//    [Required]
//    [JsonPropertyName("lesson")]
//    public LessonInfoViewModel Lesson { get; set; } = new();

//    [Required]
//    [JsonPropertyName("stats")]
//    public SessionStatsViewModel Stats { get; set; } = new();

//    [JsonPropertyName("chunks")]
//    public List<ChunkViewModel> Chunks { get; set; } = new();

//    [JsonPropertyName("mediaAssets")]
//    public List<MediaAssetViewModel> MediaAssets { get; set; } = new();

//    [JsonPropertyName("boards")]
//    public List<BoardInfoViewModel> Boards { get; set; } = new();

//    [JsonPropertyName("chapters")]
//    public List<ChapterViewModel> Chapters { get; set; } = new();
//}

public class SessionManifestViewModel
{
	public string Version { get; set; }
	public SessionInfoViewModel Session { get; set; }
	public LessonInfoViewModel Lesson { get; set; }
	public ManifestStatsViewModel Stats { get; set; }
	public List<ChunkViewModel> Chunks { get; set; } = new();
	public List<StrokeBatchRefViewModel> StrokeBatches { get; set; } = new();
}

public class ManifestStatsViewModel
{
	public long TotalDurationMs { get; set; }
	public int ChunkCount { get; set; }
	public int StrokeBatchCount { get; set; }
}

public class ChunkViewModel
{
	public int Index { get; set; }
	public long StartMs { get; set; }
	public long EndMs { get; set; }
	public ChunkAudioViewModel Audio { get; set; }
}

public class ChunkAudioViewModel
{
	public string Url { get; set; }
	public string MediaId { get; set; }
}

// Lightweight batch reference — no strokes
public class StrokeBatchRefViewModel
{
	public int BatchIndex { get; set; }
	public string IndexKey { get; set; }  // e.g. "lesson-uuid_0"
	public long StartMs { get; set; }
	public long EndMs { get; set; }
	public int StrokeCount { get; set; }
}