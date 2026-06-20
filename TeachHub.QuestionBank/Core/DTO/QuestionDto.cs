using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.DTO;
/// <summary>
/// Question data transfer object
/// Used for all API responses
/// Never exposes internal DB fields directly
/// </summary>
public class QuestionDto
{
	public Guid Id { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// IDENTITY
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string ClientId { get; set; }
	// Returned so frontend can reconcile
	// local record with server record after sync

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// CLASSIFICATION
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; }
	public Guid? TopicId { get; set; }
	public string Topic { get; set; }
	public string SubTopic { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// CONTENT
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string Title { get; set; }
	public string TextContent { get; set; }
	public int QuestionType { get; set; }
	public string QuestionTypeName { get; set; }
	// e.g "MultipleChoice", "Essay"
	public int DifficultyLevel { get; set; }
	public string DifficultyLevelName { get; set; }
	// e.g "Easy", "Hard"
	public int MarksAllocation { get; set; }
	public string? CorrectAnswer { get; set; }
	// Model answer / reference answer
	// Required for TrueFalse ("True"/"False")
	// Optional for Essay, ShortAnswer, FillInTheBlank

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// OPTIONS (MCQ only)
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public List<QuestionOptionDto> Options { get; set; }
		= new List<QuestionOptionDto>();

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// BOARD & MEDIA
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public Guid? BoardSessionId { get; set; }
	public bool HasBoardSession { get; set; }
	public string? BoardSnapshotUrl { get; set; }
	// CDN url of board snapshot
	// Null if no board session attached
	public bool HasMedia { get; set; }
	public bool HasAudio { get; set; }


	public bool IsScanned { get; set; }
	// True if question came from scan feature


	public int Status { get; set; }
	public string StatusName { get; set; }
	// e.g "Draft", "Published"

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// SYNC STATE
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public int SyncStatus { get; set; }
	public string SyncStatusName { get; set; }
	public string LastSyncedAt { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// PERMISSIONS (for frontend rendering decisions)
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public bool CanEdit { get; set; }
	public bool CanDelete { get; set; }
	public bool CanPublish { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// AUDIT
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string CreatedByName { get; set; }
	public string CreationDate { get; set; }
	public string ModifiedDate { get; set; }
}

/// <summary>
/// Option DTO for MCQ questions
/// IsCorrect only included for teacher view
/// Hidden from student view at controller level
/// </summary>
public class QuestionOptionDto
{
	public Guid Id { get; set; }
	public string OptionLabel { get; set; }
	public string OptionText { get; set; }
	public bool IsCorrect { get; set; }
	public int OrderIndex { get; set; }
}

