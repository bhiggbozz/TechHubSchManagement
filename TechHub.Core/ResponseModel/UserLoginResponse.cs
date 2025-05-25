using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.ResponseModel
{
	public class UserLoginResponse : BaseResponse
	{
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? EmailAddress { get; set; }
		public bool IsActive { get; set; }
		public Guid Id { get; set; }
		public int RoleId { get; set; }

		public SchoolResponseModel SchoolInfo { get; set; }
	}
	public class SchoolResponseModel
	{		
		public Guid Id { get; set; } = Guid.NewGuid();
		public string SchoolName { get; set; }
		public string Location { get; set; }
		public int CountryId { get; set; }
		public int StateId { get; set; }
		public string Address { get; set; }
	}
}
