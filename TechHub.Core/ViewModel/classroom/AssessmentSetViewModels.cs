using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.classroom;

// ── Create / Update ────────────────────────────────────────────────────────
public class CreateAssessmentSetViewModel
{
	public string Name { get; set; } = string.Empty;
	public string Label { get; set; } = "Quiz";
	public bool AllowRetakes { get; set; } = false;
	public int MaxAttempts { get; set; } = 1;
	public int PassMarkPercent { get; set; } = 50;
	public int? TimeLimitMinutes { get; set; }
	public bool AutoSubmitOnTimeout { get; set; } = true;
	public bool ShuffleQuestions { get; set; } = false;
	public string ShowResultMode { get; set; } = "Immediate"; // Immediate | AfterAllSubmit | Manual
	public bool ShowCorrectAnswers { get; set; } = false;
	public bool AllowBoardAnswer { get; set; } = true;
	public int EasyMarks { get; set; } = 1;
	public int MediumMarks { get; set; } = 2;
	public int HardMarks { get; set; } = 3;
	public int ExamLevelMarks { get; set; } = 5;
}

public class UpdateAssessmentSetViewModel : CreateAssessmentSetViewModel { }

// ── DTOs ───────────────────────────────────────────────────────────────────
public class AssessmentSetDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Label { get; set; } = "Quiz";
	public Guid TeacherId { get; set; }
	public Guid SchoolId { get; set; }
	public bool AllowRetakes { get; set; }
	public int MaxAttempts { get; set; }
	public int PassMarkPercent { get; set; }
	public int? TimeLimitMinutes { get; set; }
	public bool AutoSubmitOnTimeout { get; set; }
	public bool ShuffleQuestions { get; set; }
	public string ShowResultMode { get; set; } = "Immediate";
	public bool ShowCorrectAnswers { get; set; }
	public bool AllowBoardAnswer { get; set; }
	public int EasyMarks { get; set; }
	public int MediumMarks { get; set; }
	public int HardMarks { get; set; }
	public int ExamLevelMarks { get; set; }
	public bool IsActive { get; set; }
	public string CreationDate { get; set; } = string.Empty;
	public string ModifiedDate { get; set; } = string.Empty;
}

public class AssessmentSetSummaryDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Label { get; set; } = "Quiz";
	public int? TimeLimitMinutes { get; set; }
	public bool AllowRetakes { get; set; }
	public int MaxAttempts { get; set; }
	public string ShowResultMode { get; set; } = "Immediate";
	public int EasyMarks { get; set; }
	public int MediumMarks { get; set; }
	public int HardMarks { get; set; }
	public int ExamLevelMarks { get; set; }
	public bool IsActive { get; set; }
}

// ── Lesson attachment ──────────────────────────────────────────────────────
public class AttachAssessmentSetViewModel
{
	public Guid AssessmentSetId { get; set; }
}
