using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Core.ViewModel.Users;

	public class AssignAdminPermissionsViewModel
	{
		[Required]
		public Guid AdminUserId { get; set; }

		[Required]
		[MinLength(1)]
		public List<int> Permissions { get; set; } = new();  // [1, 2, 4]
	}

	// Response Models
	public class AdminPermissionsDto
	{
		public Guid Id { get; set; }
		public Guid UserId { get; set; }
		public string UserName { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string RoleName { get; set; } = string.Empty;

		public int PermissionsValue { get; set; }
		public List<int> Permissions { get; set; } = new();  

		public string CreationDate { get; set; } = string.Empty;
		public string ModifiedDate { get; set; } = string.Empty;
		public string CreatedByName { get; set; } = string.Empty;
	}

