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

namespace TechHub.Service.Service
{
	public class QueryRepositoryService<TEntity> : IQueryRepository<TEntity> where TEntity : class
	{
		private readonly IConfiguration _configuration;
		private readonly string? _config;
		public QueryRepositoryService(IConfiguration configuration) 
		{
			_configuration = configuration;
			_config = _configuration.GetConnectionString("DbConnectionString") ?? null;
			ArgumentNullException.ThrowIfNullOrEmpty(nameof(_config));
		}

		public async Task<TEntity?> Get(Guid id)
		{
			
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = QueryBuilder<TEntity>.GenerateGetbyIdQuery();
			var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(id);
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


	}
}
