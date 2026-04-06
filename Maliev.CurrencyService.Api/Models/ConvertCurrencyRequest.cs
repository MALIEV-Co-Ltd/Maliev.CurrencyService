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
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "'from' currency code must be 3 uppercase letters.")]
    public required string From { get; init; }

    /// <summary>
    /// The target currency code (ISO 4217, e.g., "EUR").
    /// </summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$", ErrorMessage = "'to' currency code must be 3 uppercase letters.")]
    public required string To { get; init; }

    /// <summary>
    /// The amount to convert.
    /// </summary>
    [Required]
    [Range(typeof(decimal), "0.0000000000000000000000000001", "79228162514264337593543950335", ErrorMessage = "'amount' must be greater than 0.")]
    public decimal Amount { get; init; }
}
