using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Application.DTOs.Currencies;

/// <summary>
/// Currency metadata response DTO.
/// </summary>
/// <remarks>
/// Per contracts/openapi.yaml: Provides currency information for client applications.
/// </remarks>
public class CurrencyResponse
{
    /// <summary>Gets or sets the unique identifier for the currency.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets or sets the ISO 4217 currency code (3 uppercase letters).</summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$")]
    public required string Code { get; init; }

    /// <summary>Gets or sets the currency symbol (e.g., "$", "€", "฿").</summary>
    [Required]
    [MaxLength(10)]
    public required string Symbol { get; init; }

    /// <summary>Gets or sets the full currency name.</summary>
    [Required]
    [MaxLength(100)]
    public required string Name { get; init; }

    /// <summary>Gets or sets the number of decimal places for this currency (0-6).</summary>
    [Range(0, 6)]
    public required int DecimalPlaces { get; init; }

    /// <summary>Gets or sets a value indicating whether the currency is currently active.</summary>
    public required bool IsActive { get; init; }

    /// <summary>Gets or sets a value indicating whether this is a primary currency (e.g., THB for Maliev).</summary>
    public required bool IsPrimary { get; init; }

    /// <summary>Gets or sets the UTC timestamp when the currency was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Gets or sets the UTC timestamp when the currency was last updated.</summary>
    public DateTime UpdatedAt { get; init; }
}
