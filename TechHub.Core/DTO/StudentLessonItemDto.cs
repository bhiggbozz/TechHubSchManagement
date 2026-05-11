using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.DTO;

public class StudentLessonItemDto
{
	public Guid Id { get; set; }
	public string Aim { get; set; }
	public string Description { get; set; }
	public string Status { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public string SubTopic { get; set; }
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; }
	public Guid TopicId { get; set; }
	public string TopicName { get; set; }
	public string TeacherName { get; set; }
	public int MediaCount { get; set; }
}

