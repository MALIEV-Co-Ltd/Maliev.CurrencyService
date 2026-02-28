using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Application.DTOs.Currencies;

/// <summary>
/// Request model for creating a new currency.
/// </summary>
/// <remarks>
/// User Story 5: Admin endpoint for adding new currencies to the system.
/// Requires JWT authentication with admin role.
/// </remarks>
public class CreateCurrencyRequest
{
    /// <summary>Gets or sets the ISO 4217 currency code (3 uppercase letters).</summary>
    [Required]
    [StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Code must be exactly 3 uppercase letters.")]
    public required string Code { get; set; }

    /// <summary>Gets or sets the full currency name (e.g., "United States Dollar").</summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Name { get; set; }

    /// <summary>Gets or sets the currency symbol (e.g., "$", "€", "£").</summary>
    [Required]
    [StringLength(10, MinimumLength = 1)]
    public required string Symbol { get; set; }

    /// <summary>Gets or sets the number of decimal places (typically 2, but can be 0 or 3).</summary>
    [Range(0, 8)]
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>Gets or sets a value indicating whether the currency is active and available for use.</summary>
    public bool IsActive { get; set; } = true;
}
