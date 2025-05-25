using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Service.Service.DatabaseService
{
	public class DbTransactionScope : IDbTransactionScope
	{
		private readonly SqlConnection _connection;
		private readonly SqlTransaction _transaction;
		private bool _committed = false;

		public SqlConnection Connection => _connection;
		public SqlTransaction Transaction => _transaction;

		public DbTransactionScope(string connectionString)
		{
			_connection = new SqlConnection(connectionString);
			_connection.Open();
			_transaction = _connection.BeginTransaction();
		}

		public async Task CommitAsync()
		{
			_transaction.Commit();
			_committed = true;
			await Task.CompletedTask;
		}

		public async Task RollbackAsync()
		{
			if (!_committed)
			{
				_transaction.Rollback();
			}
			await Task.CompletedTask;
		}

		public void Dispose()
		{
			if (!_committed)
			{
				_transaction.Rollback();
			}

			_transaction.Dispose();
			_connection.Dispose();
		}
	}

}
