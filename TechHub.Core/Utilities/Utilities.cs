using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Utilities
{
    public class Utilities : IUtilities
    {
        public Utilities()
        {
        }
		public  string SelectQueryWithColumns(Dictionary<string, object> obj, string tableName)
		{
			var queries = new StringBuilder();
			var sb = new StringBuilder($"select * from  {tableName} where ");
			int count = obj.Count;
			foreach (var item in obj.Keys)
			{

				count -= 1;
				sb.Append($"{item} = @{item}");
				if (count > 0)
				{
					sb.Append(" and ");
				}


			}
			//sb.Append($" where {keyId} = @{keyId}");
			return sb.ToString();

		}
		public string SelectAllBySingleColumn(KeyValuePair<string, object> obj, string tableName) 
		{
			var query = $"SELECT * FROM {tableName} WHERE {obj.Key} = @{obj.Key}";
			return query;
		}
	}
}
