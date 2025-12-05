using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Data.Models;

/// <summary>
/// Staged snapshot entity for batch validation and dry-run processing
/// </summary>
/// <remarks>
/// Temporary staging area for snapshot batch ingestion (FR-028, FR-029).
/// Lifecycle: Pending → Validated/Rejected → Committed/Deleted
/// Table and column names use snake_case via explicit [Table] and [Column] attributes.
/// Indexes are configured in StagedSnapshotConfiguration.
/// </remarks>
[Table("staged_snapshots")]
public class StagedSnapshot
{
    /// <summary>
    /// Unique identifier for the staged snapshot entry.
    /// </summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("batch_id")]
    public Guid BatchId { get; set; }

    [Column("from_currency")]
    [Required]
    [MaxLength(3)]
    public string FromCurrency { get; set; } = string.Empty;

    [Column("to_currency")]
    [Required]
    [MaxLength(3)]
    public string ToCurrency { get; set; } = string.Empty;

    [Column("rate")]
    [Precision(18, 6)]
    public decimal Rate { get; set; }

    [Column("snapshot_date")]
    public DateOnly SnapshotDate { get; set; }

    [Column("status")]
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [Column("validation_error")]
    public string? ValidationError { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
