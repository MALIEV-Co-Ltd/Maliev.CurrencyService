using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.CurrencyService.Infrastructure.Services.External;

/// <summary>
/// Provider chain with fallback and transitive rate calculation.
/// </summary>
/// <remarks>
/// Per research.md decisions 1 and 2:
/// - Tries Fawazahmed (primary), Frankfurter (fallback)
/// - If both fail, attempts transitive calculation via USD, EUR, GBP
/// - Tracks fallback metrics for monitoring
/// </remarks>
public class ProviderChain
{
    private readonly IEnumerable<IExchangeRateProvider> _providers;
    private readonly ILogger<ProviderChain> _logger;
    private readonly IProviderMetrics _metrics;

    // Intermediary currencies for transitive calculation (per research.md decision 2)
    private static readonly string[] IntermediaryFallback = { "USD", "EUR", "GBP" };

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderChain"/> class.
    /// </summary>
    /// <param name="providers">A collection of exchange rate providers.</param>
    /// <param name="logger">The logger for this service.</param>
    /// <param name="metrics">The provider metrics service.</param>
    public ProviderChain(
        IEnumerable<IExchangeRateProvider> providers,
        ILogger<ProviderChain> logger,
        IProviderMetrics metrics)
    {
        _providers = providers;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Gets exchange rate with provider fallback and transitive calculation.
    /// </summary>
    /// <param name="fromCurrency">Source currency.</param>
    /// <param name="toCurrency">Target currency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exchange rate or null if unavailable from all providers.</returns>
    public virtual async Task<ExchangeRate?> GetRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        // Try each provider in order (Fawazahmed → Frankfurter)
        string? previousProvider = null;
        foreach (var provider in _providers)
        {
            var rate = await provider.GetRateAsync(fromCurrency, toCurrency, cancellationToken);
            if (rate != null)
            {
                // Track fallback if we had to skip a provider
                if (previousProvider != null)
                {
                    _metrics.RecordProviderFallback(previousProvider, provider.ProviderName);
                    _logger.LogWarning("Provider fallback: {From} → {To} ({FromProvider} failed, using {ToProvider})",
                        previousProvider, provider.ProviderName, fromCurrency, toCurrency);
                }

                return rate;
            }

            previousProvider = provider.ProviderName;
        }

        // All direct providers failed - try transitive calculation
        _logger.LogInformation("All providers failed for {From}:{To}, attempting transitive calculation",
            fromCurrency, toCurrency);

        return await GetTransitiveRateAsync(fromCurrency, toCurrency, cancellationToken);
    }

    /// <summary>
    /// Calculates transitive rate via intermediary currency.
    /// </summary>
    /// <remarks>
    /// Per research.md decision 2: Tries USD → EUR → GBP as intermediaries.
    /// Calculation: rate(A→C) = rate(A→B) × rate(B→C).
    /// Cache with shorter TTL (60s vs 300s for direct).
    /// </remarks>
    private async Task<ExchangeRate?> GetTransitiveRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken)
    {
        // Try each intermediary in fallback order
        foreach (var intermediary in IntermediaryFallback)
        {
            // Skip if from or to is the intermediary (would be direct rate)
            if (intermediary.Equals(fromCurrency, StringComparison.OrdinalIgnoreCase) ||
                intermediary.Equals(toCurrency, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var transitiveRate = await TryTransitiveViaIntermediaryAsync(
                fromCurrency, toCurrency, intermediary, cancellationToken);

            if (transitiveRate != null)
            {
                _logger.LogInformation("Calculated transitive rate {From}:{To} via {Intermediary} = {Rate}",
                    fromCurrency, toCurrency, intermediary, transitiveRate.Rate);
                return transitiveRate;
            }
        }

        // No transitive path found
        _logger.LogWarning("No transitive path found for {From}:{To} via any intermediary",
            fromCurrency, toCurrency);
        return null;
    }

    /// <summary>
    /// Attempts to calculate transitive rate via a specific intermediary currency.
    /// </summary>
    /// <param name="fromCurrency">Source currency code.</param>
    /// <param name="toCurrency">Target currency code.</param>
    /// <param name="intermediary">Intermediary currency code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A transitive <see cref="ExchangeRate"/> or null if calculation failed.</returns>
    private async Task<ExchangeRate?> TryTransitiveViaIntermediaryAsync(
        string fromCurrency,
        string toCurrency,
        string intermediary,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get from → intermediary
            ExchangeRate? rate1 = null;
            foreach (var provider in _providers)
            {
                rate1 = await provider.GetRateAsync(fromCurrency, intermediary, cancellationToken);
                if (rate1 != null) break;
            }

            if (rate1 == null)
            {
                _logger.LogDebug("No rate found for {From}:{Intermediary}", fromCurrency, intermediary);
                return null;
            }

            // Get intermediary → to
            ExchangeRate? rate2 = null;
            foreach (var provider in _providers)
            {
                rate2 = await provider.GetRateAsync(intermediary, toCurrency, cancellationToken);
                if (rate2 != null) break;
            }

            if (rate2 == null)
            {
                _logger.LogDebug("No rate found for {Intermediary}:{To}", intermediary, toCurrency);
                return null;
            }

            // Calculate combined rate: A→C = (A→B) × (B→C)
            var combinedRate = rate1.Rate * rate2.Rate;
            var roundedRate = Math.Round(combinedRate, 6); // FR-SC-012: 6 decimal precision

            var now = DateTime.UtcNow;
            return new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = fromCurrency.ToUpper(),
                ToCurrency = toCurrency.ToUpper(),
                Rate = roundedRate,
                Provider = $"Transitive:{rate1.Provider},{rate2.Provider}",
                IsTransitive = true,
                IntermediateCurrency = intermediary,
                FetchedAt = now,
                ExpiresAt = now.AddSeconds(60), // Shorter TTL for transitive (60s vs 300s)
                CreatedAt = now,
                UpdatedAt = now
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating transitive rate {From}:{To} via {Intermediary}",
                fromCurrency, toCurrency, intermediary);
            return null;
        }
    }
}
