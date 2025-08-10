namespace TechHubComm.Models
{
    public class ClassroomContext
    {
        public string ClassroomId { get; set; } = string.Empty;
        public string SubjectId { get; set; } = string.Empty;
        public string TopicId { get; set; } = string.Empty;
        public string SchoolId { get; set; } = string.Empty; // Added for multi-tenancy
        public string GroupKey => $"{SchoolId}:{ClassroomId}:{SubjectId}:{TopicId}";
    }
}
