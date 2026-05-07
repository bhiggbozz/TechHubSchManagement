using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

public class SessionManifestViewModel
{
    [Required]
    [JsonPropertyName("version")]
    public string Version { get; set; } = "2.0";

    [Required]
    [JsonPropertyName("session")]
    public SessionInfoViewModel Session { get; set; } = new();

    [Required]
    [JsonPropertyName("lesson")]
    public LessonInfoViewModel Lesson { get; set; } = new();

    [Required]
    [JsonPropertyName("stats")]
    public SessionStatsViewModel Stats { get; set; } = new();

    [JsonPropertyName("chunks")]
    public List<ChunkViewModel> Chunks { get; set; } = new();

    [JsonPropertyName("mediaAssets")]
    public List<MediaAssetViewModel> MediaAssets { get; set; } = new();

    [JsonPropertyName("boards")]
    public List<BoardInfoViewModel> Boards { get; set; } = new();

    [JsonPropertyName("chapters")]
    public List<ChapterViewModel> Chapters { get; set; } = new();
}
