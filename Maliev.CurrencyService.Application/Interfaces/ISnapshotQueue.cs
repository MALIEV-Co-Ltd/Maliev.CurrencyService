namespace Maliev.CurrencyService.Application.Interfaces;

/// <summary>
/// Interface for the in-memory queue that handles snapshot batch background processing.
/// </summary>
public interface ISnapshotQueue
{
    /// <summary>
    /// Queues a batch ID for background processing.
    /// </summary>
    /// <param name="batchId">The batch identifier to queue.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask QueueBackgroundWorkItemAsync(string batchId);

    /// <summary>
    /// Dequeues the next batch ID for processing, waiting until one is available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort the wait.</param>
    /// <returns>The dequeued batch identifier.</returns>
    ValueTask<string> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the processing status of a batch.
    /// </summary>
    /// <param name="batchId">The batch identifier.</param>
    /// <param name="status">The new status string.</param>
    /// <param name="error">Optional error message if the batch failed.</param>
    void UpdateStatus(string batchId, string status, string? error = null);

    /// <summary>
    /// Gets the current processing status and error message for a batch.
    /// </summary>
    /// <param name="batchId">The batch identifier.</param>
    /// <returns>A tuple of (status, error message).</returns>
    (string Status, string? Error) GetStatus(string batchId);
}
