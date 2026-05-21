using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.DTO;

public class TeacherLessonRow
{
	public Guid Id { get; set; }
	public string Aim { get; set; }
	public string Description { get; set; }
	public string Status { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime ModifiedAt { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public string? RejectionReason { get; set; }
	public DateTime? AccessDate { get; set; }
	public TimeSpan? AccessTime { get; set; }
	public int? DurationMinutes { get; set; }
	public DateTime? AccessEndsAt { get; set; }
	public string SubjectName { get; set; }
	public string TopicName { get; set; }
	public string SubTopicName { get; set; }
	public string ClassName { get; set; }
	public string? ApprovedByName { get; set; }
}
