using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.DTO;

public class JobQuestionRow
{
	public Guid Id { get; set; }
	public int QuestionType { get; set; }
	public string QuestionHtml { get; set; }
	public string ContentParts { get; set; }
	public bool HasLatex { get; set; }
	public bool HasMedia { get; set; }
	public string? CorrectAnswer { get; set; }
	public int DifficultyLevel { get; set; }
	public int MarksAllocation { get; set; }
	public int Status { get; set; }
	public string CreationDate { get; set; }
	public string? SubTopicName { get; set; }
	public string? TopicName { get; set; }
}

public class JobQuestionDto
{
	public Guid Id { get; set; }
	public int QuestionType { get; set; }
	public string QuestionTypeName { get; set; }
	public string QuestionHtml { get; set; }
	public string ContentParts { get; set; }
	public bool HasLatex { get; set; }
	public bool HasMedia { get; set; }
	public string? CorrectAnswer { get; set; }
	public int DifficultyLevel { get; set; }
	public int MarksAllocation { get; set; }
	public int Status { get; set; }
	public string StatusName { get; set; }
	public string? SubTopicName { get; set; }
	public string? TopicName { get; set; }
	public string CreationDate { get; set; }
	public List<JobQuestionOptionDto> Options { get; set; } = new();
}

public class JobQuestionOptionDto
{
	public Guid Id { get; set; }
	public string OptionLabel { get; set; }
	public string OptionText { get; set; }
	public string OptionHtml { get; set; }
	public string ContentParts { get; set; }
	public bool IsCorrect { get; set; }
	public bool HasLatex { get; set; }
	public bool HasImages { get; set; }
	public int OrderIndex { get; set; }
}
