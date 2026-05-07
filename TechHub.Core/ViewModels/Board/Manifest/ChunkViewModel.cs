using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

public class ChunkViewModel
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("startMs")]
    public long StartMs { get; set; }

    [JsonPropertyName("endMs")]
    public long EndMs { get; set; }

    [JsonPropertyName("audio")]
    public AudioChunkViewModel Audio { get; set; } = new();

    [JsonPropertyName("strokes")]
    public StrokeSummaryViewModel Strokes { get; set; } = new();

    [JsonPropertyName("events")]
    public List<object> Events { get; set; } = new();
}
