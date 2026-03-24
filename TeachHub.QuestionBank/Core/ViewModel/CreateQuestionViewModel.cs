using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.ViewModel;

// TechHub.QuestionBank/ViewModels/QuestionViewModels.cs

public class CreateQuestionViewModel
{

	public string ClientId { get; set; }
	public string OriginDevice { get; set; }
	public string CreatedAtDevice { get; set; }
	public Guid SubjectId { get; set; }
	public Guid? TopicId { get; set; }
	public string Topic { get; set; }
	public string SubTopic { get; set; }

	public string Title { get; set; }
	public string TextContent { get; set; }
	public QuestionType QuestionType { get; set; }
	public DifficultyLevel DifficultyLevel { get; set; }
	public int MarksAllocation { get; set; }

	public List<CreateOptionViewModel> Options { get; set; }
		= new List<CreateOptionViewModel>();
	public Guid? BoardSessionId { get; set; }

	public Guid? ScanSessionId { get; set; }
	public bool IsScanned { get; set; }
	public int? ExtractedQuestionIndex { get; set; }
	public string? AIConfidenceScore { get; set; }


}

public class CreateOptionViewModel
{
	public string OptionLabel { get; set; }
	public string OptionText { get; set; }
	public bool IsCorrect { get; set; }
	public int OrderIndex { get; set; }
}

public class UpdateQuestionViewModel
{
	
	public Guid QuestionId { get; set; }

	public string ClientId { get; set; }
	// Device generated ID
	// Needed so response can map back
	// to correct local record after sync

	public string OriginDevice { get; set; }
	

	public Guid SubjectId { get; set; }
	public Guid? TopicId { get; set; }
	public string Topic { get; set; }
	public string SubTopic { get; set; }


	public string Title { get; set; }
	public string TextContent { get; set; }
	public QuestionType QuestionType { get; set; }
	public DifficultyLevel DifficultyLevel { get; set; }
	public int MarksAllocation { get; set; }


	public List<CreateOptionViewModel> Options { get; set; }= new List<CreateOptionViewModel>();

	public Guid? BoardSessionId { get; set; }
	// Teacher may attach or change board session
	// during edit


	public string LastKnownModifiedDate { get; set; }
	// What the frontend had as ModifiedDate
	// when teacher started editing offline
	// Backend compares this against server ModifiedDate
	// Mismatch = conflict detected before any overwrite

	public string EditedAtDevice { get; set; }
	// Timestamp of when teacher made the edit on device
	// Helps with conflict resolution display
	// "You edited this at 10:32am on your phone"
	
}



public class QuestionFilterViewModel
{
	public QuestionType? QuestionType { get; set; }
	public DifficultyLevel? DifficultyLevel { get; set; }
	public QuestionStatus? Status { get; set; }
	public string SearchText { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;

	public bool IncludePendingReview { get; set; } = false;
	// Default false
	// Question bank never shows unverified questions
	// Review screen sets this to true explicitly

	public Guid? ScanSessionId { get; set; }
	// Filter by scan session
	// Used on review screen to show all
	// questions from one upload together
}

// Sync ViewModels
/// <summary>
/// The full sync batch payload sent by frontend
/// </summary>
public class SyncQuestionsViewModel
{
	public List<SyncQuestionViewModel> Questions { get; set; }= new List<SyncQuestionViewModel>();

	public string DeviceId { get; set; }
	public string SyncedAt { get; set; }
}

