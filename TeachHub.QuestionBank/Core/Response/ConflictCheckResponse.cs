using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.DTO;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.Response;

/// <summary>
/// Backend response to conflict check request
/// Frontend uses this to decide what to sync
/// and what needs teacher resolution before sync
/// </summary>
public class ConflictCheckResponse : BaseResponse
{
	/// <summary>
	/// Questions that are safe to sync
	/// No conflict detected
	/// Frontend can include these in sync batch
	/// </summary>
	public List<string> SafeToSync { get; set; }
		= new List<string>();
	// List of ClientIds

	/// <summary>
	/// Questions where both local and server
	/// versions changed since last sync
	/// Teacher must resolve before sync proceeds
	/// </summary>
	public List<ConflictDetail> Conflicts { get; set; }
		= new List<ConflictDetail>();

	/// <summary>
	/// Questions that exist locally but server
	/// has no record of them
	/// Treated as new — safe to sync
	/// </summary>
	public List<string> NewToServer { get; set; }
		= new List<string>();
	// List of ClientIds

	public int TotalChecked { get; set; }
	public int TotalConflicts { get; set; }
	public int TotalSafeToSync { get; set; }
}

/// <summary>
/// Full detail of a detected conflict
/// Frontend shows teacher both versions
/// so they can make an informed choice
/// </summary>
public class ConflictDetail
{
	public string ClientId { get; set; }
	public Guid ServerId { get; set; }

	public ConflictReason Reason { get; set; }
	public string ReasonDescription { get; set; }
	// Human readable explanation
	// e.g "This question was edited on another
	//      device after your last sync"

	/// <summary>
	/// What the teacher has on their device
	/// </summary>
	public QuestionDto LocalVersion { get; set; }

	/// <summary>
	/// What the server currently has
	/// </summary>
	public QuestionDto ServerVersion { get; set; }

	public string LocalModifiedAt { get; set; }
	public string ServerModifiedAt { get; set; }
}


