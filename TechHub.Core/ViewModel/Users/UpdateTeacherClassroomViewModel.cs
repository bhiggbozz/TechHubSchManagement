using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TechHub.Core.ViewModel.Users;

/// <summary>
/// View model for teachers to update their own classroom assignments
/// Used by ClassTeachers and HeadTeachers only
/// </summary>
public class UpdateTeacherClassroomViewModel
{
	/// <summary>
	/// For HeadTeachers: List of classroom IDs to assign
	/// For ClassTeachers: Single classroom ID (only first item will be used)
	/// </summary>
	[Required]
	[MinLength(1, ErrorMessage = "At least one classroom is required")]
	public List<Guid> ClassroomIds { get; set; } = new();

	/// <summary>
	/// Optional: Set if this is the primary classroom assignment
	/// Mainly relevant for HeadTeachers who can have multiple classrooms
	/// </summary>
	public bool IsPrimary { get; set; } = false;
}
