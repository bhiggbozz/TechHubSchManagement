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

		public bool? IsActive { get; set; }
		public bool? HasAccess { get; set; }

		public int? RoleId { get; set; }
		public string? ProfileImage { get; set; }
		public string? GuardianName { get; set; }

	}

