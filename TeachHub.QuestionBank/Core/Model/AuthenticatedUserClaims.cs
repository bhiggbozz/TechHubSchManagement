using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.QuestionBank.Core.Model;

public class AuthenticatedUserClaims
{
	public string? SchoolId { get; set; }
	public string? UserId { get; set; }
	public string? Email { get; set; }
	public string? Role { get; set; }
	public string? TenantId { get; set; }

}


