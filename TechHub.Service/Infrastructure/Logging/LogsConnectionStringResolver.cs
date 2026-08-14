using Microsoft.Extensions.Configuration;

namespace TechHub.Service.Infrastructure.Logging;

/// <summary>
/// Resolves the SQL connection string used by the logging subsystem.
///
/// Resolution order:
///   1. "LogsDb"            (dedicated log database, if configured)
///   2. "DefaultConnection" (fallback)
///   3. "DbConnectionString" (this codebase's primary connection string — kept
///      as a final fallback so logging works out of the box)
///
/// Every connection is created fresh per flush and is fully self-contained —
/// this module never touches the scoped connection/transaction pattern used by
/// <c>CommandRepositoryService</c> elsewhere.
/// </summary>
public static class LogsConnectionStringResolver
{
    public static string? Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.GetConnectionString("LogsDb")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("DbConnectionString");
    }
}