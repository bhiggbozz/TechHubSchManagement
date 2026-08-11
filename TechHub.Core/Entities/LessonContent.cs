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
	public string? QuizCode { get; set; }

	public DateTime? AccessDate { get; set; }
	public TimeSpan? AccessTime { get; set; }
	public int? DurationMinutes { get; set; }
	public DateTime? AccessEndsAt { get; set; }
	public Guid? AssessmentSetId { get; set; }

	/// <summary>
	/// Whether the system should auto-generate an AI image for this lesson
	/// once it is approved. Set by the teacher at submit time.
	/// </summary>
	public bool ShouldGenerateImage { get; set; } = true;

	/// <summary>
	/// Optional teacher-supplied words describing the materials / visuals the
	/// generated image must include. Combined with the lesson aim + objectives.
	/// </summary>
	public string? ImageMaterialWords { get; set; }

	/// <summary>
	/// Number of AI images to generate for this lesson once it is approved.
	/// </summary>
	public int ImageCount { get; set; } = 1;
}

