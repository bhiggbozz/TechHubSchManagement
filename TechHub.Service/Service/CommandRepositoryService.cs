using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
//using System.Data.SqlClient;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Helper;
using TechHub.Service.Interface;

namespace TechHub.Service.Service
{
	public class CommandRepositoryService<TEntity> : ICommandRespository<TEntity> where TEntity : class
	{
		private readonly IConfiguration _configuration;
		private readonly string? _config;

		public CommandRepositoryService(IConfiguration configuration)
		{
			_configuration = configuration;
			_config = _configuration.GetConnectionString("DbConnectionString") ?? null;
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
					parameter.Add($"@{key}", obj[key]);
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
			ArgumentNullException.ThrowIfNull(nameof(_config));
			try
			{
				//using var conn = new SqlConnection(_config);
				//conn.Open();
				var tableName = typeof(TEntity).Name;
				var query = QueryBuilder<TEntity>.InsertQuery(obj, tableName);
				//using var command = new SqlCommand(query, connection, transaction)
				//var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(entity);
				await connection.ExecuteAsync(query, null, transaction);
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
		public async Task UpdateAsync(SqlTransaction transaction, SqlConnection connection, string query, Dictionary<string, object> values)
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
	}
}
