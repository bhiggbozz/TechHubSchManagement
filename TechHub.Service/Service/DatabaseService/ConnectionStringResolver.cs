using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;
using TechHub.Service.Interface;

namespace TechHub.Service.Service.DatabaseService;
/// <summary>
/// Resolves DatabaseTarget enum to actual connection string
/// Single place where enum-to-connectionstring mapping lives
/// Adding new database = add case here + appsettings entry
/// </summary>
public class ConnectionStringResolver : IConnectionStringResolver
{
	private readonly IConfiguration _configuration;

	public ConnectionStringResolver(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public string Resolve(DatabaseTarget target)
	{
		var key = target switch
		{
			DatabaseTarget.Core => "DbConnectionString",
			DatabaseTarget.QuestionBank => "QuestionBankConnection",
			_ => throw new ArgumentException(
					$"Unknown database target: {target}")
		};

		var connectionString = _configuration
			.GetConnectionString(key);

		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new InvalidOperationException(
				$"Connection string '{key}' is not configured " +
				$"for database target '{target}'");
		}

		return connectionString;
	}
}

