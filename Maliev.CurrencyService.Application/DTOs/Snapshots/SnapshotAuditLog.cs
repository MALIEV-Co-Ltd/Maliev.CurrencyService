namespace Maliev.CurrencyService.Application.DTOs.Snapshots;

/// <summary>
/// Audit log entry for a snapshot batch operation.
/// </summary>
public record SnapshotAuditLog
{
    /// <summary>Gets or sets the batch identifier.</summary>
    public string BatchId { get; init; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp of the ingestion event.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Gets or sets the number of records in the batch.</summary>
    public int RecordCount { get; init; }

    /// <summary>Gets or sets the source of the snapshot data.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Gets or sets the identifier of the user who submitted the batch.</summary>
    public string SubmittedBy { get; init; } = string.Empty;
}
