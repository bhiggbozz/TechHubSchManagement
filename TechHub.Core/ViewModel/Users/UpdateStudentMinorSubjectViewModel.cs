using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel.Users;

public class UpdateStudentMinorSubjectViewModel
{
	public List<Guid> AddSubjects { get; set; } = new();
	public List<Guid> RemoveSubjects { get; set; } = new();
}