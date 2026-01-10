using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Api.Models.Currencies;

/// <summary>
/// Request model for updating an existing currency
/// </summary>
/// <remarks>
/// User Story 5: Admin endpoint for modifying currency metadata.
/// Supports optimistic concurrency via Version field.
/// </remarks>
public class UpdateCurrencyRequest
{
    /// <summary>
    /// Currency name (e.g., "United States Dollar")
    /// </summary>
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }

    /// <summary>
    /// Currency symbol (e.g., "$", "€", "£")
    /// </summary>
    [StringLength(10, MinimumLength = 1)]
    public string? Symbol { get; set; }

    /// <summary>
    /// Number of decimal places (typically 2, but can be 0 or 3)
    /// </summary>
    [Range(0, 8)]
    public int? DecimalPlaces { get; set; }

    /// <summary>
    /// Whether the currency is active and available for use
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Current version for optimistic concurrency control (optional, ETag/If-Match preferred)
    /// </summary>
    public byte[]? Version { get; set; }
}
