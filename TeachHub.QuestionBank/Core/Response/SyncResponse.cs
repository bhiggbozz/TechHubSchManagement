using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.QuestionBank.Core.DTO;

namespace TechHub.QuestionBank.Core.Response;

public class SyncResponse : BaseResponse
{
	// Maps ClientId → ServerId
	// Frontend uses this to know which
	// local records to clean up
	public List<SyncedItemMap> SyncedItems { get; set; }
	public List<string> FailedClientIds { get; set; }
	public List<ConflictItem> Conflicts { get; set; }
}

public class SyncedItemMap
{
	public string ClientId { get; set; }
	public Guid ServerId { get; set; }
	public string SyncedAt { get; set; }
	public bool IsDuplicate { get; set; }

}

public class ConflictItem
{
	public string ClientId { get; set; }
	public Guid ServerId { get; set; }
	public string ConflictReason { get; set; }
	public QuestionDto ServerVersion { get; set; }
	// Frontend shows teacher both versions
	// to resolve
}

