using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class QuizAttempt
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string QuizCode { get; set; } = string.Empty;
	public Guid LessonId { get; set; }
	public Guid StudentId { get; set; }
	public Guid SchoolId { get; set; }
	public int AttemptNumber { get; set; } = 1;
	public string StartedAt { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
	public string? SubmittedAt { get; set; }
	public int? TimeTakenSeconds { get; set; }
	public int TotalQuestions { get; set; }
	public int TotalAutoGraded { get; set; }
	public int TotalManualGraded { get; set; }
	public int TotalCorrect { get; set; }
	public int TotalWrong { get; set; }
	public int TotalSkipped { get; set; }
	public decimal AutoMarksObtained { get; set; }
	public decimal ManualMarksObtained { get; set; }
	public decimal TotalMarks { get; set; }
	public decimal? FinalScorePercent { get; set; }
	public bool? IsPassed { get; set; }
	public string? DeviceId { get; set; }  // frontend generated UUID
	public string? UserAgent { get; set; }  // browser/device string
	public string Status { get; set; } = QuizAttemptStatus.InProgress;
	public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
	public string ModifiedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}


