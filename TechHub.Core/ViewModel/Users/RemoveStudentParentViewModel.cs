using System;
using System.ComponentModel.DataAnnotations;

namespace TechHub.Core.ViewModel.Users
{
	public class RemoveStudentParentViewModel
	{
		[Required]
		public Guid StudentId { get; set; }
		[Required]
		public Guid ParentId { get; set; }
	}
}
