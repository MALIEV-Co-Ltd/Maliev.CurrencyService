namespace Maliev.CurrencyService.Api.Models.Currencies;

/// <summary>
/// Request model for creating a new currency
/// </summary>
/// <remarks>
/// User Story 5: Admin endpoint for adding new currencies to the system.
/// Requires JWT authentication with admin role.
/// </remarks>
public class CreateCurrencyRequest
{
    /// <summary>
    /// ISO 4217 currency code (3 uppercase letters)
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Currency name (e.g., "United States Dollar")
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Currency symbol (e.g., "$", "€", "£")
    /// </summary>
    public required string Symbol { get; set; }

    /// <summary>
    /// Number of decimal places (typically 2, but can be 0 or 3)
    /// </summary>
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>
    /// Whether the currency is active and available for use
    /// </summary>
    public bool IsActive { get; set; } = true;
}
