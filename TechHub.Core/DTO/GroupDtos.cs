using System;
using System.Collections.Generic;

namespace TechHub.Core.DTO
{
	public class MyGroupDto
	{
		public Guid GroupId { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Status { get; set; } = string.Empty;
		public bool IsCreator { get; set; }
		public int MemberCount { get; set; }
	}

	public class GroupMemberDto
	{
		public Guid StudentId { get; set; }
		public string FirstName { get; set; } = string.Empty;
		public string LastName { get; set; } = string.Empty;
		public bool IsCreator { get; set; }
	}

	public class GroupContentSummaryDto
	{
		public Guid ContentId { get; set; }
		public string Aim { get; set; } = string.Empty;
		public string SubjectName { get; set; } = string.Empty;
		public string Status { get; set; } = string.Empty;
		public Guid CreatedBy { get; set; }
		public DateTime CreatedAt { get; set; }
		public int MediaCount { get; set; }
		public bool HasRecording { get; set; }
		public bool HasTextContent { get; set; }
	}

	public class GroupContentStatusDto
	{
		// NoActiveContent | RecordingInProgress | Recorded | Approved | Rejected
		// (PendingApproval content still recording falls through to the Mongo-derived
		// states above — only a decided submission short-circuits with its own status)
		public string Status { get; set; } = string.Empty;
		public Guid? ContentId { get; set; }
		public bool HasBoardRecording { get; set; }
		public bool HasManifest { get; set; }
		public int? LastBatchIndex { get; set; }
		public string? RejectionReason { get; set; }
	}

	public class GroupContentMediaDto
	{
		public Guid Id { get; set; }
		public string FileName { get; set; } = string.Empty;
		public string OriginalFileName { get; set; } = string.Empty;
		public string MediaType { get; set; } = string.Empty;
		public string CloudinaryUrl { get; set; } = string.Empty;
		public long FileSizeBytes { get; set; }
		public int? Duration { get; set; }
		public int DisplayOrder { get; set; }
	}

	public class GroupContentDetailDto
	{
		public Guid ContentId { get; set; }
		public Guid GroupId { get; set; }
		public Guid SubjectId { get; set; }
		public string SubjectName { get; set; } = string.Empty;
		public Guid? TopicId { get; set; }
		public string? SubTopic { get; set; }
		public string Aim { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string? TextContent { get; set; }
		public string Status { get; set; } = string.Empty;
		public Guid CreatedBy { get; set; }
		public string CreatedByName { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; }
		public DateTime? ApprovedAt { get; set; }
		public string? RejectionReason { get; set; }
		public bool HasRecording { get; set; }
		public List<GroupContentMediaDto> Media { get; set; } = new();
	}

	public class GroupDetailDto
	{
		public Guid GroupId { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Status { get; set; } = string.Empty;
		public Guid ClassroomId { get; set; }
		public Guid CreatedBy { get; set; }
		public List<GroupMemberDto> Members { get; set; } = new();
		public List<GroupContentSummaryDto> Content { get; set; } = new();
	}
}
