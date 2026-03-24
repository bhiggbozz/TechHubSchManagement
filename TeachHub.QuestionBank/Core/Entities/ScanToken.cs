using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.Entities;

/// <summary>
/// Short-lived single-use token
/// Issued before each scan operation
/// Controls access to Claude proxy endpoint
/// Prevents unauthorized AI calls
/// </summary>
public class ScanToken
{
	public Guid Id { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// OWNERSHIP
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public Guid TeacherId { get; set; }
	public Guid SchoolId { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// TOKEN DETAILS
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string Purpose { get; set; } = "question_scan";
	public int ScanType { get; set; }
	// Which scan type teacher selected
	// Determines prompt used in proxy

	public string FileType { get; set; }
	// "image" or "pdf"

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// LIFECYCLE
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public ScanTokenStatus Status { get; set; } = ScanTokenStatus.Active;

	public string IssuedAt { get; set; }
	public string ExpiresAt { get; set; }
	// 10 minutes from IssuedAt

	public string UsedAt { get; set; }
	// Populated when token consumed
	// Even if Claude call fails after

	public string RevokedAt { get; set; }
	public string RevokeReason { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// RECOVERY
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string LocalSessionId { get; set; }
	// Device-generated session ID
	// Links token to frontend IndexedDB entry
	// Used for recovery if tab closes

	public string DeviceId { get; set; }

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// USAGE TRACKING
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public int? TokensConsumed { get; set; }
	// Claude tokens used in this call
	// Populated after successful scan
	// Used for quota and billing tracking

	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
	// AUDIT
	// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

	public string CreationDate { get; set; }
}
