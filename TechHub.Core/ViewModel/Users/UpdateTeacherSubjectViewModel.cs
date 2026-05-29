using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.Users;

public class UpdateTeacherSubjectViewModel
{
	public List<Guid> SubjectIds { get; set; } = new();  
	public Guid ClassroomId { get; set; }
}

