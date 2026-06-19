using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class QuizAttemptAnswer
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid AttemptId { get; set; }
	public Guid QuestionId { get; set; }
	public Guid SchoolId { get; set; }
	public int QuestionType { get; set; }

	// ── Auto graded (MCQ, TrueOrFalse) ───────────────────────────────────────
	public Guid? SelectedOptionId { get; set; }
	public bool? IsCorrect { get; set; }
	public decimal? AutoMarksObtained { get; set; }

	// ── Text answer (ShortAnswer, Essay, FillInTheBlank) ─────────────────────
	public string? TypedAnswer { get; set; }

	// ── Board + Audio answer ──────────────────────────────────────────────────
	public string? BoardSessionId { get; set; }  // MongoDB session id
	public string? AudioUrl { get; set; }  // Cloudinary URL

	// ── Manual grading ────────────────────────────────────────────────────────
	public decimal? ManualMarksObtained { get; set; }
	public string? TeacherFeedback { get; set; }
	public Guid? GradedBy { get; set; }
	public string? GradedAt { get; set; }

	// ── Common ────────────────────────────────────────────────────────────────
	public decimal MaxMarks { get; set; }
	public long? TimeTakenMs { get; set; }
	public bool IsSkipped { get; set; } = false;
	public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
	public string ModifiedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
}

