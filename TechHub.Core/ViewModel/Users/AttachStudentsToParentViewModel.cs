using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TechHub.Core.ViewModel.Users
{
	public class AttachStudentsToParentViewModel
	{
		[Required]
		public Guid ParentId { get; set; }
		[Required]
		public List<Guid> StudentIds { get; set; }
	}
}
