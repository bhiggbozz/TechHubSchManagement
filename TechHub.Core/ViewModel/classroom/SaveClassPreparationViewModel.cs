using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Core.ViewModel.classroom;

/// <summary>
/// Save class preparation as draft (can be saved multiple times before submission)
/// </summary>
public class SaveClassPreparationViewModel
{
	/// <summary>
	/// NULL for new class, GUID for updating existing draft
	/// </summary>
	public Guid? Id { get; set; }

	[Required(ErrorMessage = "Subject is required")]
	public Guid SubjectId { get; set; }

	[Required(ErrorMessage = "Classroom is required")]
	public Guid ClassroomId { get; set; }

	[Required(ErrorMessage = "Topic is required")]
	[MaxLength(200, ErrorMessage = "Topic cannot exceed 200 characters")]
	public string Topic { get; set; } = string.Empty;

	[MaxLength(200, ErrorMessage = "Sub-topic cannot exceed 200 characters")]
	public string? SubTopic { get; set; }

	[Required(ErrorMessage = "Aim and objectives are required")]
	public string AimAndObjectives { get; set; } = string.Empty;

	public DateTime? ScheduledDate { get; set; }

	/// <summary>
	/// Time in 24-hour format "14:30" or 12-hour "2:30 PM"
	/// </summary>
	public string? ScheduledTime { get; set; }

	[Range(5, 480, ErrorMessage = "Duration must be between 5 and 480 minutes")]
	public int? DurationMinutes { get; set; }

	[Required(ErrorMessage = "Class type is required")]
	public ClassType ClassType { get; set; }

	/// <summary>
	/// Media file IDs (already uploaded via UploadMedia endpoint)
	/// </summary>
	public List<Guid> MediaFileIds { get; set; } = new();
}

/// <summary>
/// Upload media file request
/// Note: IFormFile comes from [FromForm] in controller
/// </summary>
public class UploadMediaRequest
{
	[Required(ErrorMessage = "Media type is required")]
	public MediaType MediaType { get; set; }

	[MaxLength(200)]
	public string? DisplayName { get; set; }
}
