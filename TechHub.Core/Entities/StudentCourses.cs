using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Core.Entities
{
	public class StudentCourses
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public string CreationDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public string ModifiedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public Guid ClassroomId { get; set; }
		public Guid StudentId { get; set; }
		public Guid SchoolId { get; set; }
		public Guid CreatedBy { get; set; }
		public StudentClassroomStatus Status { get; set; } = StudentClassroomStatus.Active;

	}
}
