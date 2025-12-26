using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models;

/// <summary>
/// Represents a request to get an exchange rate.
/// </summary>
public class GetExchangeRateRequest
{
    /// <summary>
    /// The source currency code (ISO 4217, e.g., "USD").
    /// </summary>
    [Required]
    [StringLength(3, MinimumLength = 3)]
    public required string From { get; set; }

    /// <summary>
    /// The target currency code (ISO 4217, e.g., "EUR").
    /// </summary>
    [Required]
    public required string To { get; set; }
}
