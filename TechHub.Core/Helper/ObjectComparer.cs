using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace TechHub.Core.Helper
{
	public class HelperUtil
	{
		public static List<string> GetDifferentFields<T>(T obj1, T obj2)
		{
			var differentFields = new List<string>();

			if (obj1 == null || obj2 == null)
			{
				//differentFields.Add("One or both objects are null.");
				return differentFields;
			}

			foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				var value1 = prop.GetValue(obj1);
				var value2 = prop.GetValue(obj2);

				if (!object.Equals(value1, value2))
				{
					differentFields.Add(prop.Name);
				}
			}

			return differentFields;
		}
		public static Dictionary<string, object?> GetDifferences<T>(T oldObj, T newObj)
		{
			var differences = new Dictionary<string, object?>();

			if (oldObj == null || newObj == null)
				throw new ArgumentNullException("Both objects must not be null");

			foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				var value1 = prop.GetValue(oldObj);
				var value2 = prop.GetValue(newObj);

				if (!object.Equals(value1, value2))
				{
					differences[prop.Name] = value2;
				}
			}

			return differences;
		}
		public static string GetNullPorpertiesName(object obj)
		{
			if(obj == null)
			{
				throw new ArgumentNullException(nameof(obj));
			}
			var nullProps =  obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(c => c.CanRead && c.GetValue(obj) == null)
				.Select(p => p.Name).ToList();

			return nullProps.Any() ? string.Join(",", nullProps) : string.Empty;

		}
	}
}
