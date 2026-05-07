using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

public class SessionInfoViewModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("lessonId")]
    public string LessonId { get; set; } = string.Empty;

    [JsonPropertyName("schoolId")]
    public string SchoolId { get; set; } = string.Empty;

    [JsonPropertyName("recordedAt")]
    public DateTime RecordedAt { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("teacher")]
    public TeacherInfoViewModel Teacher { get; set; } = new();
}
