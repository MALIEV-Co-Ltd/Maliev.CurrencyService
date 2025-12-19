using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Models.Rates;
using Maliev.CurrencyService.Api.Services.External;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Exchange rate service with stale-while-revalidate pattern
/// </summary>
/// <remarks>
/// Per research.md decision 4: Implements stale-while-revalidate for sub-50ms response time
/// while maintaining data freshness through background refresh.
/// </remarks>
public class RateService : IRateService
{
    private readonly ProviderChain _providerChain;
    private readonly ICacheService _cacheService;
    private readonly CurrencyDbContext _context;
    private readonly ILogger<RateService> _logger;
    private readonly CurrencyServiceMetrics _metrics;

    private const string RateCacheKeyPrefix = "rate";
    private const int FreshCacheTtlSeconds = 300; // 5 minutes
    private const int StaleWindowSeconds = 60; // 60 seconds past expiration

    /// <summary>
    /// Initializes a new instance of the <see cref="RateService"/> class.
    /// </summary>
    /// <param name="providerChain">The provider chain for fetching exchange rates.</param>
    /// <param name="cacheService">The cache service.</param>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="metrics">The metrics service.</param>
    public RateService(
        ProviderChain providerChain,
        ICacheService cacheService,
        CurrencyDbContext context,
        ILogger<RateService> logger,
        CurrencyServiceMetrics metrics)
    {
        _providerChain = providerChain;
        _cacheService = cacheService;
        _context = context;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Gets live exchange rate for a currency pair using a stale-while-revalidate pattern.
    /// </summary>
    /// <param name="fromCurrency">Source currency code (ISO 4217).</param>
    /// <param name="toCurrency">Target currency code (ISO 4217).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="ExchangeRateResponse"/> if found, otherwise null.</returns>
    public async Task<ExchangeRateResponse?> GetLiveRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        var from = fromCurrency.ToUpperInvariant();
        var to = toCurrency.ToUpperInvariant();

        _logger.LogDebug("GetLiveRateAsync: {From} → {To}", from, to);

        // Try to get from cache (database or in-memory cache)
        var cacheKey = $"{RateCacheKeyPrefix}:{from}:{to}";
        var cachedRate = await GetFromCacheAsync(cacheKey, from, to, cancellationToken);

        if (cachedRate != null)
        {
            var age = DateTime.UtcNow - cachedRate.FetchedAt;

            // Fresh cache (within TTL) - return immediately
            if (age.TotalSeconds <= FreshCacheTtlSeconds)
            {
                _logger.LogDebug("Fresh cache hit for {From}:{To} (age: {Age}s)", from, to, age.TotalSeconds);
                return MapToResponse(cachedRate, isFresh: true);
            }

            // Stale but within revalidation window - return stale + trigger background refresh
            if (age.TotalSeconds <= FreshCacheTtlSeconds + StaleWindowSeconds)
            {
                _logger.LogDebug("Serving stale cache for {From}:{To} (age: {Age}s), triggering background refresh",
                    from, to, age.TotalSeconds);

                // Trigger background refresh (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await RefreshInBackgroundAsync(from, to, cacheKey, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background refresh failed for {From}:{To}", from, to);
                    }
                });

