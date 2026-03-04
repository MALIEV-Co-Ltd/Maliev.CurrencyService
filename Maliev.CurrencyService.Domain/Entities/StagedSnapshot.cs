using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.CurrencyService.Domain.Entities;

/// <summary>
/// Staged snapshot entity for batch validation and dry-run processing.
/// </summary>
/// <remarks>
/// Temporary staging area for snapshot batch ingestion (FR-028, FR-029).
/// Lifecycle: Pending → Validated/Rejected → Committed/Deleted.
/// Table and column names use snake_case via explicit [Table] and [Column] attributes.
/// Indexes are configured in StagedSnapshotConfiguration.
/// </remarks>
[Table("staged_snapshots")]
public class StagedSnapshot
{
    /// <summary>Gets or sets the unique identifier for the staged snapshot entry.</summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the batch identifier this staged snapshot belongs to.</summary>
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

    /// <summary>Gets or sets the exchange rate value.</summary>
    [Column("rate")]
    [Precision(18, 6)]
    public decimal Rate { get; set; }

    /// <summary>Gets or sets the date for which this snapshot applies.</summary>
    [Column("snapshot_date")]
    public DateOnly SnapshotDate { get; set; }

    /// <summary>Gets or sets the current validation/processing status.</summary>
    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    /// <summary>Gets or sets the validation error message, if any.</summary>
    [Column("validation_error")]
    public string? ValidationError { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this record was created.</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the user who submitted this snapshot.</summary>
    [Column("submitted_by")]
    [MaxLength(256)]
    public string? SubmittedBy { get; set; }
}
