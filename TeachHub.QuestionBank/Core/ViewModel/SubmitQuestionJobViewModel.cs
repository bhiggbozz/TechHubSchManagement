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
}

