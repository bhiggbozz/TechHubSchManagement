using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities
{
	public  class School
	{
		public DateTime CreationDate { get; set; } = DateTime.Now;
		public DateTime Modifieddate { get; set; } = DateTime.Now;
		public Guid Id { get; set; } =  Guid.NewGuid();
		public string SchoolName { get; set; }
		public string Location { get; set; }
		public string Country { get; set; }
		public string State { get; set; }
		public string Address { get; set; }
		public bool HasBranch { get; set; }

	}
}
