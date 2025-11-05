using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.CurrencyService.Data.Models;

/// <summary>
/// Currency entity representing global currency metadata
/// </summary>
/// <remarks>
/// Stores ISO 4217 currency codes with formatting information.
/// Table: currencies (snake_case via EFCore.NamingConventions)
/// </remarks>
[Table("currencies")]
public class Currency
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("code")]
    [Required]
    [MaxLength(3)]
    public string Code { get; set; } = string.Empty;

    [Column("symbol")]
    [Required]
    [MaxLength(10)]
    public string Symbol { get; set; } = string.Empty;

    [Column("name")]
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("decimal_places")]
    public int DecimalPlaces { get; set; } = 2;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("is_primary")]
    public bool IsPrimary { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("version")]
    [Timestamp]
    public byte[] Version { get; set; } = Array.Empty<byte>();
}
