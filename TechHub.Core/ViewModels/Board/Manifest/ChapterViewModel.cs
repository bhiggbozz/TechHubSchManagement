using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

public class ChapterViewModel
{
    [JsonPropertyName("timestampMs")]
    public long TimestampMs { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}
