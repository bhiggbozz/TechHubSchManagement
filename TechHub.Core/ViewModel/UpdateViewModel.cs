using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ViewModel;

	public class UpdateUserView
	{

		[Required]
		public Guid Id { get; set; }

		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? EmailAddress { get; set; }
		public string? HashPassword { get; set; }

		/// <summary>
		/// Must be explicitly true for HashPassword to be applied. A non-empty
		/// HashPassword alone is not enough — this exists because a caller once
		/// silently populated HashPassword with an unintended value (a hash of the
		/// username) on every profile-edit save, resetting accounts' passwords
		/// without anyone intending it. Requiring a separate, explicit intent flag
		/// means a future bug of that same shape can no longer take effect.
		/// </summary>
		public bool ChangePassword { get; set; } = false;

		public bool? IsActive { get; set; }
		public bool? HasAccess { get; set; }

		public int? RoleId { get; set; }
		public string? ProfileImage { get; set; }
		public string? GuardianName { get; set; }

	}

