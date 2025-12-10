namespace Maliev.CurrencyService.Api.Models.Snapshots;

/// <summary>
/// Audit log entry for a snapshot batch operation
/// </summary>
public record SnapshotAuditLog
{
    /// <summary>
    /// The batch identifier
    /// </summary>
    public string BatchId { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp of ingestion
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Number of records in the batch
    /// </summary>
    public int RecordCount { get; init; }

    /// <summary>
    /// Source of the snapshot data
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// User who submitted the batch (if applicable)
    /// </summary>
    public string SubmittedBy { get; init; } = string.Empty;
}
