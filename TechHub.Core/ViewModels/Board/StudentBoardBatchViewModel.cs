using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board;

public class StudentBoardBatchViewModel
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("boardIndex")]
    public int BoardIndex { get; set; }

    [JsonPropertyName("strokes")]
    public List<StrokeViewModel> Strokes { get; set; } = new();
}
