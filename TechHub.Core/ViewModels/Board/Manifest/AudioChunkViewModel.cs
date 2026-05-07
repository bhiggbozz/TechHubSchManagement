using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

public class AudioChunkViewModel
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }
}
