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

	public class GroupDetailDto
	{
		public Guid GroupId { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Status { get; set; } = string.Empty;
		public Guid ClassroomId { get; set; }
		public Guid CreatedBy { get; set; }
		public List<GroupMemberDto> Members { get; set; } = new();
	}
}
