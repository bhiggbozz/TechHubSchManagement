using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.ViewModel;
/// <summary>
/// Sent by frontend after teacher makes
/// a resolution decision for each conflict
/// </summary>
public class ResolveConflictViewModel
{
	public string DeviceId { get; set; }

	public List<ConflictResolution> Resolutions { get; set; } = new List<ConflictResolution>();
}
