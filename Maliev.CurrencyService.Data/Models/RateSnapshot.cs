using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Data.Models;

/// <summary>
/// Historical exchange rate snapshot entity
/// </summary>
/// <remarks>
/// Stores point-in-time exchange rates for compliance and reporting (FR-009).
/// Snapshots are grouped by batch_id for bulk import operations.
/// Table: rate_snapshots (snake_case via EFCore.NamingConventions)
/// </remarks>
[Table("rate_snapshots")]
[Index(nameof(FromCurrency), nameof(ToCurrency), nameof(SnapshotDate), IsUnique = true)]
[Index(nameof(BatchId))]
[Index(nameof(SnapshotDate))]
public class RateSnapshot
{
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

    [Column("source")]
    [MaxLength(100)]
    public string? Source { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
