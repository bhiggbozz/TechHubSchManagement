using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dapper.SqlMapper;
using TechHub.Core.Helper;
using TechHub.Service.Interface;
using Dapper;
using TechHub.Core.Utilities;

namespace TechHub.Service.Service
{
	public class QueryRepositoryService<TEntity> : IQueryRepository<TEntity> where TEntity : class
	{
		private readonly IConfiguration _configuration;
		private readonly IUtilities _utilities;
		private readonly string? _config;
		public QueryRepositoryService(IConfiguration configuration, IUtilities utilities) 
		{
			_configuration = configuration;
			_utilities = utilities;
			_config = _configuration.GetConnectionString("DbConnectionString") ?? null;
			ArgumentNullException.ThrowIfNullOrEmpty(nameof(_config));
		}

		public async Task<TEntity?> Get(Guid id)
		{
			
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = QueryBuilder<TEntity>.GenerateGetbyIdQuery();
			var sqlParameter = new DynamicParameters();
			sqlParameter.Add($"@Id", id);
			//var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(id);
			var result = await conn.QueryFirstOrDefaultAsync<TEntity>(query, sqlParameter);
			return result;
		}
		public async Task<TEntity?> Get(string query)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			//var query = QueryBuilder<TEntity>.GenerateGetbyIdQuery();
			//var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(id);
			var result = await conn.QueryFirstOrDefaultAsync<TEntity>(query);
			return result;
		}

		public async Task<IEnumerable<TEntity?>> GetByQuery(string query)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			//var query = QueryBuilder<TEntity>.GenerateGetbyIdQuery();
			//var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(id);
			var result = await conn.QueryAsync<TEntity>(query);
			return result;
		}
		public async Task<TEntity?> GetByPropertyName(string propertyName, string value)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = QueryBuilder<TEntity>.GenerateGetQueryByProperties(propertyName);
			var parameter = new DynamicParameters();
			parameter.Add($"@{propertyName}", value);
			var result = await conn.QueryFirstOrDefaultAsync<TEntity>(query, parameter);
			return result;
		}

		public async Task<TEntity?> GetBy(Dictionary<string, object> inputValues)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = _utilities.SelectQueryWithColumns(inputValues, tableName);
			var parameter = new DynamicParameters();
			foreach(var key in inputValues.Keys)
			{
				parameter.Add($"@{key}", inputValues[key]);
			}
			var result = await conn.QueryFirstOrDefaultAsync<TEntity>(query, parameter);
			return result;
		}

		public async Task<TEntity?> SelectByColumns(string query, Dictionary<string, object> values)
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
			var result = await conn.QueryFirstOrDefaultAsync<TEntity>(query, parameter);
			return result;
			//await conn.ExecuteAsync(query, parameter);
		}
		public async Task<IEnumerable<TEntity?>> SelectByColumn(string query, KeyValuePair<string, object> values)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			//var query = QueryBuilder<TEntity>.UpdateQueryWithSingleColumnName(obj, keyValue.Key, tableName);
			var parameter = new DynamicParameters();
			parameter.Add($"@{values.Key}", values.Value);

			var result = await conn.QueryAsync<TEntity>(query, parameter);
			return result;
		}
		public async Task<IEnumerable<TEntity?>> SelectAllBySingleColumn( KeyValuePair<string, object> values)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = _utilities.SelectAllBySingleColumn(values, tableName);
			var parameter = new DynamicParameters();
			parameter.Add($"@{values.Key}", values.Value);

			var result = await conn.QueryAsync<TEntity>(query, parameter);
			return result;
		}

		//public async Task<TEntity?> SelectAllBySingleColumn(KeyValuePair<string, object> values)
		//{
		//	using var conn = new SqlConnection(_config);
		//	conn.Open();
		//	var tableName = typeof(TEntity).Name;
		//	var query = _utilities.SelectAllBySingleColumn(values, tableName);
		//	var parameter = new DynamicParameters();
		//	parameter.Add($"@{values.Key}", values.Value);

		//	var result = await conn.QueryAsync<TEntity>(query, parameter);
		//	return result;
		//}

	}
}
