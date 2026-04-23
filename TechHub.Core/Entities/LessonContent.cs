using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class LessonContent
{
	public Guid Id { get; set; }
	public Guid SchoolId { get; set; }
	public Guid ClassroomId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid TopicId { get; set; }
	public string SubTopic { get; set; }
	public string Aim { get; set; }
	public string Description { get; set; }
	public string Status { get; set; }
	public Guid CreatedBy { get; set; }
	public Guid? ApprovedBy { get; set; }
	public Guid? RejectedBy { get; set; }
	public string RejectionReason { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime ModifiedAt { get; set; }
	public DateTime? ApprovedAt { get; set; }
}

