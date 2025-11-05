namespace Maliev.CurrencyService.Api.Models.Snapshots;

/// <summary>
/// Response model for bulk snapshot import operation
/// </summary>
public class SnapshotBatchResponse
{
    /// <summary>
    /// Batch operation ID for tracking
    /// </summary>
    public required string BatchId { get; set; }

    /// <summary>
    /// Date for which snapshots were imported
    /// </summary>
    public required DateOnly SnapshotDate { get; set; }

    /// <summary>
    /// Source/provider name
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    /// Number of snapshots successfully processed
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of snapshots that failed validation or processing
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Status: "staged", "promoted", or "partial"
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Validation errors by currency pair (if any)
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// Timestamp when batch was processed
    /// </summary>
    public DateTime ProcessedAt { get; set; }
}
