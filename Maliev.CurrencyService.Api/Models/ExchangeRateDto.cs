namespace Maliev.CurrencyService.Api.Models;

/// <summary>
/// Represents an exchange rate data transfer object.
/// </summary>
public class ExchangeRateDto
{
    /// <summary>
    /// The source currency code (ISO 4217, e.g., "USD").
    /// </summary>
    public required string FromCurrency { get; set; }
    /// <summary>
    /// The target currency code (ISO 4217, e.g., "EUR").
    /// </summary>
    public required string ToCurrency { get; set; }
    /// <summary>
    /// The exchange rate from the source currency to the target currency.
    /// </summary>
    public decimal Rate { get; set; }
    /// <summary>
    /// The timestamp when the exchange rate was fetched.
    /// </summary>
    public DateTime FetchedAt { get; set; }
    /// <summary>
    /// The source from which the exchange rate was obtained (e.g., "Frankfurter", "Fawazahmed").
    /// </summary>
    public required string Source { get; set; }
}
