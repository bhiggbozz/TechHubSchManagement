using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.DTO;

public class LessonForClassDto
{
	public Guid Id { get; set; }
	public string Aim { get; set; }
	public string Description { get; set; }
	public string Status { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public string SubTopic { get; set; }
	public Guid SubTopicId { get; set; }

	// Classroom
	public Guid ClassroomId { get; set; }
	public string ClassName { get; set; }

	// Subject
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; }

	// Topic
	public Guid TopicId { get; set; }
	public string TopicName { get; set; }

	// People
	public Guid TeacherId { get; set; }
	public string TeacherName { get; set; }
	public string TeacherEmail { get; set; }
	public string ApprovedByName { get; set; }
}

public class LessonMediaDto
{
	public Guid Id { get; set; }
	public string FileName { get; set; }
	public string OriginalFileName { get; set; }
	public string FileExtension { get; set; }
	public string MediaType { get; set; }
	public string CloudinaryUrl { get; set; }
	public string PublicId { get; set; }
	public long FileSizeBytes { get; set; }
	public int? Duration { get; set; }
	public int DisplayOrder { get; set; }
	public string? MetaData { get; set; }
}
