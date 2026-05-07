using MongoDB.Bson.Serialization.Attributes;

namespace TechHub.Core.Entities.Board;

public class LessonInfo
{
    [BsonElement("topic")]
    public string Topic { get; set; } = string.Empty;

    [BsonElement("subTopic")]
    public string SubTopic { get; set; } = string.Empty;

    [BsonElement("aim")]
    public string Aim { get; set; } = string.Empty;

    [BsonElement("subject")]
    public SubjectInfo Subject { get; set; } = new();

    [BsonElement("classroom")]
    public ClassroomInfo Classroom { get; set; } = new();
}
