using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Core.Entities
{
	public class Subjects
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public DateTime CreationDate { get; set; } = DateTime.Now;
		public DateTime ModifiedDate { get; set; } = DateTime.Now;
		public string Subject {  get; set; }
		public SubjectCategory Category { get; set; }
		public ClassCategory ClassCategory { get; set; } 
		public Guid SchoolId { get; set; }
		public Guid CreatedBy { get; set; }
		public bool IsActive { get; set; }		

	}
}
