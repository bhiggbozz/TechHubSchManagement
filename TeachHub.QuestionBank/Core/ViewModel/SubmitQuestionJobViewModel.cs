using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.ViewModel;

// =====================================================
// QUESTIONJOB VIEWMODELS
// =====================================================

/// <summary>
/// Teacher submits this to start the async pipeline
/// Image goes to Cloudinary temp
/// Job logged as Pending
/// JobId returned immediately
/// </summary>
public class SubmitQuestionJobViewModel
{
	// Where this question belongs
	public Guid SubTopicId { get; set; }

	public Guid ClassroomId { get; set; }  
	public Guid SubjectId { get; set; }

	// Objective | Theory | TrueFalse
	public string QuestionType { get; set; }

	// Teacher declares upfront
	// Tells Claude to expect and extract images
	public bool HasImages { get; set; }

	// Difficulty for the question
	// Easy | Medium | Hard
	public string DifficultyLevel { get; set; } = "Medium";

	// Marks for this question
	public int MarksAllocation { get; set; } = 1;

	// ── Pre-uploaded file reference (frontend direct-to-Cloudinary) ─────
	// When the frontend already uploaded the file (direct-to-CDN), it can
	// pass FileUrl + FilePublicId + FileType instead of the `image` file part.
	public string? FileUrl { get; set; }

	public string? FilePublicId { get; set; }

	// "image" | "pdf" (lowercase, as returned by Cloudinary upload)
	public string? FileType { get; set; }
}

