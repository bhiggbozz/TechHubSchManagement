using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Entities;

// =====================================================
// QUESTIONJOB
// Tracks the async AI upload pipeline
// One row per teacher upload request
//
// LIFECYCLE:
// Pending    → job logged, image saved to temp
// Processing → background worker picked it up
// Completed  → Claude responded, Question record created
// Failed     → Claude failed, teacher retries
// =====================================================
public class QuestionJob
{
	public Guid Id { get; set; }
	public Guid SchoolId { get; set; }
	public Guid SubTopicId { get; set; }
	public Guid TeacherId { get; set; }

	// Null until background worker completes successfully
	// When populated — teacher can fetch their question
	public Guid? QuestionId { get; set; }

	// Objective | Theory | TrueFalse
	public string QuestionType { get; set; }

	// Teacher declared upfront
	// Tells background worker to expect image placeholders
	public bool HasImages { get; set; }

	// Cloudinary temp folder path
	// Background worker reads image from here
	// Deleted after processing completes
	public string? TempImagePath { get; set; }

	// Pending | Processing | Completed | Failed
	public string Status { get; set; } = "Pending";

	// Populated when Status = Failed
	// Returned to teacher so they know why it failed
	public string? FailureReason { get; set; }

	// Background worker increments on each attempt
	// After 3 attempts → Status permanently = Failed
	public int AttemptCount { get; set; } = 0;

	public string CreatedAt { get; set; }
	public string? CompletedAt { get; set; }
}