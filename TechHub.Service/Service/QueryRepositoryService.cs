using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.ResponseModel;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Helper;
using TechHub.Core.Model;
using TechHub.Core.Utilities;
using TechHub.Service.Interface;
using static Dapper.SqlMapper;

namespace TechHub.Service.Service
{
	public class QueryRepositoryService<TEntity> : IQueryRepository<TEntity> where TEntity : class
	{
		private readonly IConfiguration _configuration;
		private readonly IUtilities _utilities;
		private readonly IConnectionStringResolver _resolver;

		private readonly string? _config;
		public QueryRepositoryService(IConfiguration configuration, IUtilities utilities, IConnectionStringResolver resolver) 
		{
			_configuration = configuration;
			_utilities = utilities;
			_resolver = resolver;
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

		public async Task<TEntity?> GetByPropertyName(string propertyName, string value, DatabaseTarget target)
		{
			var connectionString = _resolver.Resolve(target);
			using var conn = new SqlConnection(connectionString);
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

		public async Task<int> CountAsync(string query, Dictionary<string, object> values)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var parameter = new DynamicParameters();
			foreach (var key in values.Keys)
			{
				parameter.Add($"@{key}", values[key]);
			}

			var result = await conn.QueryFirstOrDefaultAsync<int>(query, parameter);
			return result;
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
		public async Task<IEnumerable<T>> QueryAsync<T>(string query, Dictionary<string, object> values)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();

			var parameter = new DynamicParameters();
			foreach (var key in values.Keys)
			{
				parameter.Add($"@{key}", values[key]);
			}

			var result = await conn.QueryAsync<T>(query, parameter);
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

		public async Task<List<Users>> GetUsersBySchoolAndRole(Guid schoolId, int? roleId)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();

			var query = "SELECT * FROM Users WHERE SchoolId = @SchoolId";
			var parameters = new DynamicParameters();
			parameters.Add("@SchoolId", schoolId);

			// If roleId is provided and valid, filter by role
			if (roleId.HasValue && roleId.Value >= 0)
			{
				query += " AND RoleId = @RoleId";
				parameters.Add("@RoleId", roleId.Value);
			}

			query += " ORDER BY FirstName, LastName";

			var users = await conn.QueryAsync<Users>(query, parameters);
			return users.ToList();
		}

		public async Task<List<Users>> GetTeachersBySchool(Guid schoolId)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();

			var query = @"
                SELECT * FROM Users 
                WHERE SchoolId = @SchoolId 
                AND RoleId IN (@SubjectTeacher, @HeadTeacher)
                ORDER BY FirstName, LastName";

			var parameters = new DynamicParameters();
			parameters.Add("@SchoolId", schoolId);
			parameters.Add("@SubjectTeacher", 4); // SubjectTeacher
			parameters.Add("@HeadTeacher", 1);     // HeadTeacher

			var users = await conn.QueryAsync<Users>(query, parameters);
			return users.ToList();
		}

		public async Task<List<Users>> GetAdministratorsBySchool(Guid schoolId)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();

			var query = @"
                SELECT * FROM Users 
                WHERE SchoolId = @SchoolId 
                AND RoleId IN (@Administrator, @SuperAdministrator)
                ORDER BY FirstName, LastName";

			var parameters = new DynamicParameters();
			parameters.Add("@SchoolId", schoolId);
			parameters.Add("@Administrator", 2);        // Administrator
			parameters.Add("@SuperAdministrator", 3);   // SuperAdministrator

			var users = await conn.QueryAsync<Users>(query, parameters);
			return users.ToList();
		}

		public async Task<bool> EmailExistsInSchool(string email, Guid schoolId, Guid? excludeUserId = null)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();

			var query = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1 
                    FROM Users 
                    WHERE LOWER(EmailAddress) = @Email 
                    AND SchoolId = @SchoolId";

			var parameters = new DynamicParameters();
			parameters.Add("@Email", email.ToLower());
			parameters.Add("@SchoolId", schoolId);

			if (excludeUserId.HasValue)
			{
				query += " AND Id != @ExcludeUserId";
				parameters.Add("@ExcludeUserId", excludeUserId.Value);
			}

