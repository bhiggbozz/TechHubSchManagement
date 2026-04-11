using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Core.ViewModel
{
	public class UserViewModel
	{
		public Guid Createdby {  get; set; }
		public string? FirstName { get; set; } = null!;
		public string? LastName { get; set; } = null!;
		public string? EmailAddress { get; set; }
		public string? HashPassword { get; set; }
		public bool IsActive { get; set; }
		public bool HasAccess { get; set; }
		public string? UserName { get; set; }
		public Guid SchoolId { get; set; }
		public UserRole Role { get; set; }
		public List<Guid> UserClassroomsId { get; set; } = new List<Guid>();
		public List<Guid> UserSubjects { get; set; } = new List<Guid>();
		public List<Guid> RemoveSubjects { get; set; } = new List<Guid>();
		public List<Guid> RemoveClassroom { get; set; } = new List<Guid>();

	}

	public class UserViewModelV2
	{
		public Guid Createdby { get; set; }
		public string? FirstName { get; set; } = null!;
		public string? LastName { get; set; } = null!;
		public string? EmailAddress { get; set; }
		public string? HashPassword { get; set; }
		public bool IsActive { get; set; }
		public bool HasAccess { get; set; }
		public string? UserName { get; set; }
		public string? DOB { get; set; }

		public Guid SchoolId { get; set; }
		public UserRole Role { get; set; }
		public List<Guid> UserClassroomsId { get; set; } = new List<Guid>();
		public List<Guid> UserSubjects { get; set; } = new List<Guid>();
		public List<Guid> RemoveSubjects { get; set; } = new List<Guid>();
		public List<Guid> RemoveClassroom { get; set; } = new List<Guid>();

	}
}
