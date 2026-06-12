using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TechHub.Core.ViewModel.Board;

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
	public SessionInfoViewModel2 Session { get; set; }
	public LessonInfoViewModel Lesson { get; set; }
	public ManifestStatsViewModel Stats { get; set; }
	public List<ChunkViewModel> Chunks { get; set; } = new();
	public List<StrokeBatchRefViewModel> StrokeBatches { get; set; } = new();
	public List<MediaAssetViewModel> MediaAssets { get; set; } = new();
	public List<BoardInfoViewModel> Boards { get; set; } = new();
	public List<ChapterViewModel2> Chapters { get; set; } = new();
	public List<BoardSwitchViewModel> BoardSwitches { get; set; } = new();  

}

public class SessionInfoViewModel2
{
	public string Id { get; set; }
	public string LessonId { get; set; }
	public string SchoolId { get; set; }
	public string RecordedAt { get; set; }
	public string PublishedAt { get; set; }
	public TeacherInfoViewModel Teacher { get; set; }
}

//public class TeacherInfoViewModel
//{
//	public string Id { get; set; }
//	public string Name { get; set; }
//}

//public class LessonInfoViewModel
//{
//	public string Topic { get; set; }
//	public string SubTopic { get; set; }
//	public string Aim { get; set; }
//	public SubjectInfoViewModel2 Subject { get; set; }
//	public ClassroomInfoViewModel Classroom { get; set; }
//}

public class SubjectInfoViewModel2
{
	public string Id { get; set; }
	public string Name { get; set; }
}

//public class ClassroomInfoViewModel
//{
//	public string Id { get; set; }
//	public string Name { get; set; }
//}

public class ManifestStatsViewModel
{
	public long TotalDurationMs { get; set; }
	public string TotalDurationFormatted { get; set; }
	public int ChunkCount { get; set; }
	public long ChunkDurationMs { get; set; }
	public long SeekGranularityMs { get; set; }
	public long TotalAudioSizeBytes { get; set; }
	public int TotalStrokeCount { get; set; }
	public int BoardCount { get; set; }
	public int StrokeBatchCount { get; set; }
}

public class ChunkViewModel
{
	public int Index { get; set; }
	public long StartMs { get; set; }
	public long EndMs { get; set; }
	public ChunkAudioViewModel Audio { get; set; }
	public List<ChunkEventViewModel> Events { get; set; } = new();  
}

public class ChunkAudioViewModel
{
	public string Url { get; set; }
	public string MediaId { get; set; }
	public long SizeBytes { get; set; }
	public long DurationMs { get; set; }
}

public class StrokeBatchRefViewModel
{
	public int BatchIndex { get; set; }
	public string IndexKey { get; set; }
	public long StartMs { get; set; }
	public long EndMs { get; set; }
	public int StrokeCount { get; set; }
	public long SizeBytes { get; set; }
}

//public class MediaAssetViewModel
//{
//	public string Id { get; set; }
//	public string Name { get; set; }
//	public string Type { get; set; }
//	public string Url { get; set; }
//}

public class BoardInfoViewModel2
{
	public int Index { get; set; }
	public BoardDimensionsViewModel Dimensions { get; set; }
	public int StrokeCount { get; set; }
}

public class BoardDimensionsViewModel2
{
	public int Width { get; set; }
	public int Height { get; set; }
}

public class ChapterViewModel2
{
	public string Title { get; set; }
	public long StartMs { get; set; }
	public long EndMs { get; set; }
}