using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Enums;

/// <summary>
/// Teacher declares scan type before uploading
/// Determines which prompt additions are used
/// Controls how Claude structures its response
/// </summary>
public enum ScanType
{
	QuestionsOnly = 1,
	// Page contains only questions
	// No answers expected
	// Most common type

	QuestionsWithAnswers = 2,
	// Correct answers marked on paper
	// MCQ with answers circled etc
	// AI extracts both question and answer

	EssayQuestions = 3,
	// Long form questions
	// Mark allocations important
	// Model answers may or may not be present

	MarkingScheme = 4,
	// Answer booklet or marking guide
	// Both questions and model answers
	// Mark breakdowns per point

	MixedPaper = 5
	// Multiple question types on one page
	// AI determines type per question
	// Most flexible but least precise
}

