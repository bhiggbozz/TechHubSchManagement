using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Entities;

	/// <summary>
	/// Stores admin permissions for a user
	/// One row per admin user
	/// </summary>
	public class AdminPermissions
	{
	    public Guid Id { get; set; } = Guid.NewGuid();
		public Guid UserId { get; set; }
		public Guid SchoolId { get; set; }

		public int Permissions { get; set; }

		public string CreationDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:ss:mm");
		public string ModifiedDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:ss:mm");
		public Guid CreatedBy { get; set; }
		public bool IsActive { get; set; }
	}

