using System.Threading.Channels;

namespace TechHub.Service.Infrastructure.Logging;

/// <summary>
/// Singleton in-memory conduit between the hot request path and the
/// background <see cref="LogWriterService"/>.
///
/// Uses a bounded channel (capacity 10,000) with
/// <see cref="BoundedChannelFullMode.DropOldest"/> so a database outage that
/// floods errors can never grow memory unbounded — the oldest diagnostics are
/// dropped and the newest kept.
///
/// <see cref="Enqueue"/> is O(1), never blocks and never throws; call it
/// freely from any request thread.
/// </summary>
public sealed class LogChannel
{
    public const int Capacity = 10_000;

    private readonly Channel<LogEntry> _channel;

    public LogChannel()
    {
        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,   // only LogWriterService drains it
            SingleWriter = false   // any number of request threads enqueue
        });
    }

    /// <summary>
    /// Queues an entry via <c>TryWrite</c>. Never blocks, never throws.
    /// If the channel is full the oldest entry is dropped to make room.
    /// </summary>
    public void Enqueue(LogEntry entry)
    {
        if (entry is null)
            return;

        _channel.Writer.TryWrite(entry);
    }

    /// <summary>
    /// Number of entries waiting in the channel. Used by the writer to decide
    /// whether it is safe to flush a low-count batch. Bounded channels always
    /// support counting.
    /// </summary>
    public int Count => _channel.Reader.CanCount ? _channel.Reader.Count : 0;

    /// <summary>Async enumeration over queued entries, consumed by the background writer.</summary>
    public IAsyncEnumerable<LogEntry> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>Best-effort pull of every entry still buffered (shutdown drain).</summary>
    public void DrainTo(IList<LogEntry> target)
    {
        while (_channel.Reader.TryRead(out var entry))
            target.Add(entry);
    }
}