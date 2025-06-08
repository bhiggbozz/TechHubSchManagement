using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Service.Interface
{
	public interface IQueryRepository<TEntity> where TEntity : class
	{
		Task<TEntity?> Get(Guid id);
		Task<TEntity?> Get(string query);
		Task<TEntity?> GetByPropertyName(string propertyName, string value);
		Task<IEnumerable<TEntity?>> GetByQuery(string query);
		Task<TEntity?> GetBy(Dictionary<string, object> inputValues);
		Task<TEntity?> SelectByColumns(string query, Dictionary<string, object> values);
		Task<IEnumerable<TEntity?>> SelectByColumn(string query, KeyValuePair<string, object> values);
		Task<IEnumerable<TEntity?>> SelectAllBySingleColumn(KeyValuePair<string, object> values);
	}
}
