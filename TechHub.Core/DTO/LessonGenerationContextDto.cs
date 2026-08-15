using System;

namespace TechHub.Core.DTO;

/// <summary>
/// Lightweight context used to build an AI prompt from a lesson's
/// aim + objectives. Populated by a single indexed join query.
/// </summary>
public class LessonGenerationContextDto
{
	public Guid LessonId { get; set; }
	public Guid CreatedBy { get; set; }
	public string Aim { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string? SubjectName { get; set; }
	public string? TopicName { get; set; }
	public string? SubTopicName { get; set; }
	public string? ClassName { get; set; }
	public string? SchoolName { get; set; }
}
