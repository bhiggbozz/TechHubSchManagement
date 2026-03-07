using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Core.Helper;


	public static class AdminPermissionExtensions
	{
		private static readonly HashSet<int> ValidPermissionValues = new HashSet<int>(
            System.Enum.GetValues(typeof(AdminPermission)).Cast<int>()  // ✅ Cast<int>() is the key!
        );


		public static AdminPermission ToAdminPermission(this List<int> permissionValues)
		{
			AdminPermission result = AdminPermission.None;

			foreach (var value in permissionValues)
			{
				if (ValidPermissionValues.Contains(value))
				{
					result |= (AdminPermission)value;
				}
			}

			return result;
		}

		public static bool IsValidPermission(int value)
		{
			return ValidPermissionValues.Contains(value);
		}

		public static List<int> GetInvalidPermissions(this List<int> permissionValues)
		{
			return permissionValues.Where(p => !ValidPermissionValues.Contains(p)).ToList();
		}

		public static List<int> ToPermissionList(this AdminPermission permissions)
		{
			var result = new List<int>();

			foreach (AdminPermission value in System.Enum.GetValues(typeof(AdminPermission)))
			{
				if (value != AdminPermission.None && (permissions & value) == value)
				{
					result.Add((int)value);
				}
			}

			return result;
		}

		public static bool HasPermission(this AdminPermission permissions, AdminPermission permission)
		{
			return (permissions & permission) == permission;
		}

		public static AdminPermission AddPermission(this AdminPermission permissions, AdminPermission permission)
		{
			return permissions | permission;
		}

		public static AdminPermission RemovePermission(this AdminPermission permissions, AdminPermission permission)
		{
			return permissions & ~permission;
		}

		public static List<string> ToPermissionNames(this List<int> permissionValues)
		{
			var names = new List<string>();

			foreach (var value in permissionValues)
			{
				if (ValidPermissionValues.Contains(value))
				{
					names.Add(((AdminPermission)value).ToString());
				}
			}

			return names;
		}
	}


