using System.Text.Json.Serialization;

namespace TechHubComm.Models
{
    public class BoardUpdateMessage
    {
        [JsonPropertyName("messageId")]
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("userRole")]
        public string UserRole { get; set; } = string.Empty;

        [JsonPropertyName("classroomId")]
        public string ClassroomId { get; set; } = string.Empty;

        [JsonPropertyName("subjectId")]
        public string SubjectId { get; set; } = string.Empty;

        [JsonPropertyName("topicId")]
        public string TopicId { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("action")]
        public BoardAction Action { get; set; } = new();

        [JsonPropertyName("data")]
        public object? Data { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class BoardAction
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("operation")]
        public string Operation { get; set; } = string.Empty;

        [JsonPropertyName("elementId")]
        public string? ElementId { get; set; }

        [JsonPropertyName("coordinates")]
        public Coordinates? Coordinates { get; set; }

        [JsonPropertyName("properties")]
        public Dictionary<string, object>? Properties { get; set; }
    }

    public class Coordinates
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("width")]
        public double? Width { get; set; }

        [JsonPropertyName("height")]
        public double? Height { get; set; }
    }

    // Additional supporting classes for different board elements
    public class DrawingElement
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty; // "line", "rectangle", "circle", "text", etc.

        [JsonPropertyName("coordinates")]
        public Coordinates Coordinates { get; set; } = new();

        [JsonPropertyName("style")]
        public DrawingStyle Style { get; set; } = new();

        [JsonPropertyName("content")]
        public string? Content { get; set; } // For text elements

        [JsonPropertyName("createdBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class DrawingStyle
    {
        [JsonPropertyName("color")]
        public string Color { get; set; } = "#000000";

        [JsonPropertyName("strokeWidth")]
        public int StrokeWidth { get; set; } = 2;

        [JsonPropertyName("fillColor")]
        public string? FillColor { get; set; }

        [JsonPropertyName("opacity")]
        public double Opacity { get; set; } = 1.0;

        [JsonPropertyName("fontSize")]
        public int? FontSize { get; set; }

        [JsonPropertyName("fontFamily")]
        public string? FontFamily { get; set; }
    }
}
