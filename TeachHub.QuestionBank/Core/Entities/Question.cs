using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.Entities;

public class Question
{
	public Guid Id { get; set; }
	public Guid SchoolId { get; set; }
	public Guid SubjectId { get; set; }
	public Guid? TopicId { get; set; }
	public Guid CreatedBy { get; set; }

	public string Title { get; set; }
	public string Topic { get; set; }
	public string SubTopic { get; set; }
	public QuestionType QuestionType { get; set; }
	public string TextContent { get; set; }
	public DifficultyLevel DifficultyLevel { get; set; }
	public int MarksAllocation { get; set; }

	public Guid? BoardSessionId { get; set; }
	public bool HasBoardSession { get; set; }
	public bool HasMedia { get; set; }
	public bool HasAudio { get; set; }

	public Guid? ScanSessionId { get; set; }

	public bool IsScanned { get; set; }

	public int? ExtractedQuestionIndex { get; set; }
	public string? AIConfidenceScore { get; set; }

	public QuestionStatus Status { get; set; }
	public bool IsActive { get; set; } = true;
	public bool IsDeleted { get; set; }

	public string ReviewedDate { get; set; }

	public Guid? ReviewedBy { get; set; }

	public string PublishedDate { get; set; }
	public Guid? PublishedBy { get; set; }

	public string ClientId { get; set; }
	public string OriginDevice { get; set; }
	public DateTime? LastSyncedAt { get; set; }

	public string DeletedDate { get; set; }
	public Guid? DeletedBy { get; set; }
	public string CreationDate { get; set; }
	public string ModifiedDate { get; set; }

	public string OriginalFileUrl { get; set; }
	// CDN url of the original uploaded file
	// Shown during review
	// Deleted after review complete

	public string OriginalFileName { get; set; }
	// Original file name for display

	public string SnapshotUrl { get; set; }
	// CDN url of board snapshot PNG

	public string? SnapshotPublicId { get; set; }
}
