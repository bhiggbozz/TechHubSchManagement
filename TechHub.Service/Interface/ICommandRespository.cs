using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dapper.SqlMapper;

namespace TechHub.Service.Interface
{
	public interface ICommandRespository<T> where T : class
	{
		Task Create(T entity);
		Task UpdateTableColumnById(string columnToUpdateName, string keyColumnName, object columnToUpdateValue, object KeyColumnValue);
		Task<Guid> CreateWithReturnedID(SqlTransaction transaction, SqlConnection connection, Dictionary<string, object> obj);
		Task UpdateTableColumnById(Dictionary<string, object> obj, KeyValuePair<string, object> keyValue);
		Task Create(SqlTransaction transaction, SqlConnection connection, Dictionary<string, object> obj);
		Task UpdateAsync(string query, Dictionary<string, object> values);
		//Task UpdateAsync(SqlTransaction transaction, SqlConnection connection, string query, Dictionary<string, object> values);
		Task UpdateAsync(SqlTransaction transaction, SqlConnection connection, string query, Dictionary<string, object> values, KeyValuePair<string, object> keyValuePair);
		Task CreateBatchAsync(SqlTransaction transaction, SqlConnection connection, List<Dictionary<string, object>> batchValues);
		Task UpdateBatchByIdAsync(SqlTransaction transaction, SqlConnection connection, List<Dictionary<string, object>> batchValues);
		Task Create(SqlTransaction transaction, SqlConnection connection, T entity);

    }
}
