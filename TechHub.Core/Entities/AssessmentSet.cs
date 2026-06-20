using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class AssessmentSet
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string Name { get; set; } = string.Empty;
	public string Label { get; set; } = "Quiz";
	public Guid TeacherId { get; set; }
	public Guid SchoolId { get; set; }
	public bool AllowRetakes { get; set; } = false;
	public int MaxAttempts { get; set; } = 1;
	public int PassMarkPercent { get; set; } = 50;
	public int? TimeLimitMinutes { get; set; } = null;
	public bool AutoSubmitOnTimeout { get; set; } = true;
	public bool ShuffleQuestions { get; set; } = false;
	public string ShowResultMode { get; set; } = "Immediate";
	public bool ShowCorrectAnswers { get; set; } = false;
	public bool AllowBoardAnswer { get; set; } = true;
	public int EasyMarks { get; set; } = 1;
	public int MediumMarks { get; set; } = 2;
	public int HardMarks { get; set; } = 3;
	public int ExamLevelMarks { get; set; } = 5;
	public bool IsActive { get; set; } = true;
	public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
	public string ModifiedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}
