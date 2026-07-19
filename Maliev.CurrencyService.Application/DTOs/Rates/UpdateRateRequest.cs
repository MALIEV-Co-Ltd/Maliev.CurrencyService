namespace Maliev.CurrencyService.Application.DTOs.Rates;

/// <summary>
/// Request to update a single exchange rate.
/// </summary>
public class UpdateRateRequest
{
    /// <summary>Gets or sets the source currency code (ISO 4217).</summary>
    public required string From { get; set; }

    /// <summary>Gets or sets the target currency code (ISO 4217).</summary>
    public required string To { get; set; }

    /// <summary>Gets or sets the new exchange rate value.</summary>
    public required decimal Rate { get; set; }
}
