//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace TechHub.QuestionBank.Core.Response;

//public class QuestionPreviewResponse : BaseResponse
//{
//	public Guid JobId { get; set; }
//	public int TotalExtracted { get; set; }
//	public List<QuestionPreviewItem> Questions { get; set; } = new();
//}

//public class QuestionPreviewItem
//{
//	public Guid QuestionId { get; set; }
//	public int? QuestionNumber { get; set; }
//	public string QuestionType { get; set; } = string.Empty;
//	public string? QuestionHtml { get; set; }
//	public string? ContentParts { get; set; }
//	public bool HasLatex { get; set; }
//	public bool HasImages { get; set; }
//	public bool IsPartial { get; set; }
//	public string DifficultyLevel { get; set; } = string.Empty;
//	public int MarksAllocation { get; set; }
//	public string Status { get; set; } = string.Empty;
//	public List<OptionPreviewDto> Options { get; set; } = new();
//}
