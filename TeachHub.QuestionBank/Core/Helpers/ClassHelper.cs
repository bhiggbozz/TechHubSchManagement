using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.QuestionBank.Core.Helpers;

	// TechHub.Core/Helpers/ClaimsHelper.cs

public static class ClaimsHelper
{
	/// <summary>
	/// Safely parses role string from JWT claims
	/// Handles spaces in role names like
	/// "Super Administrator" → SuperAdministrator
	/// Defaults to Teacher if parsing fails
	/// </summary>
	public static UserRole ParseRole(string role)
	{
		return Enum.TryParse<UserRole>(role?.Replace(" ", ""),ignoreCase: true,out var parsedRole)
				? parsedRole
				: UserRole.HeadTeacher;
	}

	public static bool IsAdmin(string role)
	{
		var parsed = ParseRole(role);
		return parsed == UserRole.Administrator
			|| parsed == UserRole.SuperAdministrator;
	}

	public static bool IsSuperAdmin(string role)
	{
		var parsed = ParseRole(role);
		return parsed == UserRole.SuperAdministrator;
	}

	/// <summary>
	/// SQL fragment gating visibility of the admin-only question tier.
	/// Teachers can never see IsAdminOnly rows, no matter what they request —
	/// admins see everything by default and can optionally narrow to just
	/// their own admin-only stash. Callers append this fragment onto a WHERE
	/// clause that already filters by SchoolId/etc (alias defaults to "q").
	/// </summary>
	public static string BuildAdminOnlyFilter(string role, bool? adminOnlyRequested, string alias = "q")
	{
		if (!IsAdmin(role))
			return $" AND {alias}.IsAdminOnly = 0";

		return adminOnlyRequested == true
			? $" AND {alias}.IsAdminOnly = 1"
			: string.Empty;
	}
}
