namespace Maliev.CurrencyService.Api.Models.Snapshots;

/// <summary>
/// Request model for bulk snapshot import
/// </summary>
/// <remarks>
/// User Story 4: Supports batch ingestion of up to 5000 snapshots per request.
/// Validates date range (max 90 days per FR-RET-001), currency codes (ISO 4217),
/// and rate precision (6 decimal places per FR-SC-012).
/// </remarks>
public class SnapshotBatchRequest
{
    /// <summary>
    /// Date for which snapshots are being imported (YYYY-MM-DD)
    /// </summary>
    public required DateOnly SnapshotDate { get; set; }

    /// <summary>
    /// Source/provider name for these snapshots (e.g., "ECB", "Manual")
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    /// Collection of exchange rate snapshots to import
    /// </summary>
    public required List<SnapshotEntry> Snapshots { get; set; }

    /// <summary>
    /// If true, promotes directly to production. If false, stages for review.
    /// </summary>
    public bool AutoPromote { get; set; } = false;
}

/// <summary>
/// Individual snapshot entry in batch
/// </summary>
public class SnapshotEntry
{
    /// <summary>
    /// Source currency code (ISO 4217)
    /// </summary>
    public required string From { get; set; }

    /// <summary>
    /// Target currency code (ISO 4217)
    /// </summary>
    public required string To { get; set; }

    /// <summary>
    /// Exchange rate (6 decimal precision per FR-SC-012)
    /// </summary>
    public required decimal Rate { get; set; }
}
