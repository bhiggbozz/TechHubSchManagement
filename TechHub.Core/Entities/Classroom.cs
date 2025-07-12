using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities
{
	public class Classroom
	{
		public string CreationDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public string ModifiedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public Guid Id { get; set; } = Guid.NewGuid();
		public string Name { get; set; }
		public string TeacherName { get; set; }
		public int NoOfStudents { get; set; }
		public Guid CreatedBy { get; set; }
		public Guid SchoolId { get; set; }
		
	}
}
