using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.Entities;

/// <summary>
/// Represents one teacher upload session
/// Groups all questions extracted from one file
/// Tracks review progress
/// Controls CDN cleanup lifecycle
/// </summary>
public class ScanSession
{
	public Guid Id { get; set; }
	public Guid SchoolId { get; set; }
	public Guid TeacherId { get; set; }

	public string OriginalFileUrl { get; set; }

	public string OriginalFileName { get; set; }

	public string FileType { get; set; }
	public long FileSizeBytes { get; set; }

	public int TotalExtracted { get; set; }

	public int TotalConfirmed { get; set; }

	public int TotalRejected { get; set; }

	public int TotalPending { get; set; }


	public string AIModel { get; set; }

	public string ExtractionCompletedDate { get; set; }
	

	public ScanSessionStatus Status { get; set; }

	public string FailureReason { get; set; }

	public bool OriginalFileDeleted { get; set; }

	public string OriginalFileDeletedDate { get; set; }

	public bool IsActive { get; set; } = true;
	public bool IsDeleted { get; set; } = false;
	public string DeletedDate { get; set; }

	public string CreationDate { get; set; }
	public string ModifiedDate { get; set; }
	public string CompletedDate { get; set; }
}

