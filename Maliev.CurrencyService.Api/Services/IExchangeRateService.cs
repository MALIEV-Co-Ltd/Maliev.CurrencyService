using Maliev.CurrencyService.Api.Models;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Defines the interface for a service that provides exchange rate information and currency conversion.
/// </summary>
public interface IExchangeRateService
{
    /// <summary>
    /// Gets the exchange rate between two currencies.
    /// </summary>
    /// <param name="fromCurrency">The source currency code (ISO 4217).</param>
    /// <param name="toCurrency">The target currency code (ISO 4217).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="ExchangeRateDto"/> if found, otherwise null.</returns>
    Task<ExchangeRateDto?> GetExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets multiple exchange rates from a base currency to several target currencies.
    /// </summary>
    /// <param name="baseCurrency">The base currency code (ISO 4217).</param>
    /// <param name="targetCurrencies">A collection of target currency codes (ISO 4217).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A dictionary where the key is the target currency code and the value is the <see cref="ExchangeRateDto"/>.</returns>
    Task<Dictionary<string, ExchangeRateDto>> GetMultipleRatesAsync(string baseCurrency, IEnumerable<string> targetCurrencies, CancellationToken cancellationToken = default);
    /// <summary>
    /// Converts an amount from one currency to another.
    /// </summary>
    /// <param name="request">The <see cref="ConvertCurrencyRequest"/> containing the conversion details.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="ConvertCurrencyResponse"/> if successful, otherwise null.</returns>
    Task<ConvertCurrencyResponse?> ConvertCurrencyAsync(ConvertCurrencyRequest request, CancellationToken cancellationToken = default);
    
    // For testing purposes
    /// <summary>
    /// Retrieves current provider metrics.
    /// </summary>
    /// <returns>A dictionary of provider names to their metrics.</returns>
    Dictionary<string, ProviderMetrics> GetProviderMetrics();
}
