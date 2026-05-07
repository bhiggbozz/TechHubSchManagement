using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class BoardDimensions
{
    [BsonElement("width")]
    public int Width { get; set; }

    [BsonElement("height")]
    public int Height { get; set; }
}
