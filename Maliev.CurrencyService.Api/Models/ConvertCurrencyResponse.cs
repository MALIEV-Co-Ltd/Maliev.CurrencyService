namespace Maliev.CurrencyService.Api.Models;

/// <summary>
/// Represents the response from a currency conversion request.
/// </summary>
public class ConvertCurrencyResponse
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
    /// The original amount in the source currency.
    /// </summary>
    public decimal OriginalAmount { get; set; }
    /// <summary>
    /// The converted amount in the target currency.
    /// </summary>
    public decimal ConvertedAmount { get; set; }
    /// <summary>
    /// The exchange rate used for the conversion.
    /// </summary>
    public decimal ExchangeRate { get; set; }
    /// <summary>
    /// The timestamp when the exchange rate was valid.
    /// </summary>
    public DateTime RateTimestamp { get; set; }
    /// <summary>
    /// The source of the exchange rate (e.g., "Frankfurter", "Fawazahmed").
    /// </summary>
    public required string Source { get; set; }
}
