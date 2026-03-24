using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.ViewModel;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// CONFLICT CHECK
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// Sent by frontend before sync attempt
/// Frontend tells backend what it has locally
/// Backend compares against server versions
/// and reports back any conflicts
/// </summary>
public class ConflictCheckViewModel
{
	public string DeviceId { get; set; }

	public List<LocalQuestionFingerprint> LocalQuestions { get; set; }
		= new List<LocalQuestionFingerprint>();
}

/// <summary>
/// Lightweight fingerprint of a local question
/// Frontend sends this instead of full question data
/// to keep the conflict check request small
/// </summary>
public class LocalQuestionFingerprint
{
	public string ClientId { get; set; }
	// Device generated ID

	public Guid? ServerId { get; set; }
	// Null if never synced before
	// Present if previously synced and then edited

	public string LastModifiedAtDevice { get; set; }
	// When teacher last edited it on device
	// Backend compares this against server ModifiedDate
	// to detect if server version changed since last sync

	public SyncStatus LocalSyncStatus { get; set; }
	// Helps backend understand what state
	// the frontend thinks this record is in
}

