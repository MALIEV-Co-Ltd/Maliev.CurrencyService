using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.CurrencyService.Domain.Entities;

/// <summary>
/// Country-to-currency mapping entity.
/// </summary>
/// <remarks>
/// Maps ISO 3166-1 country codes to their primary currency.
/// Supports both alpha-2 (TH, US) and alpha-3 (THA, USA) codes.
/// Table and column names use snake_case via explicit [Table] and [Column] attributes.
/// Indexes are configured in CountryCurrencyConfiguration.
/// </remarks>
[Table("country_currencies")]
public class CountryCurrency
{
    /// <summary>Gets or sets the unique identifier for the country-currency mapping.</summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the ISO 3166-1 alpha-2 country code (e.g., "TH").</summary>
    [Column("country_iso2")]
    [Required]
    [MaxLength(2)]
    public string CountryIso2 { get; set; } = string.Empty;

    /// <summary>Gets or sets the ISO 3166-1 alpha-3 country code (e.g., "THA").</summary>
    [Column("country_iso3")]
    [Required]
    [MaxLength(3)]
    public string CountryIso3 { get; set; } = string.Empty;

    /// <summary>Gets or sets the ISO 4217 currency code for this mapping.</summary>
    [Column("currency_code")]
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this is the primary currency for the country.</summary>
    [Column("is_primary")]
    public bool IsPrimary { get; set; } = true;

    /// <summary>Gets or sets the UTC timestamp when this record was created.</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the navigation property to the related currency entity.</summary>
    public Currency? Currency { get; set; }
}
