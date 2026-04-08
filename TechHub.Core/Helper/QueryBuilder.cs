using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
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

		public static string GenerateUpdateQuery<TEntity>(string tableName, object columnToUpdateName, string keyColumnName)
		{
			//var properties = typeof(TEntity).GetProperties().Select(p => p.Name)
			//										  .Where(p => !p.Equals(keyColumn, StringComparison.OrdinalIgnoreCase)).ToList();
			//string columns = string.Join(", ", properties.Select(p => "{p} = @{p}"));
			string query = $"Update {tableName} Set {columnToUpdateName} = @{columnToUpdateName} where {keyColumnName} = @{keyColumnName}";
			return query;
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
			PropertyInfo[] properties = obj.GetType()
				.GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach (var property in properties)
			{
				var value = property.GetValue(obj);
				var propertyType = property.PropertyType;

				// Handle nullable types
				var underlyingType = Nullable.GetUnderlyingType(propertyType);
				var isNullable = underlyingType != null;

				if (value == null)
				{
					// Null value — add as DBNull with correct db type
					// This prevents the DBNull cast exception Dapper throws
					// for nullable Guid, nullable int, nullable DateTime etc
					if (isNullable && underlyingType == typeof(Guid))
					{
						parameters.Add(property.Name,null,DbType.Guid);
					}
					else if (isNullable && underlyingType == typeof(int))
					{
						parameters.Add(property.Name,null,DbType.Int32);
					}
					else if (isNullable && underlyingType == typeof(DateTime))
					{
						parameters.Add(property.Name,null,DbType.DateTime);
					}
					else if (isNullable && underlyingType == typeof(bool))
					{
						parameters.Add(property.Name,null,DbType.Boolean);
					}
					else if (isNullable && underlyingType == typeof(decimal))
					{
						parameters.Add(property.Name,null,DbType.Decimal);
					}
					else
					{
						// String and all other reference types
						parameters.Add(property.Name, null);
					}
				}
				else
				{
					// Non-null value — add directly
					// Dapper handles all concrete types correctly
					parameters.Add(property.Name, value);
				}
			}

			return parameters;
		}
		public static string InsertQueryWithReturnedID(Dictionary<string, object> data, string tableName)
		{
			var queries = new StringBuilder();
			var sb = new StringBuilder($"insert into {tableName} (  ");
			int count = data.Count;

			foreach (var item in data.Keys)
			{
				count -= 1;
				sb.Append($"{item}");
				if (count == 0)
				{
					sb.Append(" )");
				}
				else
				{
					sb.Append(",");
				}


			}
			sb.Append(" values ( ");
			int count2 = data.Count;

			foreach (var item in data.Keys)
			{
				count2 -= 1;
				sb.Append($"'{data[item]}'");
				if (count2 == 0)
				{
					sb.Append(" )");
				}
				else
				{
					sb.Append(",");
				}

			}
			sb.Append(" \nSELECT SCOPE_IDENTITY();");
			var query = sb.ToString();
			return query;


		}

		public static string InsertQuery(Dictionary<string, object> data, string tableName)
		{
			var queries = new StringBuilder();
			var sb = new StringBuilder($"insert into {tableName} (  ");
			int count = data.Count;

			foreach (var item in data.Keys)
			{
				count -= 1;
				sb.Append($"{item}");
				if (count == 0)
				{
					sb.Append(" )");
				}
				else
				{
					sb.Append(",");
				}


			}
			sb.Append(" values ( ");
			int count2 = data.Count;

			foreach (var item in data.Keys)
			{
				count2 -= 1;
				sb.Append($"'{data[item]}'");
				if (count2 == 0)
				{
					sb.Append(" )");
				}
				else
				{
					sb.Append(",");
				}

			}
			//sb.Append(" \nSELECT SCOPE_IDENTITY();");
			var query = sb.ToString();
			return query;


		}

		public static string InsertQueryV2(Dictionary<string, object> data, string tableName)
		{
			var queries = new StringBuilder();
			var sb = new StringBuilder($"insert into {tableName} (  ");
			int count = data.Count;

			foreach (var item in data.Keys)
			{
				count -= 1;
				sb.Append($"{item}");
				if (count == 0)
				{
					sb.Append(" )");
				}
				else
				{
					sb.Append(",");
				}


			}
			sb.Append(" values ( ");
			int count2 = data.Count;

			foreach (var item in data.Keys)
			{
				count2 -= 1;
				sb.Append($"@{item}");
				if (count2 == 0)
				{
					sb.Append(" )");
				}
				else
				{
					sb.Append(",");
				}

			}
			//sb.Append(" \nSELECT SCOPE_IDENTITY();");
			var query = sb.ToString();
			return query;


		}

		public static string UpdateQueryWithSingleColumnName(Dictionary<string , object> obj, string keyId, string tableName)
		{
			var queries = new StringBuilder();
			var sb = new StringBuilder($"update {tableName} set ");
			int count = obj.Count;
			foreach(var item in obj.Keys)
			{
				
				count -= 1;
				sb.Append($"{item} = @{item}");
                if(count > 0)
				{
					sb.Append(", ");
				}


			}
			sb.Append($" where {keyId} = @{keyId}");
			return sb.ToString();

		}


		public static string BatchInsertQuery(List<Dictionary<string, object>> batchData, string tableName)
		{
			var queries = new StringBuilder();
			int batchCount = batchData.Count;
			foreach (var items in batchData)
			{
				var sb = new StringBuilder($"insert into {tableName} (  ");

				int count = items.Count;

				foreach(var item in items.Keys)
				{
					count -= 1;
					sb.Append($"{item}");
					if (count == 0)
					{
						sb.Append(" )");
					}
					else
					{
						sb.Append(",");
					}


				}
				sb.Append(" values ( ");
				int count2 = items.Count;

				foreach (var item in items.Keys)
				{
					count2 -= 1;
					sb.Append($"@{item}_{count2}_{batchCount}");
					if (count2 == 0)
					{
						sb.Append(" )");
					}
					else
					{
						sb.Append(",");
					}

				}
				var query = sb.ToString() + "\n";
				queries.Append(query);
				batchCount--;

			}

			return queries.ToString();
		}

		public static string UpdateBatchWithId(List<Dictionary<string, object>> data, string tableName)
		{
			var queries = new StringBuilder();
			int batchCount = data.Count;
			foreach (var item in data)
			{
				var sb = new StringBuilder($"update {tableName} set ");

				int count = item.Count;
				foreach (var column in item.Keys)
				{
					count -= 1;
					sb.Append($"{column} = @{column}_{batchCount}_{count}");
					if (count != 0)
					{
						sb.Append(", ");
					}


				}
				sb.Append($" where Id = @Id_{batchCount}_{count}");
				queries.Append(sb.ToString());
				queries.Append("\n");
				batchCount--;
			}
			var query = queries.ToString();
			return query;


		}

	}
}
