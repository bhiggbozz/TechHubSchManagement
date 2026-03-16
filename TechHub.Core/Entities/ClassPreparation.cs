using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;
public class ClassPreparation
{
	public string TeacherName;
	public bool IsUrgent;
	public bool NeedsReview;

	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid ClassroomId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid TeacherId { get; set; }
	public Guid SchoolId { get; set; }

	public string Title { get; set; } = string.Empty;
	public string Topic { get; set; } = string.Empty;
	public string? SubTopic { get; set; }
	public string AimAndObjectives { get; set; } = string.Empty;

	public DateTime? ScheduledDate { get; set; }
	public TimeSpan? ScheduledTime { get; set; }
	public int? DurationMinutes { get; set; }

	public int ClassType { get; set; }
	public int Status { get; set; }

	public DateTime? SubmittedForApprovalDate { get; set; }
	public Guid? SubmittedBy { get; set; }
	public Guid? ApprovedBy { get; set; }
	public DateTime? ApprovedDate { get; set; }
	public Guid? RejectedBy { get; set; }
	public DateTime? RejectedDate { get; set; }
	public string? RejectionReason { get; set; }

	public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
	public string ModifiedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
	public Guid CreatedBy { get; set; }
	public bool IsActive { get; set; }
}

