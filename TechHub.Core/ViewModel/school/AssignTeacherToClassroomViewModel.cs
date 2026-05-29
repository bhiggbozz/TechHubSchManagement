using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.school;

public class AssignTeacherToClassroomViewModel
{
	public Guid TeacherId { get; set; }
	public Guid ClassroomId { get; set; } 
	public List<Guid> ClassroomIds { get; set; } = new();  
	public bool IsPrimary { get; set; } = false;
}