using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Core.Utilities
{
	public interface IUtilities
	{
		string SelectQueryWithColumns(Dictionary<string, object> obj, string tableName);
		string SelectAllBySingleColumn(KeyValuePair<string, object> obj, string tableName);
	}
}
