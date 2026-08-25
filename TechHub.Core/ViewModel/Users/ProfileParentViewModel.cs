using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TechHub.Core.ViewModel.Users
{
	public class ProfileParentViewModel
	{
		[Required]
		public string ParentFirstName { get; set; }
		[Required]
		public string ParentLastName { get; set; }
		[Required]
		[EmailAddress]
		public string ParentEmail { get; set; }
		[Required]
		public List<Guid> StudentIds { get; set; }
	}
}
