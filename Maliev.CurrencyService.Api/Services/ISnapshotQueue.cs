namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Interface for a queue to handle snapshot batch processing backgrounds jobs
/// </summary>
public interface ISnapshotQueue
{
    /// <summary>
    /// Queues a batch ID for background processing
    /// </summary>
    /// <param name="batchId">The batch ID to queue</param>
    /// <returns>True if queued successfully</returns>
    ValueTask QueueBackgroundWorkItemAsync(string batchId);

    /// <summary>
    /// Dequeues a batch ID for processing
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The dequeued batch ID</returns>
    ValueTask<string> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// updates the status of a batch
    /// </summary>
    void UpdateStatus(string batchId, string status, string? error = null);

    /// <summary>
    /// Gets the current status of a batch
    /// </summary>
    (string Status, string? Error) GetStatus(string batchId);
}
