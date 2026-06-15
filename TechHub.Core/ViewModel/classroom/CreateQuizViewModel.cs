using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.classroom;

public class CreateQuizViewModel
{
	public List<Guid> QuestionIds { get; set; } = new();
}

public class AttachQuizViewModel
{
	public string QuizCode { get; set; } = string.Empty;
}

public class QuizQuestionDto
{
	public Guid QuestionId { get; set; }
	public string Title { get; set; }
	public string TextContent { get; set; }
	public int QuestionType { get; set; }
	public int DifficultyLevel { get; set; }
	public int MarksAllocation { get; set; }
	public string SubjectName { get; set; }
	public string TopicName { get; set; }
	public int DisplayOrder { get; set; }
}

