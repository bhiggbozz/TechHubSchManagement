using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.classroom;

public class CreateAssessmentViewModel
{
	public string Title { get; set; }
	public string? Description { get; set; }
	public Guid SchoolId { get; set; }
	public int TimeLimitMinutes { get; set; }
	public bool ShuffleQuestions { get; set; }
	public int PassMarkPercent { get; set; } = 50;
	public bool ShowResultImmediately { get; set; } = true;
	public int EasyMarks { get; set; } = 1;
	public int MediumMarks { get; set; } = 2;
	public int HardMarks { get; set; } = 3;
	public int ExamLevelMarks { get; set; } = 5;
	public List<Guid> QuestionIds { get; set; } = new();
}

public class AssignAssessmentViewModel
{
	public Guid AssessmentId { get; set; }
	public string TargetType { get; set; } // Student | Subject | Classroom
	public List<Guid> TargetIds { get; set; } = new();
}

public class AssessmentListItemDto
{
	public Guid AssessmentId { get; set; }
	public string Code { get; set; }
	public string Title { get; set; }
	public string? Description { get; set; }
	public int TimeLimitMinutes { get; set; }
	public int QuestionCount { get; set; }
	public int? AttemptNumber { get; set; }
	public decimal? FinalScorePercent { get; set; }
	public bool? IsPassed { get; set; }
	public string Status { get; set; } // NotStarted | InProgress | Completed
}

public class AssessmentDetailDto
{
	public Guid AssessmentId { get; set; }
	public string Code { get; set; }
	public string Title { get; set; }
	public string? Description { get; set; }
	public int TimeLimitMinutes { get; set; }
	public int PassMarkPercent { get; set; }
	public bool ShuffleQuestions { get; set; }
	public bool ShowResultImmediately { get; set; }
	public int QuestionCount { get; set; }
	public int TotalMarks { get; set; }
	public List<AssessmentQuestionDto> Questions { get; set; } = new();
}

public class AssessmentQuestionDto
{
	public Guid QuestionId { get; set; }
	public string Title { get; set; }
	public string TextContent { get; set; }
	public int QuestionType { get; set; }
	public int DifficultyLevel { get; set; }
	public decimal MarksAllocation { get; set; }
	public int DisplayOrder { get; set; }
	public List<AssessmentOptionDto> Options { get; set; } = new();
}

public class AssessmentOptionDto
{
	public Guid QuestionId { get; set; }
	public Guid OptionId { get; set; }
	public string OptionLabel { get; set; }
	public string OptionText { get; set; }
}

public class SubmitAssessmentAnswerViewModel
{
	public Guid AttemptId { get; set; }
	public Guid QuestionId { get; set; }
	public Guid? SelectedOptionId { get; set; }
	public string? TypedAnswer { get; set; }
	public string? BoardSessionId { get; set; }
	public string? AudioUrl { get; set; }
	public bool IsSkipped { get; set; }
}

public class AssessmentAttemptDto
{
	public Guid AttemptId { get; set; }
	public int AttemptNumber { get; set; }
	public bool IsOfficial { get; set; }
	public string Status { get; set; }
	public decimal? FinalScorePercent { get; set; }
	public bool? IsPassed { get; set; }
	public int? TimeTakenSeconds { get; set; }
	public string? SubmittedAt { get; set; }
}

public class AssessmentResultDto
{
	public Guid AttemptId { get; set; }
	public string AssessmentCode { get; set; }
	public string Title { get; set; }
	public int AttemptNumber { get; set; }
	public bool IsOfficial { get; set; }
	public decimal TotalMarks { get; set; }
	public decimal AutoMarksObtained { get; set; }
	public decimal ManualMarksObtained { get; set; }
	public decimal FinalScorePercent { get; set; }
	public bool? IsPassed { get; set; }
	public string Status { get; set; }
	public string? SubmittedAt { get; set; }
	public List<AssessmentAnswerResultDto> Answers { get; set; } = new();
}

public class AssessmentAnswerResultDto
{
	public Guid QuestionId { get; set; }
	public int QuestionType { get; set; }
	public decimal MaxMarks { get; set; }
	public decimal MarksObtained { get; set; }
	public bool? IsCorrect { get; set; }
	public string? TypedAnswer { get; set; }
	public string? TeacherFeedback { get; set; }
	public bool IsSkipped { get; set; }
}
