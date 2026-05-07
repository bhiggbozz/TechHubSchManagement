using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board;

public class BoardBatchViewModel
{
    [Required]
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("lessonId")]
    public string LessonId { get; set; } = string.Empty;

    [Required]
    [JsonPropertyName("batchIndex")]
    public int BatchIndex { get; set; }

    [Required]
    [JsonPropertyName("startMs")]
    public long StartMs { get; set; }

    [Required]
    [JsonPropertyName("endMs")]
    public long EndMs { get; set; }

    [JsonPropertyName("strokeCount")]
    public int StrokeCount { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("strokes")]
    public List<StrokeViewModel> Strokes { get; set; } = new();
}
