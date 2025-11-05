using Maliev.CurrencyService.Api.Models.Rates;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Exchange rate service interface
/// </summary>
/// <remarks>
/// User Story 2: Provides live exchange rate retrieval with provider fallback and caching.
/// User Story 3: Extends with snapshot mode for historical rate queries.
/// </remarks>
public interface IRateService
{
    /// <summary>
    /// Gets live exchange rate for a currency pair
    /// </summary>
    /// <param name="fromCurrency">Source currency code</param>
    /// <param name="toCurrency">Target currency code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exchange rate response or null if unavailable</returns>
    /// <remarks>
    /// Uses stale-while-revalidate pattern per research.md decision 4:
    /// - Serves stale cache (up to 5 minutes old) immediately
    /// - Triggers background refresh if stale
    /// - Falls back through provider chain: Fawazahmed → Frankfurter → Transitive
    /// </remarks>
    Task<ExchangeRateResponse?> GetLiveRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical snapshot exchange rate for a currency pair on a specific date
    /// </summary>
    /// <param name="fromCurrency">Source currency code</param>
    /// <param name="toCurrency">Target currency code</param>
    /// <param name="date">Snapshot date (YYYY-MM-DD)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exchange rate response or null if unavailable for that date</returns>
    /// <remarks>
    /// User Story 3: Queries RateSnapshot table for historical data.
    /// Data retention: 90 days per research.md decision 8.
    /// </remarks>
    Task<ExchangeRateResponse?> GetSnapshotRateAsync(
        string fromCurrency,
        string toCurrency,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
