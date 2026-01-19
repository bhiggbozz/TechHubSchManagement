using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities
{
	public class ClassroomSubjects
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public string CreationDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public string ModifiedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public Guid ClassroomId { get; set; }
		public Guid SubjectId { get; set; }
		public Guid SchoolId { get; set; }
		public Guid Createdby { get; set; }
		public bool IsActive { get; set; }

	}
}
