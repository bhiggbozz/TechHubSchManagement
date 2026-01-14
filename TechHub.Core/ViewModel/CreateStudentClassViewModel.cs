using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class CreateStudentClassViewModel : CreateBaseViewModel
	{
		
		public List<StudentClassView> classrooms { get; set; } = new List<StudentClassView>();
	}
	public class StudentClassView
	{
		[Required]
		public string Name { get; set; }
		[Required]
		public string TeacherName { get; set; }
		public int NoOfStudents { get; set; }
	}
}
