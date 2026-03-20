using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;
using TechHub.QuestionBank.Core.ViewModel;

namespace TechHub.QuestionBank.Core.Response;

/// <summary>
/// Response after conflict resolution processed
/// </summary>
public class ConflictResolutionResponse : BaseResponse
{
	internal List<ResolvedItem> Resolved;
	internal List<string> Failed;

	public string ClientId { get; set; }
	public Guid ServerId { get; set; }
	public ResolutionChoice Choice { get; set; }

	public SyncQuestionViewModel LocalData { get; set; }
	// KeepLocal sends teacher's local version here

	public SyncQuestionViewModel MergedData { get; set; }
	// ClientIds that failed to resolve
	// Frontend should retry these
}

/// <summary>
/// Confirmation of one resolved conflict
/// </summary>
public class ResolvedItem
{
	public string ClientId { get; set; }
	public Guid ServerId { get; set; }
	public ResolutionChoice AppliedChoice { get; set; }
	public string ResolvedAt { get; set; }

	public bool ShouldCleanLocalRecord { get; set; }
	// True if frontend should remove
	// full local data and keep only reference
	// Happens when KeepServer or DiscardLocal chosen
}

/// <summary>
/// What the teacher decided to do with the conflict
/// </summary>
