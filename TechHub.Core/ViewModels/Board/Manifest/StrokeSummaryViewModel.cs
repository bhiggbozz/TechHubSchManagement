using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

public class StrokeSummaryViewModel
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
}
