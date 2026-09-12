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
		public bool FirstTimeLogin { get; set; }

		/// <summary>
		/// True when a staff member reset this account's password (student forgot
		/// theirs) — the caller must complete the same flow as first-time login
		/// (update-password/newUser) before a real session is issued. Token/
		/// RefreshToken are left unset on this response, same as FirstTimeLogin.
		/// </summary>
		public bool PasswordResetRequired { get; set; }
		public string Token { get; set; }
		public int TokenExpiresIn { get; set; }
		public string RefreshToken { get; set; }
		public DateTime RefreshTokenExpiry { get; set; }

		public SchoolResponseModel? SchoolInfo { get; set; }

		/// <summary>Populated only when RoleId is Parent — null/omitted for every other role.</summary>
		public List<ChildInfo>? Children { get; set; }
	}

	public class ChildInfo
	{
		public Guid StudentId { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public Guid? ClassroomId { get; set; }
		public string? ClassroomName { get; set; }
	}

	public class SchoolResponseModel
	{		
		public Guid Id { get; set; }
		public string SchoolName { get; set; }
		public string Location { get; set; }
		public int CountryId { get; set; }
		public int StateId { get; set; }
		public string? State { get; set; }
		public string? Address { get; set; }
		public string? LogoUrl { get; set; }
	}
}
