using Maliev.CurrencyService.Application.DTOs.Snapshots;

namespace Maliev.CurrencyService.Application.Interfaces;

/// <summary>
/// Service interface for managing exchange rate snapshots.
/// </summary>
/// <remarks>
/// User Story 4: Provides batch ingestion with staging table for review,
/// promotion to production, and 90-day retention cleanup.
/// </remarks>
public interface ISnapshotService
{
    /// <summary>
    /// Imports a batch of snapshots, optionally promoting directly to production.
    /// </summary>
    /// <param name="request">Batch request with snapshots.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="SnapshotBatchResponse"/> detailing the import operation result.</returns>
    /// <remarks>
    /// Process:
    /// 1. Validates all entries against the Currency table.
    /// 2. Stages snapshots in RateSnapshotStaging table.
    /// 3. If AutoPromote=true, promotes to RateSnapshot and invalidates cache.
    /// 4. Returns batch ID for tracking and promotion operations.
    /// </remarks>
    Task<SnapshotBatchResponse> ImportBatchAsync(
        SnapshotBatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Promotes staged snapshots to production.
    /// </summary>
    /// <param name="batchId">The batch ID from the import operation.</param>
    /// <param name="source">Optional source/provider name for the snapshots.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if promotion succeeded, otherwise false.</returns>
    /// <remarks>
    /// Moves snapshots from RateSnapshotStaging to RateSnapshot table,
    /// invalidates affected cache keys, and removes staging entries.
    /// </remarks>
    Task<bool> PromoteBatchAsync(
        string batchId,
        string? source = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up snapshots older than 90 days (FR-RET-001).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of snapshots deleted.</returns>
    Task<int> CleanupOldSnapshotsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the audit log for a specific batch (FR-032).
    /// </summary>
    /// <param name="batchId">The batch identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The audit log, or null if not found.</returns>
    Task<SnapshotAuditLog?> GetBatchAuditAsync(string batchId, CancellationToken cancellationToken = default);
}
