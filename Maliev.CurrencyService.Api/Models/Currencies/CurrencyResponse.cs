using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models.Currencies;

/// <summary>
/// Currency metadata response DTO
/// </summary>
/// <remarks>
/// Per contracts/openapi.yaml: Provides currency information for client applications.
/// </remarks>
public class CurrencyResponse
{
    /// <summary>
    /// Unique identifier for the currency
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// ISO 4217 currency code (3 letters)
    /// </summary>
    [Required]
    [RegularExpression("^[A-Z]{3}$")]
    public required string Code { get; init; }

    /// <summary>
    /// Currency symbol (e.g., "$", "€", "฿")
    /// </summary>
    [Required]
    [MaxLength(10)]
    public required string Symbol { get; init; }

    /// <summary>
    /// Full currency name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string Name { get; init; }

    /// <summary>
    /// Number of decimal places for this currency (0-6)
    /// </summary>
    [Range(0, 6)]
    public required int DecimalPlaces { get; init; }

    /// <summary>
    /// Whether the currency is currently active
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Whether this is the primary currency (THB for Maliev)
    /// </summary>
    public required bool IsPrimary { get; init; }

    /// <summary>
    /// Timestamp when the currency was created
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the currency was last updated
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
