using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

public class LessonInfoViewModel
{
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("subTopic")]
    public string SubTopic { get; set; } = string.Empty;

    [JsonPropertyName("aim")]
    public string Aim { get; set; } = string.Empty;

    [JsonPropertyName("subject")]
    public SubjectInfoViewModel Subject { get; set; } = new();

    [JsonPropertyName("classroom")]
    public ClassroomInfoViewModel Classroom { get; set; } = new();
}
