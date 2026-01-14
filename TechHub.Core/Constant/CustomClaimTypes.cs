using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Constant;

/// <summary>
/// Custom JWT claim types for multi-tenant authentication
/// </summary>
public static class CustomClaimTypes
{
	/// <summary>
	/// User ID claim
	/// </summary>
	public const string UserId = "userId";

	/// <summary>
	/// School/Tenant ID claim
	/// </summary>
	public const string SchoolId = "schoolId";

	/// <summary>
	/// School Name claim
	/// </summary>
	public const string SchoolName = "schoolName";

	/// <summary>
	/// Tenant domain/identifier claim (e.g., "pearl", "oxford")
	/// </summary>
	public const string Domain = "domain";

	/// <summary>
	/// User role claim
	/// </summary>
	public const string Role = "role";
}

