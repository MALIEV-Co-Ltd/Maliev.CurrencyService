using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.CurrencyService.Domain.Entities;

/// <summary>
/// Exchange rate entity for caching live rates from external providers.
/// </summary>
/// <remarks>
/// Stores cached exchange rates with provider tracking and expiration.
/// Supports transitive rates calculated via intermediary currencies.
/// Table and column names use snake_case via explicit [Table] and [Column] attributes.
/// Indexes are configured in ExchangeRateConfiguration.
/// </remarks>
[Table("exchange_rates")]
public class ExchangeRate
{
    /// <summary>Gets or sets the unique identifier for the exchange rate entry.</summary>
    [Column("id")]
    public Guid Id { get; set; }

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
    public decimal Rate { get; set; }

    /// <summary>Gets or sets the name of the provider that supplied this rate.</summary>
    [Column("provider")]
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether this is a transitively calculated rate.</summary>
    [Column("is_transitive")]
    public bool IsTransitive { get; set; } = false;

    /// <summary>Gets or sets the intermediate currency code used for transitive rate calculation.</summary>
    [Column("intermediate_currency")]
    [MaxLength(3)]
    public string? IntermediateCurrency { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this rate was fetched from the provider.</summary>
    [Column("fetched_at")]
    public DateTime FetchedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this cached rate expires.</summary>
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this record was created.</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this record was last updated.</summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
