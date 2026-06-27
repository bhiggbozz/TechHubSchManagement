using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.DTO;

public class AdminLessonItemDto
{
	public Guid Id { get; set; }
	public string Aim { get; set; }
	public string Description { get; set; }
	public string Status { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public string SubTopic { get; set; }
	public string? RejectionReason { get; set; }
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; }
	public Guid TopicId { get; set; }
	public string TopicName { get; set; }
	public Guid? SubTopicId { get; set; }  
	public string? SubTopicName { get; set; }  
	public Guid ClassroomId { get; set; }  
	public string ClassName { get; set; }  
	public string TeacherName { get; set; }
	public string? ApprovedByName { get; set; }
	public int MediaCount { get; set; }
	public string? QuizCode { get; set; }
	public DateTime? AccessDate { get; set; }  
	public TimeSpan? AccessTime { get; set; }  
	public int? DurationMinutes { get; set; }  
	public DateTime? AccessEndsAt { get; set; } 
}


public class LessonWithMediaDto : AdminLessonItemDto
{
	public Guid? SubTopicId { get; set; }
	public string? SubTopicName { get; set; }
	public Guid ClassroomId { get; set; }
	public string ClassName { get; set; }
	public DateTime? AccessDate { get; set; }
	public TimeSpan? AccessTime { get; set; }
	public int? DurationMinutes { get; set; }
	public DateTime? AccessEndsAt { get; set; }
	public List<LessonMediaItemDto> Media { get; set; } = new();
}

public class LessonMediaItemDto
{
	public Guid LessonContentId { get; set; }
	public Guid MediaId { get; set; }
	public string MediaName { get; set; }
	public string Url { get; set; }
	public string MediaType { get; set; }
	public string FileExtension { get; set; }
	public long FileSizeBytes { get; set; }
	public int DisplayOrder { get; set; }
}