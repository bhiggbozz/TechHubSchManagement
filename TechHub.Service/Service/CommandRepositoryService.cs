using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;

//using System.Data.SqlClient;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;
using TechHub.Core.Helper;
using TechHub.Service.Interface;

namespace TechHub.Service.Service
{
	public class CommandRepositoryService<TEntity> : ICommandRespository<TEntity> where TEntity : class
	{
		private readonly IConfiguration _configuration;
		private readonly string? _config;
		private readonly IConnectionStringResolver _resolver;


		public CommandRepositoryService(IConfiguration configuration, IConnectionStringResolver resolver)
		{
			_configuration = configuration;
			_config = _configuration.GetConnectionString("DbConnectionString") ?? null;
			_resolver = resolver;
		}
		public async Task Create(TEntity entity)
		{
			ArgumentNullException.ThrowIfNull(nameof(_config));
			try
			{
				using var conn = new SqlConnection(_config);
				conn.Open();
				var tableName = typeof(TEntity).Name;
				var query = QueryBuilder<TEntity>.GenerateInsertQuery(tableName, entity);
				var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(entity);
				await conn.ExecuteAsync(query, sqlParameter);
			}
			catch(Exception ex)
			{
				throw;
			}
			
			//var sqlQuery =  

		}

		

		public async Task Create(Dictionary<string, object> obj)
		{
			ArgumentNullException.ThrowIfNull(nameof(_config));
			try
			{
				using var conn = new SqlConnection(_config);
				conn.Open();
				var tableName = typeof(TEntity).Name;
				var query = QueryBuilder<TEntity>.InsertQueryV2(obj, tableName);
			    var parameter = new DynamicParameters();
				foreach (var key in obj.Keys)
				{
					var value = obj[key];

					if (value is DBNull || value == null)
						parameter.Add($"@{key}", null);
					else
						parameter.Add($"@{key}", value);
				};
				await conn.ExecuteAsync(query, parameter);
			}
			catch (Exception ex)
			{
				throw;
			}

			//var sqlQuery =  

		}
		public async Task<Guid> CreateWithReturnedID(SqlTransaction transaction, SqlConnection connection, Dictionary<string, object> obj)
		{
			ArgumentNullException.ThrowIfNull(nameof(_config));
			try
			{
				//using var conn = new SqlConnection(_config);
				//conn.Open();
				var tableName = typeof(TEntity).Name;
				var query = QueryBuilder<TEntity>.InsertQueryWithReturnedID(obj, tableName);
				//using var command = new SqlCommand(query, connection, transaction)
				//var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(entity);
				return await connection.QuerySingleAsync<Guid>(query, null, transaction);
				// await command.ExecuteScalarAsync();
			}
			catch (Exception ex)
			{
				throw;
			}

			//var sqlQuery =  

		}

		public async Task Create(SqlTransaction transaction, SqlConnection connection, Dictionary<string, object> obj)
		{
			try
			{
				var tableName = typeof(TEntity).Name;
				var query = QueryBuilder<TEntity>.InsertQuery(obj, tableName);

				var parameter = new DynamicParameters();
				foreach (var key in obj.Keys)
				{
					var value = obj[key];

					if (value is DBNull || value == null)
						parameter.Add($"@{key}", null);
					else if (value is bool boolValue)
						parameter.Add($"@{key}", boolValue, DbType.Boolean);
					else if (value is Guid guidValue)
						parameter.Add($"@{key}", guidValue, DbType.Guid);
					else if (value is DateTime dateValue)
						parameter.Add($"@{key}", dateValue, DbType.DateTime2);
					else
						parameter.Add($"@{key}", value);
				}

				await connection.ExecuteAsync(query, parameter, transaction);
			}
			catch (Exception ex)
			{
				throw;
			}
		}



		public async Task Create(SqlTransaction transaction, SqlConnection connection, TEntity entity)
        {

            ArgumentNullException.ThrowIfNull(nameof(_config));
            try
            {
				
					var tableName = typeof(TEntity).Name;
					var query = QueryBuilder<TEntity>.GenerateInsertQuery(tableName, entity);
					var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(entity);
					await connection.ExecuteAsync(query, sqlParameter, transaction);
				

				//using var conn = new SqlConnection(_config);
				//conn.Open();
				//var tableName = typeof(TEntity).Name;
				//var query = QueryBuilder<TEntity>.GenerateInsertQuery(tableName, entity);
				//var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(entity);
				//await conn.ExecuteAsync(query, sqlParameter, transaction);
				//using var conn = new SqlConnection(_config);
				//conn.Open();
				//var tableName = typeof(TEntity).Name;
				//var query = QueryBuilder<TEntity>.InsertQuery(obj, tableName);
				//using var command = new SqlCommand(query, connection, transaction)
				//var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(entity);
				// await connection.ExecuteAsync(query, null, transaction);
				// await command.ExecuteScalarAsync();
			}
            catch (Exception ex)
            {
                throw;
            }

            //var sqlQuery =  

        }

