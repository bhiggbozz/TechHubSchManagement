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

public class QuestionListResponse : BaseResponse
{
	public List<QuestionSummaryDto> Questions { get; set; }
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public bool HasMore { get; set; }
}

