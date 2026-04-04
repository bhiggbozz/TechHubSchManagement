using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Response;

// =====================================================
// QUESTIONJOB RESPONSES
// =====================================================

/// <summary>
/// Returned immediately after teacher uploads image
/// Teacher stores JobId to poll status later
/// </summary>
public class SubmitJobResponse : BaseResponse
{
	public Guid JobId { get; set; }

	// "Pending" — job logged, image saved
	// Teacher polls GetJobStatus with this JobId
	public string Status { get; set; }

	public string Message { get; set; } = "Your question is being processed. Check back shortly.";
}

/// <summary>
/// Returned when teacher polls job status
/// </summary>
public class JobStatusResponse : BaseResponse
{
	public Guid JobId { get; set; }

	// Pending | Processing | Completed | Failed
	public string Status { get; set; }

	// Populated when Status = Completed
	// Teacher uses this to fetch the processed question
	public Guid? QuestionId { get; set; }

	// Populated when Status = Failed
	// Teacher knows why it failed and can retry
	public string? FailureReason { get; set; }

	// How many times the background worker tried
	public int AttemptCount { get; set; }

	public string CreatedAt { get; set; }
	public string? CompletedAt { get; set; }
}

/// <summary>
/// Returned when teacher fetches their job list
/// Lightweight — no question content
/// </summary>
public class JobSummaryDto
{
	public Guid JobId { get; set; }
	public string QuestionType { get; set; }
	public string Status { get; set; }
	public Guid? QuestionId { get; set; }
	public string? FailureReason { get; set; }
	public string CreatedAt { get; set; }
	public string? CompletedAt { get; set; }
}

public class JobListResponse : BaseResponse
{
	public List<JobSummaryDto> Jobs { get; set; } = new();
	public int TotalCount { get; set; }
	public int PendingCount { get; set; }
	public int CompletedCount { get; set; }
	public int FailedCount { get; set; }
}

/// <summary>
/// Returned when teacher fetches the processed question
/// for preview and editing before publishing
/// </summary>
public class QuestionPreviewResponse : BaseResponse
{
	public Guid QuestionId { get; set; }
	public Guid JobId { get; set; }
	public string QuestionType { get; set; }

	// Claude-generated HTML — render directly
	// Frontend: DOMPurify.sanitize → innerHTML → KaTeX
	public string QuestionHtml { get; set; }

	// Claude-generated JSON — for editing
	public string ContentParts { get; set; }

	// Options for Objective questions
	public List<OptionPreviewDto> Options { get; set; } = new();

	public bool HasLatex { get; set; }
	public bool HasImages { get; set; }
	public string DifficultyLevel { get; set; }
	public int MarksAllocation { get; set; }
	public string Status { get; set; }
}

public class OptionPreviewDto
{
	public Guid Id { get; set; }
	public string OptionLabel { get; set; }
	public string? OptionText { get; set; }
	public string? OptionHtml { get; set; }
	public string? ContentParts { get; set; }
	public bool IsCorrect { get; set; }
	public bool HasLatex { get; set; }
	public bool HasImages { get; set; }
	public int OrderIndex { get; set; }
}

