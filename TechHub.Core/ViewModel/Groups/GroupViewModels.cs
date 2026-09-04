using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TechHub.Core.ViewModel.Groups
{
	public class CreateGroupViewModel
	{
		[Required]
		public string Name { get; set; }
	}

	public class AddGroupMembersViewModel
	{
		[Required]
		public List<Guid> StudentIds { get; set; }
	}
}
