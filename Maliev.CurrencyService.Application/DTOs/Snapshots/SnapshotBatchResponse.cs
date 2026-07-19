namespace Maliev.CurrencyService.Application.DTOs.Snapshots;

/// <summary>
/// Response model for bulk snapshot import operation.
/// </summary>
public class SnapshotBatchResponse
{
    /// <summary>Gets or sets the batch operation identifier for tracking.</summary>
    public required string BatchId { get; set; }

    /// <summary>Gets or sets the date for which snapshots were imported.</summary>
    public required DateOnly SnapshotDate { get; set; }

    /// <summary>Gets or sets the source/provider name for the imported snapshots.</summary>
    public required string Source { get; set; }

    /// <summary>Gets or sets the number of snapshots successfully processed.</summary>
    public int SuccessCount { get; set; }

    /// <summary>Gets or sets the number of snapshots that failed validation or processing.</summary>
    public int FailureCount { get; set; }

    /// <summary>Gets or sets the status of the batch operation ("staged", "promoted", or "partial").</summary>
    public required string Status { get; set; }

    /// <summary>Gets or sets the validation errors by currency pair, if any.</summary>
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the batch was processed.</summary>
    public DateTime ProcessedAt { get; set; }
}
