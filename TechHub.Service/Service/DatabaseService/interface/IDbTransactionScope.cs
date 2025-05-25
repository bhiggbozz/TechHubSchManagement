using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Service.Service.DatabaseService
{
	public interface IDbTransactionScope : IDisposable
	{
		SqlConnection Connection { get; }
		SqlTransaction Transaction { get; }
		Task CommitAsync();
		Task RollbackAsync();
	}

}
