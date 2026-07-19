using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.CurrencyService.Domain.Entities;

/// <summary>
/// Historical exchange rate snapshot entity.
/// </summary>
/// <remarks>
/// Stores point-in-time exchange rates for compliance and reporting (FR-009).
/// Snapshots are grouped by batch_id for bulk import operations.
/// Table and column names use snake_case via explicit [Table] and [Column] attributes.
/// Indexes are configured in RateSnapshotConfiguration.
/// </remarks>
[Table("rate_snapshots")]
public class RateSnapshot
{
    /// <summary>Gets or sets the unique identifier for the rate snapshot entry.</summary>
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the batch identifier grouping snapshots from the same import.</summary>
    [Column("batch_id")]
    public Guid BatchId { get; set; }

    /// <summary>Gets or sets the source currency code (ISO 4217).</summary>
    [Column("from_currency")]
    [Required]
    [MaxLength(3)]
    public string FromCurrency { get; set; } = string.Empty;

    /// <summary>Gets or sets the target currency code (ISO 4217).</summary>
    [Column("to_currency")]
    [Required]
    [MaxLength(3)]
    public string ToCurrency { get; set; } = string.Empty;

    /// <summary>Gets or sets the exchange rate value for this snapshot.</summary>
    [Column("rate")]
    public decimal Rate { get; set; }

    /// <summary>Gets or sets the date for which this snapshot is valid.</summary>
    [Column("snapshot_date")]
    public DateOnly SnapshotDate { get; set; }

    /// <summary>Gets or sets the data source or provider name for this snapshot.</summary>
    [Column("source")]
    [MaxLength(100)]
    public string? Source { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this record was created.</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the user who submitted this snapshot.</summary>
    [Column("submitted_by")]
    [MaxLength(256)]
    public string? SubmittedBy { get; set; }
}
