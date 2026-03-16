using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ResponseModel;
public class BulkOperationResponse : BaseResponse
{
	public int TotalProcessed { get; set; }
	public int SuccessCount { get; set; }
	public int FailureCount { get; set; }

	public List<BulkOperationResult> Results { get; set; } = new();
}

public class BulkOperationResult
{
	public Guid ClassPreparationId { get; set; }
	public bool Success { get; set; }
	public string Message { get; set; } = string.Empty;
}

