using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Reflection;

namespace TechHub.Entity.Migration;

public class DatabaseInitializer : IHostedService
{
    private readonly IConfiguration _configuration;

    public DatabaseInitializer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connStr = _configuration.GetConnectionString("DbConnectionString");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            Log.Warning("DatabaseInitializer: DbConnectionString not found. Skipping migration.");
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "TechHub.Entity.Migration.Migration.Script_Initial.sql";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            Log.Error("DatabaseInitializer: Embedded resource '{Resource}' not found.", resourceName);
            return;
        }

        using var reader = new StreamReader(stream);
        var script = await reader.ReadToEndAsync();

        // Split by GO statements (SQL Server batch separator)
        var batches = script.Split(
            new[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO\n", "\nGO\r\n", "\r\nGO", "\nGO", "GO\r\n", "GO\n" },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        Log.Information("DatabaseInitializer: Running initial migration ({BatchCount} batches)...", batches.Length);

        try
        {
            await using var connection = new SqlConnection(connStr);
            await connection.OpenAsync(cancellationToken);

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                    continue;

                await using var cmd = new SqlCommand(batch, connection);
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            Log.Information("DatabaseInitializer: Initial migration completed successfully.");

            // Schema migrations for new columns
            var schemaMigrations = new[]
            {
                "ALTER TABLE AssessmentQuestion ADD SubTopicId UNIQUEIDENTIFIER NULL",
                "ALTER TABLE School ADD State NVARCHAR(100) NULL"
            };

            foreach (var migrationSql in schemaMigrations)
            {
                try
                {
                    await using var cmd = new SqlCommand(migrationSql, connection);
                    cmd.CommandTimeout = 120;
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                    Log.Information("DatabaseInitializer: Schema migration applied: {Sql}", migrationSql);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "DatabaseInitializer: Schema migration skipped (may already exist): {Sql}", migrationSql);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DatabaseInitializer: Initial migration failed (non-fatal, continuing startup).");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
