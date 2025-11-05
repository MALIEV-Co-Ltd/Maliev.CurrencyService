using Maliev.CurrencyService.Data.Models;

namespace Maliev.CurrencyService.Api.Services.External;

/// <summary>
/// Interface for external exchange rate providers
/// </summary>
/// <remarks>
/// Per research.md decision 1: Supports provider chain pattern with fallback
/// (Fawazahmed primary, Frankfurter fallback).
/// </remarks>
public interface IExchangeRateProvider
{
    /// <summary>
    /// Gets the provider name for tracking and logging
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the exchange rate for a currency pair from the external provider
    /// </summary>
    /// <param name="fromCurrency">Source currency code (e.g., "USD")</param>
    /// <param name="toCurrency">Target currency code (e.g., "THB")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>
    /// ExchangeRate if available from provider, or null if:
    /// - Currency pair not supported by provider
    /// - Provider returns error
    /// - Network/timeout issues
    /// </returns>
    Task<ExchangeRate?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this provider supports a specific currency pair
    /// </summary>
    /// <param name="fromCurrency">Source currency code</param>
    /// <param name="toCurrency">Target currency code</param>
    /// <returns>True if provider can provide rate for this pair</returns>
    bool SupportsPair(string fromCurrency, string toCurrency);

    /// <summary>
    /// Gets all available currencies supported by this provider
    /// </summary>
    /// <returns>Set of currency codes supported by the provider</returns>
    Task<IReadOnlySet<string>> GetSupportedCurrenciesAsync(CancellationToken cancellationToken = default);
}
