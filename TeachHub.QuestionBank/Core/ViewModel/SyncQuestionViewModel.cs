using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.ViewModel;

/// <summary>
/// Represents one question in a sync batch
/// Can be a new question or an edited existing question
/// Extends CreateQuestionViewModel with sync-specific fields
/// </summary>
public class SyncQuestionViewModel
{
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// OFFLINE IDENTITY
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string ClientId { get; set; }
	// Device generated ID
	// Always present regardless of whether
	// question is new or previously synced

	public Guid? ServerId { get; set; }
	// Null  = new question, never synced before
	// Guid  = previously synced, now edited offline
	// This is what determines new vs existing

	public string OriginDevice { get; set; }
	public string CreatedAtDevice { get; set; }
	public string EditedAtDevice { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// SYNC STATE
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public QuestionStatus Status { get; set; }
	// Current status on device
	// PendingReview → blocked from sync
	// Draft         → allowed to sync

	public string LastKnownModifiedDate { get; set; }
	// ModifiedDate the device had when
	// teacher started editing
	// Backend uses this for dirty detection

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// CLASSIFICATION
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public Guid SubjectId { get; set; }
	public Guid? TopicId { get; set; }
	public Guid SubTopicId { get; set; }

	public string Topic { get; set; }
	public string SubTopic { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// CONTENT
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string Title { get; set; }
	public string TextContent { get; set; }
	public QuestionType QuestionType { get; set; }
	public DifficultyLevel DifficultyLevel { get; set; }
	public int MarksAllocation { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// OPTIONS (MCQ)
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public List<CreateOptionViewModel> Options { get; set; }
		= new List<CreateOptionViewModel>();

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// BOARD
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public Guid? BoardSessionId { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// SCAN SESSION
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public Guid? ScanSessionId { get; set; }
	public bool IsScanned { get; set; }
	public int? ExtractedQuestionIndex { get; set; }
	public string AIConfidenceScore { get; set; }



    private CreateQuestionViewModel MapToCreateViewModel(SyncQuestionViewModel model)
	{
		return new CreateQuestionViewModel
		{
			ClientId = model.ClientId,
			OriginDevice = model.OriginDevice,
			CreatedAtDevice = model.CreatedAtDevice,
			SubjectId = model.SubjectId,
			TopicId = model.TopicId,
			Topic = model.Topic,
			SubTopic = model.SubTopicId,
			Title = model.Title,
			TextContent = model.TextContent,
			QuestionType = model.QuestionType,
			DifficultyLevel = model.DifficultyLevel,
			MarksAllocation = model.MarksAllocation,
			Options = model.Options,
			BoardSessionId = model.BoardSessionId,
			ScanSessionId = model.ScanSessionId,
			IsScanned = model.IsScanned,
			ExtractedQuestionIndex = model.ExtractedQuestionIndex,
			AIConfidenceScore = model.AIConfidenceScore
		};
	}
}


