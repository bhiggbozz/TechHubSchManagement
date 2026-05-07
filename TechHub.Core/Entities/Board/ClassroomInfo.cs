using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class ClassroomInfo
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;
}
