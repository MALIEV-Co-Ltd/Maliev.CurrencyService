using Maliev.CurrencyService.Domain.Entities;

namespace Maliev.CurrencyService.Application.Interfaces;

/// <summary>
/// Interface for external exchange rate data providers.
/// </summary>
/// <remarks>
/// Per research.md decision 1: Supports provider chain pattern with fallback
/// (Fawazahmed primary, Frankfurter fallback).
/// </remarks>
public interface IExchangeRateProvider
{
    /// <summary>
    /// Gets the unique name of this provider for tracking and logging.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the exchange rate for a currency pair from this external provider.
    /// </summary>
    /// <param name="fromCurrency">Source currency code (e.g., "USD").</param>
    /// <param name="toCurrency">Target currency code (e.g., "THB").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="ExchangeRate"/> if available from the provider, or null if:
    /// the currency pair is not supported, the provider returns an error, or network/timeout issues occur.
    /// </returns>
    Task<ExchangeRate?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether this provider supports a specific currency pair.
    /// </summary>
    /// <param name="fromCurrency">Source currency code.</param>
    /// <param name="toCurrency">Target currency code.</param>
    /// <returns>True if this provider can supply a rate for this pair.</returns>
    bool SupportsPair(string fromCurrency, string toCurrency);

    /// <summary>
    /// Gets all currency codes supported by this provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only set of supported currency codes.</returns>
    Task<IReadOnlySet<string>> GetSupportedCurrenciesAsync(CancellationToken cancellationToken = default);
}
