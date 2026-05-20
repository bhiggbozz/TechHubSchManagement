using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.Board;

public class SessionManifestViewModel
{
	[JsonPropertyName("version")]
	public string Version { get; set; } = string.Empty;

	[JsonPropertyName("session")]
	public SessionInfoViewModel Session { get; set; } = new();

	[JsonPropertyName("lesson")]
	public LessonInfoViewModel Lesson { get; set; } = new();

	[JsonPropertyName("stats")]
	public ManifestStatsViewModel Stats { get; set; } = new();

	[JsonPropertyName("chunks")]
	public List<ChunkViewModel> Chunks { get; set; } = new();

	[JsonPropertyName("strokeBatches")]
	public List<StrokeBatchRefViewModel> StrokeBatches { get; set; } = new();

	[JsonPropertyName("mediaAssets")]
	public List<MediaAssetViewModel> MediaAssets { get; set; } = new();

	[JsonPropertyName("boards")]
	public List<BoardInfoViewModel> Boards { get; set; } = new();

	[JsonPropertyName("chapters")]
	public List<ChapterViewModel> Chapters { get; set; } = new();
}

public class SessionInfoViewModel
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("lessonId")]
	public string LessonId { get; set; } = string.Empty;

	[JsonPropertyName("schoolId")]
	public string SchoolId { get; set; } = string.Empty;

	[JsonPropertyName("recordedAt")]
	public string RecordedAt { get; set; } = string.Empty;

	[JsonPropertyName("publishedAt")]
	public string PublishedAt { get; set; } = string.Empty;

	[JsonPropertyName("teacher")]
	public TeacherInfoViewModel Teacher { get; set; } = new();
}

public class TeacherInfoViewModel
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;
}

public class LessonInfoViewModel
{
	[JsonPropertyName("topic")]
	public string Topic { get; set; } = string.Empty;

	[JsonPropertyName("subTopic")]
	public string SubTopic { get; set; } = string.Empty;

	[JsonPropertyName("aim")]
	public string Aim { get; set; } = string.Empty;

	[JsonPropertyName("subject")]
	public SubjectInfoViewModel Subject { get; set; } = new();

	[JsonPropertyName("classroom")]
	public ClassroomInfoViewModel Classroom { get; set; } = new();
}

public class SubjectInfoViewModel
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;
}

public class ClassroomInfoViewModel
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;
}

public class ManifestStatsViewModel
{
	[JsonPropertyName("totalDurationMs")]
	public long TotalDurationMs { get; set; }

	[JsonPropertyName("totalDurationFormatted")]
	public string TotalDurationFormatted { get; set; } = string.Empty;

	[JsonPropertyName("chunkCount")]
	public int ChunkCount { get; set; }

	[JsonPropertyName("chunkDurationMs")]
	public long ChunkDurationMs { get; set; }

	[JsonPropertyName("seekGranularityMs")]
	public long SeekGranularityMs { get; set; }

	[JsonPropertyName("totalAudioSizeBytes")]
	public long TotalAudioSizeBytes { get; set; }

	[JsonPropertyName("totalStrokeCount")]
	public int TotalStrokeCount { get; set; }

	[JsonPropertyName("boardCount")]
	public int BoardCount { get; set; }

	[JsonPropertyName("strokeBatchCount")]
	public int StrokeBatchCount { get; set; }
}

public class MediaAssetViewModel
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	[JsonPropertyName("type")]
	public string Type { get; set; } = string.Empty;

	[JsonPropertyName("url")]
	public string Url { get; set; } = string.Empty;
}

public class BoardInfoViewModel
{
	[JsonPropertyName("index")]
	public int Index { get; set; }

	[JsonPropertyName("dimensions")]
	public BoardDimensionsViewModel Dimensions { get; set; } = new();

	[JsonPropertyName("strokeCount")]
	public int StrokeCount { get; set; }
}

public class BoardDimensionsViewModel
{
	[JsonPropertyName("width")]
	public int Width { get; set; }

	[JsonPropertyName("height")]
	public int Height { get; set; }
}

public class ChapterViewModel
{
	[JsonPropertyName("title")]
	public string Title { get; set; } = string.Empty;

	[JsonPropertyName("startMs")]
	public long StartMs { get; set; }

	[JsonPropertyName("endMs")]
	public long EndMs { get; set; }
}
