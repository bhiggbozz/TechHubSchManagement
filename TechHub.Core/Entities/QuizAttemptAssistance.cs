using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class QuizAttemptAssistance
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid AttemptId { get; set; }
	public Guid QuestionId { get; set; }
	public Guid StudentId { get; set; }
	public Guid SchoolId { get; set; }
	public string? StudentPrompt { get; set; }
	public string? AIResponse { get; set; }
	public int TokensUsed { get; set; } = 0;
	public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}
