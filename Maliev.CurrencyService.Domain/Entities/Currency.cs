using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.CurrencyService.Domain.Entities;

/// <summary>
/// Currency entity representing global currency metadata.
/// </summary>
/// <remarks>
/// Stores ISO 4217 currency codes with formatting information.
/// Table and column names use snake_case via explicit [Table] and [Column] attributes.
/// </remarks>
[Table("currencies")]
public class Currency
{
    /// <summary>Gets or sets the unique identifier.</summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the ISO 4217 currency code (3 letters, uppercase).</summary>
    [Column("code")]
    [Required]
    [MaxLength(3)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the currency symbol (e.g., "$", "€").</summary>
    [Column("symbol")]
    [Required]
    [MaxLength(10)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the full currency name (e.g., "US Dollar").</summary>
    [Column("name")]
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of decimal places used for this currency.</summary>
    [Column("decimal_places")]
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>Gets or sets a value indicating whether the currency is active.</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether this is the primary currency for its region.</summary>
    [Column("is_primary")]
    public bool IsPrimary { get; set; } = false;

    /// <summary>Gets or sets the UTC timestamp when this record was created.</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this record was last updated.</summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>Gets or sets the concurrency token for optimistic concurrency control.</summary>
    [Column("version")]
    public byte[] Version { get; set; } = new byte[8];
}
