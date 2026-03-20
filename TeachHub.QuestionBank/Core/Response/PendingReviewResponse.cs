using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.DTO;

namespace TechHub.QuestionBank.Core.Response;

// TechHub.QuestionBank/ViewModels/ReviewViewModels.cs

/// <summary>
/// Response for GetPendingReviewQuestions
/// Contains original file and all questions to review
/// </summary>
public class PendingReviewResponse : BaseResponse
{
	// Original file — shown alongside questions
	public string OriginalFileUrl { get; set; }
	public string OriginalFileName { get; set; }
	public string FileType { get; set; }

	// Session progress
	public int TotalExtracted { get; set; }
	public int TotalConfirmed { get; set; }
	public int TotalRejected { get; set; }
	public int TotalPending { get; set; }

	// Questions awaiting review
	public List<PendingReviewItem> Questions { get; set; }
		= new List<PendingReviewItem>();

	public int SessionStatus { get; set; }
	public string SessionStatusName { get; set; }
}

/// <summary>
/// One question in the review queue
/// Includes AI confidence metadata
/// to help teacher prioritise review effort
/// </summary>
public class PendingReviewItem
{
	public Guid QuestionId { get; set; }
	public string Title { get; set; }
	public string TextContent { get; set; }
	public string SubjectName { get; set; }
	public string Topic { get; set; }
	public string SubTopic { get; set; }
	public int QuestionType { get; set; }
	public string QuestionTypeName { get; set; }
	public int DifficultyLevel { get; set; }
	public string DifficultyLevelName { get; set; }
	public int MarksAllocation { get; set; }

	public int? ExtractedQuestionIndex { get; set; }
	// Position in original document
	// Teacher can cross-reference with original file

	public string AIConfidenceScore { get; set; }
	// Raw score e.g "0.87"

	public string AIConfidenceLabel { get; set; }
	// "High", "Medium", "Low"

	public bool NeedsCloseReview { get; set; }
	// True when confidence is Low
	// UI highlights these for teacher attention

	public List<QuestionOptionDto> Options { get; set; }
		= new List<QuestionOptionDto>();
}
