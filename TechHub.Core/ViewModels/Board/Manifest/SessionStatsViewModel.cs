using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

public class SessionStatsViewModel
{
    [JsonPropertyName("totalDurationMs")]
    public long TotalDurationMs { get; set; }

    [JsonPropertyName("totalDurationFormatted")]
    public string TotalDurationFormatted { get; set; } = string.Empty;

    [JsonPropertyName("chunkCount")]
    public int ChunkCount { get; set; }

    [JsonPropertyName("chunkDurationMs")]
    public long ChunkDurationMs { get; set; }

    [JsonPropertyName("totalAudioSizeBytes")]
    public long TotalAudioSizeBytes { get; set; }

    [JsonPropertyName("totalStrokeCount")]
    public int TotalStrokeCount { get; set; }

    [JsonPropertyName("boardCount")]
    public int BoardCount { get; set; }
}
