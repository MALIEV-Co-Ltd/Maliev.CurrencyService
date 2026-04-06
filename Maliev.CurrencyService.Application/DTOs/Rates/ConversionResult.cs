namespace Maliev.CurrencyService.Application.DTOs.Rates;

/// <summary>
/// Represents the result of a currency conversion.
/// </summary>
public class ConversionResult
{
    /// <summary>Gets or sets the source currency code (ISO 4217).</summary>
    public required string FromCurrency { get; init; }

    /// <summary>Gets or sets the target currency code (ISO 4217).</summary>
    public required string ToCurrency { get; init; }

    /// <summary>Gets or sets the original amount in the source currency.</summary>
    public decimal OriginalAmount { get; init; }

    /// <summary>Gets or sets the converted amount in the target currency.</summary>
    public decimal ConvertedAmount { get; init; }

    /// <summary>Gets or sets the exchange rate used for the conversion.</summary>
    public decimal ExchangeRate { get; init; }

    /// <summary>Gets or sets the UTC timestamp when this rate was fetched.</summary>
    public DateTime RateTimestamp { get; init; }

    /// <summary>Gets or sets the data source (e.g., "Fawazahmed", "Frankfurter").</summary>
    public required string Source { get; init; }

    /// <summary>Gets or sets a value indicating whether the rate is computed via an intermediary currency.</summary>
    public bool IsTransitive { get; init; }

    /// <summary>Gets or sets the intermediary currency code if transitive.</summary>
    public string? IntermediateCurrency { get; init; }
}
