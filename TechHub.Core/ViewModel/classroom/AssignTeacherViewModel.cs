using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.classroom
{
	public class AssignTeacherViewModel
	{
		[Required]
		public Guid ClassroomId { get; set; }

		[Required]
		[MinLength(1, ErrorMessage = "At least one teacher is required")]
		public List<Guid> TeacherIds { get; set; } = new();
	}
}
