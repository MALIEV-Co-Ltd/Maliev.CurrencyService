using System.Text.Json.Serialization;

namespace Maliev.CurrencyService.Api.Models.Snapshots;

/// <summary>
/// Result of snapshot batch ingestion operation (FR-027)
/// </summary>
public class SnapshotIngestionResult
{
    /// <summary>
    /// Batch ID for tracking async processing
    /// </summary>
    [JsonPropertyName("batchId")]
    public required string BatchId { get; init; }

    /// <summary>
    /// Status: Queued, Processing, Completed, Failed
    /// </summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>
    /// Number of records in the batch
    /// </summary>
    [JsonPropertyName("recordCount")]
    public int RecordCount { get; init; }

    /// <summary>
    /// Timestamp when batch was submitted
    /// </summary>
    [JsonPropertyName("submittedAt")]
    public DateTime SubmittedAt { get; init; }
}
