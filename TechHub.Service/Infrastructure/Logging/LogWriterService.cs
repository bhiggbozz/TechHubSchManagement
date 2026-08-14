using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace TechHub.Service.Infrastructure.Logging;

/// <summary>
/// Background service that drains <see cref="LogChannel"/> and writes error
/// logs to SQL Server in small batched round trips so logging never adds
/// latency to requests.
///
/// Batching: entries accumulate into a list and are flushed when either
///   1. the batch reaches 50 entries, or
///   2. the channel is momentarily empty (with a short debounce window so a
///      burst of errors can still join the same batch — a lone error is
///      written within ~1 second).
///
/// Each flush is a single Dapper <c>ExecuteAsync</c> call that builds ONE
/// multi-row INSERT (per-row parameters), i.e. one round trip per batch. Every
/// flush opens a brand-new <see cref="SqlConnection"/> that is closed after
/// use — nothing is shared with the scoped repository pattern.
///
/// Failure behaviour: the flush is wrapped in a catch-all that swallows every
/// exception. If the database is down the batch is dropped silently — logging
/// must never crash the application.
/// </summary>
public sealed class LogWriterService : BackgroundService
{
    public const int BatchSize = 50;

    /// <summary>Debounce while the channel looks empty so near-simultaneous errors batch together.</summary>
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(250);

    private static readonly string[] Columns =
    {
        "LogLevel", "Message", "Exception", "Source", "Endpoint",
        "RequestPath", "RequestMethod", "UserId", "TenantId", "CorrelationId"
    };

    private readonly LogChannel _channel;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;

    public LogWriterService(LogChannel channel, IConfiguration configuration, ILogger logger)
    {
        _channel = channel;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<LogEntry>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var entry in _channel.ReadAllAsync(stoppingToken))
                {
                    batch.Add(entry);

                    // Full batch — flush immediately.
                    if (batch.Count >= BatchSize)
                    {
                        await FlushAsync(batch, stoppingToken);
                        batch = new List<LogEntry>(BatchSize);
                        continue;
                    }

                    // Channel momentarily empty — wait a hair, then flush if
                    // nothing else arrived, so a single error still lands
                    // promptly while a tight burst is batched into 50s.
                    if (_channel.Count == 0)
                    {
                        await Task.Delay(DebounceDelay, stoppingToken);

                        if (_channel.Count == 0)
                        {
                            await FlushAsync(batch, stoppingToken);
                            batch = new List<LogEntry>(BatchSize);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // app shutting down — drain below
            }
            catch (Exception ex)
            {
                // Writer must never die. Drop the current batch, resume draining.
                Log.Debug(ex, "ApplicationLogs: writer loop fault; continuing");
                batch = new List<LogEntry>(BatchSize);
            }
        }

        // Graceful shutdown: flush whatever is still queued. Pass
        // CancellationToken.None so the single best-effort flush can complete
        // even though the host token is already cancelled.
        _channel.DrainTo(batch);
        if (batch.Count > 0)
            await FlushAsync(batch, CancellationToken.None);
    }

    /// <summary>
    /// Writes a list of entries, chunked so no single INSERT exceeds SQL
    /// Server's 2100-parameter limit (9 params per row → 50-row chunks = 450).
    /// </summary>
    private async Task FlushAsync(List<LogEntry> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;

        for (int i = 0; i < batch.Count; i += BatchSize)
        {
            var chunk = batch.GetRange(i, Math.Min(BatchSize, batch.Count - i));
            await WriteBatchAsync(chunk, cancellationToken);
        }
    }

    private async Task WriteBatchAsync(List<LogEntry> batch, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = LogsConnectionStringResolver.Resolve(_configuration);
            if (string.IsNullOrWhiteSpace(connectionString))
                return;

            var sql = BuildBatchInsertSql(batch.Count);
            var parameters = BuildBatchParameters(batch);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Logging must never throw and never crash the app — if the DB is
            // down the batch is dropped silently. Debug-level fallback only.
            Log.Debug(ex, "ApplicationLogs: batch flush failed; batch dropped");
        }
    }

    /// <summary>
    /// Builds a single multi-row INSERT, e.g.
    /// <c>INSERT INTO ApplicationLogs (...) VALUES (@p0_LogLevel, ...), (@p1_LogLevel, ...)</c>.
    /// One ExecuteAsync → one round trip per batch. Parameter names use a
    /// letter prefix ("p") because T-SQL identifiers cannot start with a digit.
    /// </summary>
    private static string BuildBatchInsertSql(int count)
    {
        var sql = new StringBuilder("INSERT INTO ApplicationLogs (");
        sql.Append(string.Join(", ", Columns));
        sql.Append(") VALUES ");

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                sql.Append(',');

            sql.Append('(');
            for (int c = 0; c < Columns.Length; c++)
            {
                if (c > 0)
                    sql.Append(',');
                sql.Append("@p").Append(i).Append('_').Append(Columns[c]);
            }
            sql.Append(')');
        }

        return sql.ToString();
    }

    private static DynamicParameters BuildBatchParameters(List<LogEntry> batch)
    {
        var parameters = new DynamicParameters();

        for (int i = 0; i < batch.Count; i++)
        {
            var entry = batch[i];
            parameters.Add($"@p{i}_LogLevel", entry.LogLevel);
            parameters.Add($"@p{i}_Message", entry.Message);
            parameters.Add($"@p{i}_Exception", entry.Exception);
            parameters.Add($"@p{i}_Source", entry.Source);
            parameters.Add($"@p{i}_Endpoint", entry.Endpoint);
            parameters.Add($"@p{i}_RequestPath", entry.RequestPath);
            parameters.Add($"@p{i}_RequestMethod", entry.RequestMethod);
            parameters.Add($"@p{i}_UserId", entry.UserId);
            parameters.Add($"@p{i}_TenantId", entry.TenantId);
            parameters.Add($"@p{i}_CorrelationId", entry.CorrelationId);
        }

        return parameters;
    }
}