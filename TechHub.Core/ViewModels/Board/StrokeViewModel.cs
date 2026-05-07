using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board;

public class StrokeViewModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "stroke";

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#000000";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("currentBoard")]
    public int CurrentBoard { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("duration")]
    public long Duration { get; set; }

    [JsonPropertyName("startTime")]
    public string StartTime { get; set; } = string.Empty;

    [JsonPropertyName("endTime")]
    public string EndTime { get; set; } = string.Empty;
}
