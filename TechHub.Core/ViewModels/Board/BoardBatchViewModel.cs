using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board;

public class BoardBatchViewModel
{
	[JsonPropertyName("sessionId")]
	public string SessionId { get; set; } = string.Empty;

	[JsonPropertyName("lessonId")]
	public string LessonId { get; set; } = string.Empty;

	[JsonPropertyName("batchIndex")]
	public int BatchIndex { get; set; }

	[JsonPropertyName("startMs")]
	public long StartMs { get; set; }

	[JsonPropertyName("endMs")]
	public long EndMs { get; set; }

	[JsonPropertyName("strokeCount")]
	public int StrokeCount { get; set; }

	[JsonPropertyName("sizeBytes")]
	public long SizeBytes { get; set; }

	[JsonPropertyName("boardIndex")]
	public int BoardIndex { get; set; }  // ← add

	[JsonPropertyName("strokes")]
	public List<StrokeViewModel> Strokes { get; set; } = new();
	public List<BoardSwitchViewModel> BoardSwitches { get; set; } = new();
	[JsonPropertyName("audioUrl")]
	public string? AudioUrl { get; set; }

}
public class BoardSwitchViewModel
{
	[JsonPropertyName("fromBoard")]
	public int FromBoard { get; set; }

	[JsonPropertyName("toBoard")]
	public int ToBoard { get; set; }

	[JsonPropertyName("timestampMs")]
	public long TimestampMs { get; set; }
}