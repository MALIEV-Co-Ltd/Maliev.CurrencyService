namespace Maliev.CurrencyService.Application.DTOs.Snapshots;

/// <summary>
/// Request model for bulk snapshot import.
/// </summary>
/// <remarks>
/// User Story 4: Supports batch ingestion of up to 5000 snapshots per request.
/// Validates date range (max 90 days per FR-RET-001), currency codes (ISO 4217),
/// and rate precision (6 decimal places per FR-SC-012).
/// </remarks>
public class SnapshotBatchRequest
{
    /// <summary>Gets or sets the date for which snapshots are being imported.</summary>
    public required DateOnly SnapshotDate { get; set; }

    /// <summary>Gets or sets the source/provider name for these snapshots (e.g., "ECB", "Manual").</summary>
    public required string Source { get; set; }

    /// <summary>Gets or sets the collection of exchange rate snapshots to import.</summary>
    public required List<SnapshotEntry> Snapshots { get; set; }

    /// <summary>Gets or sets a value indicating whether to promote directly to production (true) or stage for review (false).</summary>
    public bool AutoPromote { get; set; } = false;
}

/// <summary>
/// Individual snapshot entry in a batch.
/// </summary>
public class SnapshotEntry
{
    /// <summary>Gets or sets the source currency code (ISO 4217).</summary>
    public required string From { get; set; }

    /// <summary>Gets or sets the target currency code (ISO 4217).</summary>
    public required string To { get; set; }

    /// <summary>Gets or sets the exchange rate with 6 decimal precision per FR-SC-012.</summary>
    public required decimal Rate { get; set; }
}
