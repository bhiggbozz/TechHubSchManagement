using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Helper
{
	public class QueryBuilder<TEntity> where TEntity : class
	{
		//public string InsertQuery(TEntity entity)
		//{
		//	List<KeyValuePair<string, object>> ObjKeyValue = GetPropertyValues(entity);

		//}
		static List<KeyValuePair<string, object>> GetPropertyValues<T>(T obj)
		{
			return typeof(T)
				.GetProperties()
				.Select(p => new KeyValuePair<string, object>(p.Name, p.GetValue(obj) ?? "null"))
				.ToList();
		}
		public static string GenerateInsertQuery<TEntity>(string tableName, TEntity obj)
		{
			var properties = typeof(TEntity).GetProperties()
									  .Select(p => p.Name)
									  .ToList();

			string columnNames = string.Join(", ", properties);
			string parameterNames = string.Join(", ", properties.Select(p => $"@{p}"));

			return $"INSERT INTO {tableName} ({columnNames}) VALUES ({parameterNames});";
		}
		public static string GenerateUpdateQuery<TEntity>(string tableName, TEntity obj, string keyColumn)
		{
			var properties = typeof(TEntity).GetProperties().Select(p => p.Name)
				                                      .Where(p => !p.Equals(keyColumn, StringComparison.OrdinalIgnoreCase)).ToList();
			string columns = string.Join(", ", properties.Select(p => "{p} = @{p}"));
			return $"Update {tableName} Set {columns} where {keyColumn} = @{keyColumn}";
		}

		public static string GenerateGetbyIdQuery()
		{
			var tableName = typeof(TEntity).Name;
			return $"select * from {tableName} where Id = @Id";
		}
		public static string GenerateGetQueryByProperties( string propertyName)
		{
			var tableName = typeof(TEntity).Name;
			return $"select * from {tableName} where {propertyName} = '@{propertyName}'";
		}

		/// <summary>
		/// Creates a DynamicParameters instance from an object's public properties.
		/// If any property has a null value, it is converted to DBNull.Value.
		/// </summary>
		/// <param name="obj">The source object containing parameter values.</param>
		/// <returns>A DynamicParameters object with the given properties as parameters.</returns>
		public static DynamicParameters CreateDynamicParameters(object obj)
		{
			if (obj == null)
				throw new ArgumentNullException(nameof(obj));

			var parameters = new DynamicParameters();
			PropertyInfo[] properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach (var property in properties)
			{
				// Get the property value; use DBNull.Value if null.
				var value = property.GetValue(obj) ?? DBNull.Value;

				// Add the parameter using the property name as the parameter name.
				parameters.Add(property.Name, value);
			}

			return parameters;
		}
	}
}
