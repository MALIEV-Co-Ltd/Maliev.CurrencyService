using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models;

/// <summary>
/// Represents a request to convert currency.
/// </summary>
public class ConvertCurrencyRequest
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
    [StringLength(3, MinimumLength = 3)]
    public required string To { get; set; }

    /// <summary>
    /// The amount to convert.
    /// </summary>
    [Required]
    [Range(0.01, 1000000000000.00, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }
}
