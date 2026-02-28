using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Core.ViewModel.school;
public class UpdateClassroomTeachersViewModel
{
	[Required]
	public Guid ClassroomId { get; set; }

	[Required]
	[MinLength(1, ErrorMessage = "At least one teacher action is required")]
	public List<TeacherAssignmentAction> TeacherActions { get; set; } = new();
}

public class TeacherAssignmentAction
{
	[Required]
	public Guid TeacherId { get; set; }

	[Required]
	public TeacherActionType Action { get; set; }  // Add, Remove, Reactivate

	public bool IsPrimary { get; set; } = false;
}



