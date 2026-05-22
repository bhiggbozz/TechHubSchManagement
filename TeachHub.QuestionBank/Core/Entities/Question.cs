using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.Entities;

public class Questions
{
	public Guid Id { get; set; }
	public Guid SchoolId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid? TopicId { get; set; }
	public Guid ClassroomId { get; set; }
	public Guid CreatedBy { get; set; }

	// ─────────────────────────────────────────────────────
	// CLASSIFICATION
	// ─────────────────────────────────────────────────────
	public Guid? SubTopicId { get; set; }
	public string? Topic { get; set; }
	public string? SubTopic { get; set; }

	// ─────────────────────────────────────────────────────
	// CONTENT — PLAIN TEXT (existing scan pipeline)
	// ─────────────────────────────────────────────────────
	public string? Title { get; set; }
	public string? TextContent { get; set; }
	public QuestionType QuestionType { get; set; }
	public DifficultyLevel DifficultyLevel { get; set; }
	public int MarksAllocation { get; set; }

	// ─────────────────────────────────────────────────────
	// CONTENT — AI GENERATED (new upload pipeline)
	// ─────────────────────────────────────────────────────
	public string? QuestionHtml { get; set; }
	public string? ContentParts { get; set; }
	public bool HasLatex { get; set; }
	public string? CorrectAnswer { get; set; }

	public string? ImageUrl { get; set; }
	public string? ImagePublicId { get; set; }

	// ─────────────────────────────────────────────────────
	// JOB TRACKING
	// ─────────────────────────────────────────────────────
	public Guid? JobId { get; set; }

	// ─────────────────────────────────────────────────────
	// BOARD SESSION
	// ─────────────────────────────────────────────────────
	public Guid? BoardSessionId { get; set; }
	public bool HasBoardSession { get; set; }
	public bool HasMedia { get; set; }
	public bool HasAudio { get; set; }
	public string? SnapshotUrl { get; set; }
	public string? SnapshotPublicId { get; set; }

	// ─────────────────────────────────────────────────────
	// SCAN PIPELINE (existing — unchanged)
	// ─────────────────────────────────────────────────────
	public Guid? ScanSessionId { get; set; }
	public bool IsScanned { get; set; }
	public int? ExtractedQuestionIndex { get; set; }
	public string? AIConfidenceScore { get; set; }

	// ─────────────────────────────────────────────────────
	// STATUS
	// ─────────────────────────────────────────────────────
	public QuestionStatus Status { get; set; }
	public bool IsActive { get; set; } = true;
	public bool IsDeleted { get; set; }

	// ─────────────────────────────────────────────────────
	// AUDIT
	// ─────────────────────────────────────────────────────
	public string? ReviewedDate { get; set; }
	public Guid? ReviewedBy { get; set; }
	public string? PublishedDate { get; set; }
	public Guid? PublishedBy { get; set; }
	public string? ClientId { get; set; }
	public string? OriginDevice { get; set; }
	public DateTime? LastSyncedAt { get; set; }
	public string? DeletedDate { get; set; }
	public Guid? DeletedBy { get; set; }
	public string? CreationDate { get; set; }
	public string? ModifiedDate { get; set; }
}