using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TechHub.Service.Service.DatabaseService
{
	public interface IDbTransactionScopeFactory
	{
		IDbTransactionScope Create(string connString);
	}

	public class DbTransactionScopeFactory : IDbTransactionScopeFactory
	{
		private readonly IConfiguration _configuration;

		public DbTransactionScopeFactory(IConfiguration configuration)
		{
			_configuration = configuration;
		}

		public IDbTransactionScope Create(string connectionStringName)
		{
			var connStr = _configuration.GetConnectionString(connectionStringName);
			if (string.IsNullOrWhiteSpace(connStr))
				throw new ArgumentException($"Connection string '{connectionStringName}' not found.");

			return new DbTransactionScope(connStr);
		}
	}

}
