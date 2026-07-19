using Maliev.CurrencyService.Application.DTOs.Rates;

namespace Maliev.CurrencyService.Application.Interfaces;

/// <summary>
/// Exchange rate service interface.
/// </summary>
/// <remarks>
/// User Story 2: Provides live exchange rate retrieval with provider fallback and caching.
/// User Story 3: Extends with snapshot mode for historical rate queries.
/// </remarks>
public interface IRateService
{
    /// <summary>
    /// Gets the live exchange rate for a currency pair using a stale-while-revalidate pattern.
    /// </summary>
    /// <param name="fromCurrency">Source currency code (ISO 4217).</param>
    /// <param name="toCurrency">Target currency code (ISO 4217).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exchange rate response, or null if unavailable from all providers.</returns>
    /// <remarks>
    /// Per research.md decision 4: Serves stale cache (up to 5 minutes old) immediately
    /// and triggers background refresh if stale.
    /// Falls back through provider chain: Fawazahmed → Frankfurter → Transitive.
    /// </remarks>
    Task<ExchangeRateResponse?> GetLiveRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a historical snapshot exchange rate for a currency pair on a specific date.
    /// </summary>
    /// <param name="fromCurrency">Source currency code (ISO 4217).</param>
    /// <param name="toCurrency">Target currency code (ISO 4217).</param>
    /// <param name="date">The specific date for the snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exchange rate response, or null if no snapshot exists for that date.</returns>
    /// <remarks>
    /// User Story 3: Queries RateSnapshot table for historical data.
    /// Data retention: 90 days per research.md decision 8.
    /// </remarks>
    Task<ExchangeRateResponse?> GetSnapshotRateAsync(
        string fromCurrency,
        string toCurrency,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a specific exchange rate (admin only).
    /// </summary>
    /// <param name="from">Source currency code.</param>
    /// <param name="to">Target currency code.</param>
    /// <param name="rate">The new exchange rate value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateRateAsync(string from, string to, decimal rate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk updates exchange rates (admin only).
    /// </summary>
    /// <param name="rates">List of rate update requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BulkUpdateRatesAsync(List<UpdateRateRequest> rates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts an amount from one currency to another using the current exchange rate.
    /// </summary>
    /// <param name="fromCurrency">Source currency code (ISO 4217).</param>
    /// <param name="toCurrency">Target currency code (ISO 4217).</param>
    /// <param name="amount">The amount to convert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The conversion result with converted amount and exchange rate, or null if unavailable.</returns>
    Task<ConversionResult?> ConvertCurrencyAsync(
        string fromCurrency,
        string toCurrency,
        decimal amount,
        CancellationToken cancellationToken = default);
}
