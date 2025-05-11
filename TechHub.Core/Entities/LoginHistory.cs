using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities
{
	public class LoginHistory
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public string CreationDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd :HH:mm:ss");
		public string ModifiedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd :HH:mm:ss");
		public Guid? UserId { get; set; }
		public int RoleId { get; set; }
		public bool PasswordFailed { get; set; }
		public string? DeviceType { get; set; }
		public string? DeviceIp { get; set; }


	}
}
