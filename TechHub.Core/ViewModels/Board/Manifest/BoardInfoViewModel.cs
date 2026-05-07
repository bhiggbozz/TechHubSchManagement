using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

public class BoardInfoViewModel
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("dimensions")]
    public BoardDimensionsViewModel Dimensions { get; set; } = new();

    [JsonPropertyName("strokeCount")]
    public int StrokeCount { get; set; }
}
