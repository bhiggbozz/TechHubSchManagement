using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.DTO;

public class StudentSubjectDto
{
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; }
	public string Category { get; set; }
	public string SubjectType { get; set; } 
}


public class TeacherSubjectIdRow
{
	public Guid SubjectId { get; set; }
}