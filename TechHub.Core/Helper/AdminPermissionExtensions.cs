using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Core.Helper;


	public static class AdminPermissionExtensions
	{
		/// <summary>True when v is a power of two (single-bit flag), excluding 0.</summary>
		private static bool IsSingleBitValue(int v) => v > 0 && (v & (v - 1)) == 0;

		private static readonly HashSet<int> ValidPermissionValues = new HashSet<int>(
			System.Enum.GetValues(typeof(AdminPermission))
				.Cast<int>()
				.Where(IsSingleBitValue)
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
			return IsSingleBitValue(value);
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
				int v = (int)value;
				if (IsSingleBitValue(v) && (permissions & value) == value)
				{
					result.Add(v);
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
			AdminPermission combined = AdminPermission.None;
			foreach (var value in permissionValues)
			{
				combined |= (AdminPermission)value;
			}
			return combined.ToPermissionNames();
		}

		public static List<string> ToPermissionNames(this AdminPermission permissions)
		{
			var names = new List<string>();
			foreach (AdminPermission value in System.Enum.GetValues(typeof(AdminPermission)))
			{
				int v = (int)value;
				if (IsSingleBitValue(v) && (permissions & value) == value)
				{
					names.Add(value.ToString());
				}
			}
			return names;
		}
	}


