using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Data.Models;

/// <summary>
/// Exchange rate entity for caching live rates
/// </summary>
/// <remarks>
/// Stores cached exchange rates with provider tracking and expiration.
/// Supports transitive rates calculated via intermediary currencies.
/// Table: exchange_rates (snake_case via EFCore.NamingConventions)
/// </remarks>
[Table("exchange_rates")]
[Index(nameof(FromCurrency), nameof(ToCurrency), nameof(FetchedAt), IsUnique = true)]
[Index(nameof(ExpiresAt))]
public class ExchangeRate
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

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

    [Column("provider")]
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    [Column("is_transitive")]
    public bool IsTransitive { get; set; } = false;

    [Column("intermediate_currency")]
    [MaxLength(3)]
    public string? IntermediateCurrency { get; set; }

    [Column("fetched_at")]
    public DateTime FetchedAt { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
