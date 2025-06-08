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
		public string Name { get; set; }
		[Required]
		public string TeacherName {  get; set; }
		public int NoOfStudents { get; set; }
		[Required]
		public Guid CreatedBy { get; set; }
		[Required]
		public Guid SchoolId { get; set; }
	}
}
