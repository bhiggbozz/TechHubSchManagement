using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ResponseModel;

	public class ClassroomDetailDto
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public int NoOfStudents { get; set; }
		public bool IsActive { get; set; }
		public string CreationDate { get; set; } = string.Empty;
		public string ModifiedDate { get; set; } = string.Empty;
		public List<ClassroomTeacherDto> Teachers { get; set; } = new();
	}

	public class ClassroomTeacherDto
	{
		public Guid TeacherId { get; set; }
		public string FirstName { get; set; } = string.Empty;
		public string LastName { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;

	}