		public async Task Create(SqlTransaction transaction, SqlConnection connection, TEntity entity, DatabaseTarget target)
		{
			var connectionString = _resolver.Resolve(target);
			ArgumentNullException.ThrowIfNull(nameof(connectionString));
			try
			{

				//using var conn = new SqlConnection(connectionString);
				//conn.Open();
				var tableName = typeof(TEntity).Name;
				var query = QueryBuilder<TEntity>.GenerateInsertQuery(tableName, entity);
				var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(entity);
				await connection.ExecuteAsync(query, sqlParameter, transaction);
				//using var conn = new SqlConnection(_config);
				//conn.Open();
				//var tableName = typeof(TEntity).Name;
				//var query = QueryBuilder<TEntity>.InsertQuery(obj, tableName);
				//using var command = new SqlCommand(query, connection, transaction)
				//var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(entity);
				// await connection.ExecuteAsync(query, null, transaction);
				// await command.ExecuteScalarAsync();
			}
			catch (Exception ex)
			{
				throw;
			}

			//var sqlQuery =  

		}

		public async Task UpdateTableColumnById( string columnToUpdateName, string keyColumnName, object columnToUpdateValue, object KeyColumnValue)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = QueryBuilder<TEntity>.GenerateUpdateQuery<TEntity>(tableName, columnToUpdateName,keyColumnName);
			var parameter = new DynamicParameters();
			parameter.Add($"@{columnToUpdateName}", columnToUpdateValue);
			parameter.Add($"@{keyColumnName}", KeyColumnValue);
			await conn.ExecuteAsync(query, parameter);
		}

		public async Task UpdateTableColumnById(Dictionary<string, object> obj, KeyValuePair<string, object> keyValue)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = QueryBuilder<TEntity>.UpdateQueryWithSingleColumnName(obj, keyValue.Key, tableName);
			var parameter = new DynamicParameters();
			foreach(var key in obj.Keys)
			{
				parameter.Add($"@{key}", obj[key]);
			}
			
