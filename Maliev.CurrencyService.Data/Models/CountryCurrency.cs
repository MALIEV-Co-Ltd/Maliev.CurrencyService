using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Data.Models;

/// <summary>
/// Country-to-currency mapping entity
/// </summary>
/// <remarks>
/// Maps ISO 3166-1 country codes to their primary currency.
/// Supports both alpha-2 (TH, US) and alpha-3 (THA, USA) codes.
/// Table: country_currencies (snake_case via EFCore.NamingConventions)
/// </remarks>
[Table("country_currencies")]
[Index(nameof(CountryIso2), nameof(CurrencyCode), IsUnique = true, Name = "ix_country_iso2_currency")]
[Index(nameof(CountryIso3), nameof(CurrencyCode), IsUnique = true, Name = "ix_country_iso3_currency")]
public class CountryCurrency
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("country_iso2")]
    [Required]
    [MaxLength(2)]
    public string CountryIso2 { get; set; } = string.Empty;

    [Column("country_iso3")]
    [Required]
    [MaxLength(3)]
    public string CountryIso3 { get; set; } = string.Empty;

    [Column("currency_code")]
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    [Column("is_primary")]
    public bool IsPrimary { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation property (not mapped to DTO)
    public Currency? Currency { get; set; }
}
