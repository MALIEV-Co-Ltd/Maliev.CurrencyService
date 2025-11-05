using Maliev.CurrencyService.Api.Models.Snapshots;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Service for managing exchange rate snapshots
/// </summary>
/// <remarks>
/// User Story 4: Provides batch ingestion with staging table for review,
/// promotion to production, and 90-day retention cleanup.
/// </remarks>
public interface ISnapshotService
{
    /// <summary>
    /// Imports a batch of snapshots, optionally promoting directly to production
    /// </summary>
    /// <param name="request">Batch request with snapshots</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch operation result</returns>
    /// <remarks>
    /// Process:
    /// 1. Validates all entries against Currency table
    /// 2. Stages snapshots in RateSnapshotStaging table
    /// 3. If AutoPromote=true, promotes to RateSnapshot and invalidates cache
    /// 4. Returns batch ID for tracking and promotion operations
    /// </remarks>
    Task<SnapshotBatchResponse> ImportBatchAsync(
        SnapshotBatchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Promotes staged snapshots to production
    /// </summary>
    /// <param name="batchId">Batch ID from import operation</param>
    /// <param name="source">Source/provider name for the snapshots</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if promotion succeeded</returns>
    /// <remarks>
    /// Moves snapshots from RateSnapshotStaging to RateSnapshot table,
    /// invalidates affected cache keys, and removes staging entries.
    /// </remarks>
    Task<bool> PromoteBatchAsync(
        string batchId,
        string? source = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up snapshots older than 90 days (FR-RET-001)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of snapshots deleted</returns>
    Task<int> CleanupOldSnapshotsAsync(CancellationToken cancellationToken = default);
}
