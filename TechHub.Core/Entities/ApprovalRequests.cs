using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

public class ApprovalRequests
{
	public Guid Id { get; set; }
	public Guid SchoolId { get; set; }
	public Guid RequestedBy { get; set; }
	public Guid ApproverId { get; set; }
	public string OperationType { get; set; }
	public string EntityType { get; set; }
	public Guid? EntityId { get; set; }
	public string Payload { get; set; }
	public string Status { get; set; }
	public string RejectionReason { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? RespondedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
}