			parameter.Add($"@{keyValue.Key}", keyValue.Value);
			await conn.ExecuteAsync(query, parameter);
		}

		public async Task UpdateAsync(string query, Dictionary<string, object> values)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			//var query = QueryBuilder<TEntity>.UpdateQueryWithSingleColumnName(obj, keyValue.Key, tableName);
			var parameter = new DynamicParameters();
			foreach (var key in values.Keys)
			{
				parameter.Add($"@{key}", values[key]);
			}

			//parameter.Add($"@{keyValue.Key}", keyValue.Value);
			await conn.ExecuteAsync(query, parameter);
		}
		public async Task UpdateAsync(SqlTransaction transaction, SqlConnection connection, string query, Dictionary<string, object> values, KeyValuePair<string, object> keyValuePair)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query2 = QueryBuilder<TEntity>.UpdateQueryWithSingleColumnName(values, keyValuePair.Key,tableName);
			var parameter = new DynamicParameters();
			foreach (var key in values.Keys)
			{
				parameter.Add($"@{key}", values[key]);
			}
			parameter.Add($"@{keyValuePair.Key}", keyValuePair.Value);
			//parameter.Add($"@{keyValue.Key}", keyValue.Value);
			await conn.ExecuteAsync(query2, parameter);
		}

		public async Task CreateBatchAsync(SqlTransaction transaction,SqlConnection connection,List<Dictionary<string, object>> batchValues)
		{
			var tableName = typeof(TEntity).Name;
			var query = QueryBuilder<TEntity>.BatchInsertQuery(batchValues, tableName);

			var parameter = new DynamicParameters();
			int batchCount = batchValues.Count;
			foreach (var items in batchValues)
			{
				int count2 = items.Count;
				foreach (var item in items.Keys)
				{
					count2 -= 1;
					parameter.Add($"@{item}_{count2}_{batchCount}", items[item]);
				}
				batchCount -= 1;
			}

			await connection.ExecuteAsync(query, parameter, transaction: transaction);
		}
		public async Task UpdateBatchByIdAsync(SqlTransaction transaction, SqlConnection connection, List<Dictionary<string, object>> batchValues)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = QueryBuilder<TEntity>.UpdateBatchWithId(batchValues, tableName);
			var parameter = new DynamicParameters();
			int batchCount = batchValues.Count;
			foreach (var items in batchValues)
			{
				int count2 = items.Count;
				foreach (var item in items.Keys)
				{
					count2 -= 1;
					parameter.Add($"@{item}_{count2}_{batchCount}", items[item]);
				}
				batchCount -= 1;
			}
			//	foreach (var key in values.Keys)
			//{
			//	parameter.Add($"@{key}", values[key]);
			//}

			//parameter.Add($"@{keyValue.Key}", keyValue.Value);
			await conn.ExecuteAsync(query, parameter);
		}

		public async Task UpdateBatchByIdAsyncV2(SqlTransaction transaction, SqlConnection connection, List<Dictionary<string, object>> batchValues)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var parameter = new DynamicParameters();
			var queryBuilder = new StringBuilder();

			for (int i = 0; i < batchValues.Count; i++)
			{
				var values = batchValues[i];

				if (!values.ContainsKey("Id"))
					throw new ArgumentException($"Batch item at index {i} is missing required 'Id' key");

				// Build SET clause excluding Id
				var setClauses = values.Keys
					.Where(k => k != "Id")
					.Select(k => $"[{k}] = @{k}_{i}");

				queryBuilder.AppendLine($@"
					UPDATE [{tableName}] 
					SET {string.Join(", ", setClauses)}
					WHERE [Id] = @Id_{i};");

				// Add parameters with consistent naming @ColumnName_rowIndex
				foreach (var key in values.Keys)
				{
					parameter.Add($"@{key}_{i}", values[key]);
				}
			}

			await conn.ExecuteAsync(queryBuilder.ToString(), parameter);
		}

		public async Task Create(TEntity entity,DatabaseTarget target)
		{
			try
			{
				var connectionString = _resolver.Resolve(target);

				using var conn = new SqlConnection(connectionString);
				conn.Open();

				var tableName = typeof(TEntity).Name;
				var query = QueryBuilder<TEntity>.GenerateInsertQuery(tableName, entity);
				var parameters = QueryBuilder<TEntity>.CreateDynamicParameters(entity);

				await conn.ExecuteAsync(query, parameters);
			}
			catch (Exception)
			{
				throw;
			}
		}

		public async Task UpdateTableColumnById(string columnToUpdateName, string keyColumnName, object columnToUpdateValue, object KeyColumnValue, DatabaseTarget target)
		{
			var connectionString = _resolver.Resolve(target);
			using var conn = new SqlConnection(connectionString);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = QueryBuilder<TEntity>.GenerateUpdateQuery<TEntity>(tableName, columnToUpdateName, keyColumnName);
			var parameter = new DynamicParameters();
			parameter.Add($"@{columnToUpdateName}", columnToUpdateValue);
			parameter.Add($"@{keyColumnName}", KeyColumnValue);
			await conn.ExecuteAsync(query, parameter);
		}

		public async Task UpdateTableColumnById(Dictionary<string, object> obj, KeyValuePair<string, object> keyValue, DatabaseTarget target)
		{
			var connectionString = _resolver.Resolve(target);
			using var conn = new SqlConnection(connectionString);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = QueryBuilder<TEntity>.UpdateQueryWithSingleColumnName(obj, keyValue.Key, tableName);
			var parameter = new DynamicParameters();
			foreach (var key in obj.Keys)
			{
				parameter.Add($"@{key}", obj[key]);
			}

			parameter.Add($"@{keyValue.Key}", keyValue.Value);
			await conn.ExecuteAsync(query, parameter);
		}

		public async Task UpdateTableColumnById(SqlTransaction transaction, SqlConnection connection,Dictionary<string, object> obj, KeyValuePair<string, object> keyValue, DatabaseTarget target)
		{
			//var connectionString = _resolver.Resolve(target);
			//using var conn = new SqlConnection(connectionString);
			//conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = QueryBuilder<TEntity>.UpdateQueryWithSingleColumnName(obj, keyValue.Key, tableName);
			var parameter = new DynamicParameters();
			foreach (var key in obj.Keys)
			{
				parameter.Add($"@{key}", obj[key]);
			}

			parameter.Add($"@{keyValue.Key}", keyValue.Value);
			await connection.ExecuteAsync(query, parameter, transaction);
		}

		public async Task RevokeToken(SqlTransaction transaction, SqlConnection connection, Guid tokenId, string replacedByToken = null)
		{
			const string sql = @"
				UPDATE RefreshTokens
				SET IsRevoked       = 1,
					RevokedAt       = @RevokedAt,
					ReplacedByToken = @ReplacedByToken
				WHERE Id = @Id";

			var parameter = new DynamicParameters();
			parameter.Add("@Id", tokenId);
			parameter.Add("@RevokedAt", DateTime.UtcNow);
			parameter.Add("@ReplacedByToken", replacedByToken);

			await connection.ExecuteAsync(sql, parameter, transaction);
		}

		public async Task RevokeAllTokensForUser(SqlTransaction transaction, SqlConnection connection,Guid userId)
		{
			const string sql = @"
				UPDATE RefreshTokens
				SET IsRevoked = 1,
					RevokedAt = @RevokedAt
				WHERE UserId    = @UserId
				  AND IsRevoked = 0";

			var parameter = new DynamicParameters();
			parameter.Add("@UserId", userId);
			parameter.Add("@RevokedAt", DateTime.UtcNow);

			await connection.ExecuteAsync(sql, parameter, transaction);
		}



	}
}