                return MapToResponse(cachedRate, isFresh: false, staleAgeSeconds: (int)age.TotalSeconds);
            }

            _logger.LogInformation("Cached rate too old for {From}:{To} (age: {Age}s), fetching fresh",
                from, to, age.TotalSeconds);
        }

        // No cache or beyond stale window - fetch synchronously
        var freshRate = await FetchAndCacheAsync(from, to, cacheKey, cancellationToken);
        return freshRate != null ? MapToResponse(freshRate, isFresh: true) : null;
    }

    /// <summary>
    /// Gets rate from cache (in-memory or database)
    /// </summary>
    private async Task<ExchangeRate?> GetFromCacheAsync(
        string cacheKey,
        string from,
        string to,
        CancellationToken cancellationToken)
    {
        // Try in-memory/Redis cache first
        var cachedResponse = await _cacheService.GetAsync<ExchangeRateResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            // Map back to ExchangeRate entity for age calculation
            return new ExchangeRate
            {
                FromCurrency = cachedResponse.FromCurrency,
                ToCurrency = cachedResponse.ToCurrency,
                Rate = cachedResponse.Rate,
                Provider = cachedResponse.Source,
                IsTransitive = cachedResponse.IsTransitive,
                IntermediateCurrency = cachedResponse.IntermediateCurrency,
                FetchedAt = cachedResponse.Timestamp,
                ExpiresAt = cachedResponse.Timestamp.AddSeconds(FreshCacheTtlSeconds)
            };
        }

        // Try database cache
        var dbRate = await _context.ExchangeRates
            .AsNoTracking()
            .Where(r => r.FromCurrency == from && r.ToCurrency == to)
            .OrderByDescending(r => r.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return dbRate;
    }

    /// <summary>
    /// Fetches rate from provider chain and caches it
    /// </summary>
    private async Task<ExchangeRate?> FetchAndCacheAsync(
        string from,
        string to,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        try
        {
            // Fetch from provider chain (Fawazahmed → Frankfurter → Transitive)
            var rate = await _providerChain.GetRateAsync(from, to, cancellationToken);

            if (rate == null)
            {
                _logger.LogWarning("No rate available from any provider for {From}:{To}", from, to);
                return null;
            }

            // Cache in both database and distributed cache
            await CacheRateAsync(rate, cacheKey, cancellationToken);

            return rate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching rate for {From}:{To}", from, to);
            _metrics.RecordProviderError("ProviderChain", "fetch_error");
            return null;
        }
    }

    /// <summary>
    /// Background refresh task (fire and forget)
    /// </summary>
    private async Task RefreshInBackgroundAsync(
        string from,
        string to,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Background refresh started for {From}:{To}", from, to);
        _metrics.RecordBackgroundJobExecution("rate_refresh");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var rate = await _providerChain.GetRateAsync(from, to, cancellationToken);

            if (rate != null)
            {
                await CacheRateAsync(rate, cacheKey, cancellationToken);
                _logger.LogInformation("Background refresh completed for {From}:{To} in {Elapsed}ms",
                    from, to, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("Background refresh failed - no rate available for {From}:{To}", from, to);
                _metrics.RecordBackgroundJobFailure("rate_refresh", "no_rate");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background refresh error for {From}:{To}", from, to);
            _metrics.RecordBackgroundJobFailure("rate_refresh", "exception");
        }
        finally
        {
            stopwatch.Stop();
            _metrics.RecordBackgroundJobDuration("rate_refresh", stopwatch.Elapsed.TotalSeconds);
        }
    }

    /// <summary>
    /// Caches rate in both database and distributed cache
    /// </summary>
    private async Task CacheRateAsync(ExchangeRate rate, string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            // Save to database for persistence
            _context.ExchangeRates.Add(rate);
            await _context.SaveChangesAsync(cancellationToken);

            // Cache response in distributed cache
            var response = MapToResponse(rate, isFresh: true);
            await _cacheService.SetAsync(
                cacheKey,
                response,
                TimeSpan.FromSeconds(FreshCacheTtlSeconds),
                cancellationToken);

            _logger.LogDebug("Cached rate {From}:{To} = {Rate} (provider: {Provider})",
                rate.FromCurrency, rate.ToCurrency, rate.Rate, rate.Provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching rate for {From}:{To}", rate.FromCurrency, rate.ToCurrency);
        }
    }

    /// <summary>
    /// Gets historical snapshot exchange rate for a currency pair on a specific date.
    /// </summary>
    /// <param name="fromCurrency">Source currency code (ISO 4217).</param>
    /// <param name="toCurrency">Target currency code (ISO 4217).</param>
    /// <param name="date">The specific date for the snapshot (YYYY-MM-DD).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="ExchangeRateResponse"/> if found for the specified date, otherwise null.</returns>
    public async Task<ExchangeRateResponse?> GetSnapshotRateAsync(
        string fromCurrency,
        string toCurrency,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var from = fromCurrency.ToUpperInvariant();
        var to = toCurrency.ToUpperInvariant();

        _logger.LogInformation("GetSnapshotRateAsync: {From} → {To} on {Date}", from, to, date);

        // Try cache first (snapshots don't change, so can cache longer)
        var cacheKey = $"snapshot:{from}:{to}:{date:yyyy-MM-dd}";
        var cachedResponse = await _cacheService.GetAsync<ExchangeRateResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogDebug("Snapshot cache hit for {From}:{To} on {Date}", from, to, date);
            return cachedResponse;
        }

        _logger.LogDebug("Snapshot cache miss for {From}:{To} on {Date}", from, to, date);

        // Query database for snapshot
        var snapshot = await _context.RateSnapshots
            .AsNoTracking()
            .Where(s => s.FromCurrency == from && s.ToCurrency == to && s.SnapshotDate == date)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot == null)
        {
            _logger.LogWarning("No snapshot found for {From}:{To} on {Date}", from, to, date);
            return null;
        }

        // Map to response
        var response = new ExchangeRateResponse
        {
            FromCurrency = snapshot.FromCurrency,
            ToCurrency = snapshot.ToCurrency,
            Rate = snapshot.Rate,
            Timestamp = snapshot.CreatedAt,
            Source = snapshot.Source ?? "Snapshot",
            IsTransitive = false,
            IntermediateCurrency = null,
            CalculationDetails = null,
            Mode = "snapshot",
            SnapshotDate = snapshot.SnapshotDate
        };

        // Cache for 24 hours (snapshots don't change)
        await _cacheService.SetAsync(
            cacheKey,
            response,
            TimeSpan.FromHours(24),
            cancellationToken);

        _logger.LogInformation("Retrieved snapshot for {From}:{To} on {Date} = {Rate}",
            from, to, date, snapshot.Rate);

        return response;
    }

    /// <summary>
    /// Maps ExchangeRate entity to ExchangeRateResponse DTO
    /// </summary>
    private static ExchangeRateResponse MapToResponse(
        ExchangeRate rate,
        bool isFresh,
        int? staleAgeSeconds = null)
    {
        return new ExchangeRateResponse
        {
            FromCurrency = rate.FromCurrency,
            ToCurrency = rate.ToCurrency,
            Rate = rate.Rate,
            Timestamp = rate.FetchedAt,
            Source = rate.Provider,
            IsTransitive = rate.IsTransitive,
            IntermediateCurrency = rate.IntermediateCurrency,
            CalculationDetails = rate.IsTransitive && rate.IntermediateCurrency != null
                ? $"{rate.FromCurrency}/{rate.IntermediateCurrency} × {rate.IntermediateCurrency}/{rate.ToCurrency}"
                : null,
            Mode = "live"
        };
    }
}
