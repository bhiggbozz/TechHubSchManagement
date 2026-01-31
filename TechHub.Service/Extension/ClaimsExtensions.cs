using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Model;

namespace TechHub.Service.Extension;

public static class ClaimsExtensions
{
	public static string GetSchoolIdClaim(this ClaimsPrincipal user)
	{
		return user.FindFirst("SchoolId")?.Value;
	}

	public static string GetUserIdClaim(this ClaimsPrincipal user)
	{
		return user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
	}

	public static string GetEmailClaim(this ClaimsPrincipal user)
	{
		return user.FindFirst(ClaimTypes.Email)?.Value;
	}

	public static string GetRoleClaim(this ClaimsPrincipal user)
	{
		return user.FindFirst(ClaimTypes.Role)?.Value;
	}

	public static AuthenticatedUserClaims? GetAuthenticatedUserClaims(this ClaimsPrincipal user)
	{
		return new AuthenticatedUserClaims
		{
			SchoolId = user.GetSchoolIdClaim(),
			UserId = user.GetUserIdClaim(),
			Email = user.GetEmailClaim(),
			Role = user.GetRoleClaim(),
			//TenantId = user.GetTenantIdClaim()
		};
	}
}

