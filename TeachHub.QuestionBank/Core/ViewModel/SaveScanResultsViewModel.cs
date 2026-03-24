using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.ViewModel;

/// <summary>
/// Sent after teacher completes review
/// Contains only confirmed questions
/// Rejected questions never reach backend
/// </summary>
public class SaveScanResultsViewModel
{
	public string LocalSessionId { get; set; }
	// Idempotency key
	// If this LocalSessionId already processed
	// return existing result — do not duplicate

	public string OriginalFileName { get; set; }
	public string FileType { get; set; }
	public ScanType ScanType { get; set; }

	public List<CreateQuestionViewModel> Questions { get; set; } = new List<CreateQuestionViewModel>();
	// Only teacher-confirmed questions
	// Already mapped to CreateQuestionViewModel
	// by Web Worker before sending
	// Each has IsScanned = true
	// Each has AIConfidenceScore
	// Each has ExtractedQuestionIndex
}
