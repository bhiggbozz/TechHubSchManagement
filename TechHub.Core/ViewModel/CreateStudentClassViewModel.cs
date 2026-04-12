using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class CreateStudentClassViewModel
	{
		[Required]
		[MinLength(1, ErrorMessage = "At least one classroom is required")]
		public List<StudentClassView> classrooms { get; set; } = new List<StudentClassView>();
	}
	public class StudentClassView
	{
		[Required(ErrorMessage = "Classroom name is required")]
		[StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
		public string Name { get; set; }
		
		public int NoOfStudents { get; set; }
		[Required(ErrorMessage = "At least one subject has to be registered for this class")]
		public List<Guid> SubjectIds { get; set; } = new List<Guid>();

	}
}
