using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.DTO;

/// <summary>
/// Lightweight version of QuestionDto
/// Used in list responses to save memory
/// Full detail fetched only when question is opened
/// </summary>
public class QuestionSummaryDto
{
	public Guid Id { get; set; }
	public string ClientId { get; set; }
	public string Title { get; set; }
	public string SubjectName { get; set; }
	public string Topic { get; set; }
	public int QuestionType { get; set; }
	public string QuestionTypeName { get; set; }
	public int DifficultyLevel { get; set; }
	public string DifficultyLevelName { get; set; }
	public int MarksAllocation { get; set; }
	public bool HasBoardSession { get; set; }
	public bool HasMedia { get; set; }
	public bool IsScanned { get; set; }
	public int Status { get; set; }
	public string StatusName { get; set; }
	public string CreationDate { get; set; }
	public string? TopicName { get; set; }  
	public string? SubTopicName { get; set; } 
	public string? ClassName { get; set; }
	public string? BoardSessionId { get; set; }  // ← add
    public string? SnapshotUrl { get; set; }
}

