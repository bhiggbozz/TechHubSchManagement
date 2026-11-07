using System;

namespace TechHub.QuestionBank.Core.Model;

/// <summary>Display names for where an extraction job was filed (used by the review preview).</summary>
public class PreviewFilingRow
{
	public Guid? TopicId { get; set; }
	public string? SubTopicName { get; set; }
	public string? TopicName { get; set; }
	public string? SubjectName { get; set; }
	public string? ClassroomName { get; set; }
}
