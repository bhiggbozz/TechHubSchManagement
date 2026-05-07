using System.Text.Json.Serialization;

namespace TechHub.Core.ViewModels.Board.Manifest;

public class ClassroomInfoViewModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
