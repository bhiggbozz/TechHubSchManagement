using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

//public class Chapter
//{
//    [BsonElement("timestampMs")]
//    public long TimestampMs { get; set; }

//    [BsonElement("label")]
//    public string Label { get; set; } = string.Empty;
//}

public class Chapter
{
	[BsonElement("title")]
	public string Title { get; set; }

	[BsonElement("startMs")]
	public long StartMs { get; set; }

	[BsonElement("endMs")]
	public long EndMs { get; set; }
}
