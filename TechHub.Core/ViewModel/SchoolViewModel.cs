using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class SchoolViewModel
	{
		public string SchoolName { get; set; }
		public string Location { get; set; }
		public int CountryId { get; set; }
		public int StateId { get; set; }
		public string? State { get; set; }
		public string Address { get; set; }
		public bool HasBranch { get; set; }
		public bool IsActive { get; set; }
	}
}
