using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// In-memory queue for snapshot batch processing using System.Threading.Channels
/// </summary>
public class SnapshotQueue : ISnapshotQueue
{
    private readonly Channel<string> _queue;
    private readonly ConcurrentDictionary<string, (string Status, string? Error)> _statuses = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotQueue"/> class.
    /// </summary>
    public SnapshotQueue()
    {
        // Unbounded channel - in a real production system with massive load, 
        // we might want a bounded channel, but for this use case unbounded is fine
        // as batch ingestion volume is expected to be manageable.
        var options = new UnboundedChannelOptions
        {
            SingleReader = true, // We have one background service reading
            SingleWriter = false // Multiple concurrent requests can write
        };
        _queue = Channel.CreateUnbounded<string>(options);
    }

    /// <inheritdoc />
    public async ValueTask QueueBackgroundWorkItemAsync(string batchId)
    {
        if (string.IsNullOrEmpty(batchId))
        {
            throw new ArgumentNullException(nameof(batchId));
        }

        _statuses[batchId] = ("Queued", null);
        await _queue.Writer.WriteAsync(batchId);
    }

    /// <inheritdoc />
    public async ValueTask<string> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void UpdateStatus(string batchId, string status, string? error = null)
    {
        _statuses[batchId] = (status, error);
    }

    /// <inheritdoc />
    public (string Status, string? Error) GetStatus(string batchId)
    {
        return _statuses.TryGetValue(batchId, out var status)
            ? status
            : ("NotFound", null);
    }
}
