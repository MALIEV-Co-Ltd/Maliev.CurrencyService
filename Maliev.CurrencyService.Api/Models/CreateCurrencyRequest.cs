using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models;

/// <summary>
/// Represents a request to create a new currency.
/// </summary>
public class CreateCurrencyRequest
{
    /// <summary>
    /// The 3-letter ISO 4217 code for the currency (e.g., "USD").
    /// </summary>
    [Required]
    [StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "ShortName must be exactly 3 uppercase letters")]
    public required string ShortName { get; set; }

    /// <summary>
    /// The full name of the currency (e.g., "United States Dollar").
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string LongName { get; set; }
}
