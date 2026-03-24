using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.Enums;

namespace TechHub.QuestionBank.Core.ViewModel;

/// <summary>
/// Teacher's resolution decision for one conflict
/// </summary>
public class ConflictResolution
{
	public string ClientId { get; set; }
	public Guid ServerId { get; set; }

	public ResolutionChoice Choice { get; set; }
	public SyncQuestionViewModel LocalData { get; set; }

	/// <summary>
	/// Only populated if Choice is KeepMerged
	/// Teacher manually merged both versions
	/// Frontend sends the merged result
	/// </summary>
	public SyncQuestionViewModel MergedData { get; set; }
}

