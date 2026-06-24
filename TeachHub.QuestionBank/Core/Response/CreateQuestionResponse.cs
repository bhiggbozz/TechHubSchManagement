using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.DTO;

namespace TechHub.QuestionBank.Core.Response;

public class CreateQuestionResponse :BaseResponse
{
	public Guid QuestionId { get; set; }
	public string ClientId { get; set; }
	public bool IsDuplicate { get; set; }
	// Returns ClientId so frontend knows
	// which local record this maps to
}

public class BatchQuestionResult
{
	public string ClientId { get; set; } = string.Empty;
	public Guid? QuestionId { get; set; }
	public bool Success { get; set; }
	public string? ErrorMessage { get; set; }
	public bool IsDuplicate { get; set; }
}

public class CreateQuestionsBatchResponse : BaseResponse
{
	public List<BatchQuestionResult> Results { get; set; } = new();
	public int TotalCount { get; set; }
	public int SuccessCount { get; set; }
	public int FailedCount { get; set; }
}

public class QuestionListResponse : BaseResponse
{
	public List<QuestionSummaryDto> Questions { get; set; }
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public bool HasMore { get; set; }
}

