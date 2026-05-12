using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Model;

public class SubjectTeacherAssignmentRow
{
	public Guid ClassroomId { get; set; }
	public string ClassName { get; set; }
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; }
	public string SubjectCategory { get; set; }
}

public class ClassroomRow
{
	public Guid ClassroomId { get; set; }
	public string ClassName { get; set; }
	public bool ClassroomIsActive { get; set; }
}

public class SubjectRow
{
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; }
	public string SubjectCategory { get; set; }
}