			query += ") THEN 1 ELSE 0 END";

			var exists = await conn.ExecuteScalarAsync<bool>(query, parameters);
			return exists;
		}

		public async Task<IEnumerable<TEntity?>> GetByQuery(string query, DatabaseTarget target)
		{
			var connectionString = _resolver.Resolve(target);
			using var conn = new SqlConnection(_config);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			//var query = QueryBuilder<TEntity>.GenerateGetbyIdQuery();
			//var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(id);
			var result = await conn.QueryAsync<TEntity>(query);
			return result;
		}

		public async Task<int> CountAsync(string query, Dictionary<string, object> values, DatabaseTarget target)
		{
			var connectionString = _resolver.Resolve(target);
			using var conn = new SqlConnection(connectionString);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var parameter = new DynamicParameters();
			foreach (var key in values.Keys)
			{
				parameter.Add($"@{key}", values[key]);
			}

			var result = await conn.QueryFirstOrDefaultAsync<int>(query, parameter);
			return result;
		}

		public async Task<int> CountAsync(string query,DatabaseTarget target)
		{
			var connectionString = _resolver.Resolve(target);
			using var conn = new SqlConnection(connectionString);
			conn.Open();
			var result = await conn.QueryFirstOrDefaultAsync<int>(query);
			return result;
		}

		public async Task<IEnumerable<QuestionQueryResult>> GetByQueryForQuestion(string query, DatabaseTarget target)
		{
			var connectionString = _resolver.Resolve(target);
			using var conn = new SqlConnection(connectionString);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			//var query = QueryBuilder<TEntity>.GenerateGetbyIdQuery();
			//var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(id);
			var result = await conn.QueryAsync<QuestionQueryResult>(query);
			return result;
		}

		public async Task<TEntity?> Get(Guid id, DatabaseTarget target)
		{
			var connectionString = _resolver.Resolve(target);
			using var conn = new SqlConnection(connectionString);
			conn.Open();
			var tableName = typeof(TEntity).Name;
			var query = QueryBuilder<TEntity>.GenerateGetbyIdQuery();
			var sqlParameter = new DynamicParameters();
			sqlParameter.Add($"@Id", id);
			//var sqlParameter = QueryBuilder<TEntity>.CreateDynamicParameters(id);
			var result = await conn.QueryFirstOrDefaultAsync<TEntity>(query, sqlParameter);
			return result;
		}
		public async Task<IEnumerable<TeacherResponseModel>> GetTeachersBySchoolAsync(Guid schoolId, DatabaseTarget target)
		{
			var sql = @"
				SELECT 
					u.Id,
					u.FirstName,
					u.LastName,
					u.UserName,
					u.EmailAddress,
					u.RoleId,
					u.IsActive,
					u.CreationDate
				FROM Users u
				WHERE u.SchoolId = @SchoolId
				AND u.IsActive = 1
				AND u.RoleId IN (@SubjectTeacherRole, @HeadTeacherRole)
				ORDER BY u.FirstName ASC";

			var connectionString = _resolver.Resolve(target);
			using var conn = new SqlConnection(connectionString);
			var teachers = await conn.QueryAsync<dynamic>(sql, new
			{
				SchoolId = schoolId,
				SubjectTeacherRole = (int)UserRole.SubjectTeacher,
				HeadTeacherRole = (int)UserRole.HeadTeacher
			});

			return teachers.Select(t => new TeacherResponseModel
			{
				Id = t.Id,
				FirstName = t.FirstName,
				LastName = t.LastName,
				UserName = t.UserName,
				EmailAddress = t.EmailAddress,
				Role = ((UserRole)t.RoleId).ToString(),
				IsActive = t.IsActive,
				CreationDate = t.CreationDate
			});
		}

		public async Task<TEntity?> GetByToken(string token)
		{
			using var conn = new SqlConnection(_config);
			conn.Open();
			const string sql = "SELECT * FROM RefreshTokens WHERE Token = @Token";
			var parameter = new DynamicParameters();
			parameter.Add("@Token", token);
			return await conn.QueryFirstOrDefaultAsync<TEntity>(sql, parameter);
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
