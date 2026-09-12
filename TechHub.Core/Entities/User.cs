using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Entities;

namespace TechHub.Core.Model
{
	public class Users
	{
		public Guid Id { get; set; } = Guid.NewGuid();
		public string CreationDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public string ModifiedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		public string?  FirstName { get; set; }
		public string? LastName { get; set; }
		public string? EmailAddress { get; set; }
		public string? HashPassword { get; set; }
		public bool IsActive { get; set; }
		public bool HasAccess { get; set; }	
		public string? UserName {  get; set; }
		public string? SchoolCode { get; set; }
		public Guid SchoolId { get; set; }
		public int RoleId { get; set; }
		public Guid CreatedBy { get; set; }
		public string? ProfileImage { get; set; }	
		public string? GuardianName { get; set; }
		public string? DOB { get; set; } = DateTime.MinValue.ToString("yyyy-MM-dd HH:mm:ss");
		public Guid? LineManagerId { get; set; }
		public string? QrCodeToken { get; set; }
		public bool RequirePasswordChange { get; set; }
		public DateTime? PasswordResetExpiresAt { get; set; }

	}
}
