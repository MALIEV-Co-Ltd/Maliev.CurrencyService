using System.ComponentModel.DataAnnotations;

namespace Maliev.CurrencyService.Application.DTOs.Currencies;

/// <summary>
/// Request model for updating an existing currency.
/// </summary>
/// <remarks>
/// User Story 5: Admin endpoint for modifying currency metadata.
/// Supports optimistic concurrency via Version field.
/// </remarks>
public class UpdateCurrencyRequest
{
    /// <summary>Gets or sets the currency name (e.g., "United States Dollar").</summary>
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }

    /// <summary>Gets or sets the currency symbol (e.g., "$", "€", "£").</summary>
    [StringLength(10, MinimumLength = 1)]
    public string? Symbol { get; set; }

    /// <summary>Gets or sets the number of decimal places (typically 2, but can be 0 or 3).</summary>
    [Range(0, 8)]
    public int? DecimalPlaces { get; set; }

    /// <summary>Gets or sets the active status of the currency.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Gets or sets the current version for optimistic concurrency control (optional; ETag/If-Match is preferred).</summary>
    public byte[]? Version { get; set; }
}
