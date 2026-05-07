using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

public class BoardDimensionsViewModel
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
