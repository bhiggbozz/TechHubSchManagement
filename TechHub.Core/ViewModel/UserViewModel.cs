using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class UserViewModel
	{
		public Guid Createdby {  get; set; }
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? EmailAddress { get; set; }
		public string? HashPassword { get; set; }
		public bool IsActive { get; set; }
		public bool HasAccess { get; set; }
		public string? UserName { get; set; }
		public Guid SchoolId { get; set; }
		public int RoleId { get; set; }
		public string SchoolCode { get; set; }

	}
}
