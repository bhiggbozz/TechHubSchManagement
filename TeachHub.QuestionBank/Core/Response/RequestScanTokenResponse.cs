using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Response;


/// <summary>
/// Response to token request
/// Frontend stores token for proxy call
/// </summary>
public class RequestScanTokenResponse : BaseResponse
{
	public Guid TokenId { get; set; }
	public string ExpiresAt { get; set; }
	public int RemainingDailyQuota { get; set; }
	// How many more scans teacher
	// can do today
	public int DailyLimit { get; set; }
	// Teacher's total daily limit
}
/// <summary>
/// Response after saving scan results
/// Frontend uses to clean IndexedDB entry
/// </summary>
public class SaveScanResultsResponse : BaseResponse
{
	public Guid ScanSessionId { get; set; }
	// Created by backend for this batch

	public string LocalSessionId { get; set; }
	// Echoed back for frontend reconciliation

	public List<SavedQuestionMap> SavedQuestions { get; set; } = new List<SavedQuestionMap>();
	// Maps each question's client position
	// to its new server ID
	// Frontend cleans IndexedDB after this

	public int TotalSaved { get; set; }
	public int TotalFailed { get; set; }
}

/// <summary>
/// Maps extracted question index
/// to server assigned question ID
/// </summary>
public class SavedQuestionMap
{
	public int ExtractedQuestionIndex { get; set; }
	public Guid QuestionId { get; set; }
	public string ClientId { get; set; }
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// QUOTA CHECK
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// Frontend can check quota before
/// showing scan option to teacher
/// Avoids teacher attempting scan
/// only to be told quota exceeded
/// </summary>
public class ScanQuotaResponse : BaseResponse
{
	public int DailyLimit { get; set; }
	public int UsedToday { get; set; }
	public int RemainingToday { get; set; }
	public bool CanScan { get; set; }
	public string ResetsAt { get; set; }
	// When daily quota resets
	// Midnight UTC
}
