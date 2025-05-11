using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities
{
	public class Role
	{
		public string RoleId { get; set; }
		public string RoleName { get; set; }
	}
	public class RoleDto
	{
		public Guid Id { get; set; }
		public string CreationDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public string ModifiedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public string? RoleName { get; set; }

	}

}
