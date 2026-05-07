using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class BoardInfo
{
    [BsonElement("index")]
    public int Index { get; set; }

    [BsonElement("dimensions")]
    public BoardDimensions Dimensions { get; set; } = new();

    [BsonElement("strokeCount")]
    public int StrokeCount { get; set; }
}
