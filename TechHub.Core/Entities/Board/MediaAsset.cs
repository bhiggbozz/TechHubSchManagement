using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class MediaAsset
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("url")]
    public string Url { get; set; } = string.Empty;
}
