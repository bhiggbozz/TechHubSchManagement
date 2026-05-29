using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Model;

public class TeacherSubjectRow
{
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; }
	public Guid ClassroomId { get; set; }
	public string ClassName { get; set; }
}

