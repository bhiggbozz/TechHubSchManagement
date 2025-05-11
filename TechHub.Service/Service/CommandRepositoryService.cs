using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
//using System.Data.SqlClient;
using System.Linq;
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
	}
}
