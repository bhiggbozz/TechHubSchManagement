using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class LoginViewModel
	{
		public string? Username {  get; set; }
		public string? HashPassword { get; set; }
		public string Inst { get; set; }
		public string? DeviceType { get; set; }
		public string? DeviceIp { get; set; }
	}
}
