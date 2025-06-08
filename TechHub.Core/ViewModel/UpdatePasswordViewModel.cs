using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class UpdatePasswordViewModel
	{
		public string HashPassword {  get; set; }
		public string CurrentHashPassword { get; set; }
		public string SchoolId { get; set; }
		public string? username { get; set; }
		public string? DeviceIp { get; set; }
		public string? DeviceType { get; set; }
	}
}
