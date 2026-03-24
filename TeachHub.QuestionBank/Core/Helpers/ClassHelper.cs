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
}
