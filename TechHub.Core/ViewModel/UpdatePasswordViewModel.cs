using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel
{
	public class UpdatePasswordViewModel
	{
		[Required]
		public string HashPassword {  get; set; }
		[Required]
		public string CurrentHashPassword { get; set; }
		[Required]
		public string? username { get; set; }
		public string? DeviceIp { get; set; }
		public string? DeviceType { get; set; }
	}

	public class UpdatePasswordViewModelV2
	{
		[Required]
		public string HashPassword { get; set; }
		[Required]
		public string CurrentHashPassword { get; set; }
		[Required]
		public Guid SchoolId { get; set; }
		public string? username { get; set; }
		public string? DeviceIp { get; set; }
		public string? DeviceType { get; set; }
	}
}
