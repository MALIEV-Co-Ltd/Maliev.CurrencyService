using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.CurrencyService.Infrastructure.Services;

/// <summary>
/// Exchange rate service with stale-while-revalidate pattern.
/// </summary>
/// <remarks>
/// Implements stale-while-revalidate for sub-50ms response time
/// while maintaining data freshness through background refresh.
/// </remarks>
public class RateService : IRateService
{
    private readonly ProviderChain _providerChain;
    private readonly ICacheService _cacheService;
    private readonly CurrencyDbContext _context;
    private readonly ILogger<RateService> _logger;
    private readonly IRateServiceMetrics _metrics;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task> _refreshTasks = new();

    private const string RateCacheKeyPrefix = "rate";
    private const int FreshCacheTtlSeconds = 300;
    private const int StaleWindowSeconds = 60;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateService"/> class.
    /// </summary>
    /// <param name="providerChain">The provider chain for fetching exchange rates.</param>
    /// <param name="cacheService">The cache service.</param>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="metrics">The metrics service.</param>
    /// <param name="appLifetime">The application lifetime service.</param>
    public RateService(
        ProviderChain providerChain,
        ICacheService cacheService,
        CurrencyDbContext context,
        ILogger<RateService> logger,
        IRateServiceMetrics metrics,
        IHostApplicationLifetime appLifetime)
    {
        _providerChain = providerChain;
        _cacheService = cacheService;
        _context = context;
        _logger = logger;
        _metrics = metrics;
        _appLifetime = appLifetime;
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

        var cacheKey = $"{RateCacheKeyPrefix}:{from}:{to}";
        var cachedRate = await GetFromCacheAsync(cacheKey, from, to, cancellationToken);

        if (cachedRate != null)
        {
            var age = DateTime.UtcNow - cachedRate.FetchedAt;

            if (age.TotalSeconds <= FreshCacheTtlSeconds)
            {
                _logger.LogDebug("Fresh cache hit for {From}:{To} (age: {Age}s)", from, to, age.TotalSeconds);
                return MapToResponse(cachedRate, isFresh: true);
            }

            if (age.TotalSeconds <= FreshCacheTtlSeconds + StaleWindowSeconds)
            {
                _logger.LogDebug("Serving stale cache for {From}:{To} (age: {Age}s), triggering background refresh",
                    from, to, age.TotalSeconds);

                var refreshKey = $"refresh:{from}:{to}";
                _ = _refreshTasks.GetOrAdd(refreshKey, _ => Task.Run(async () =>
                {
                    try
                    {
                        await RefreshInBackgroundAsync(from, to, cacheKey, _appLifetime.ApplicationStopping);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Background refresh failed for {From}:{To}", from, to);
                    }
                    finally
                    {
                        _refreshTasks.TryRemove(refreshKey, out Task? _);
                    }
                }, _appLifetime.ApplicationStopping));

                return MapToResponse(cachedRate, isFresh: false, staleAgeSeconds: (int)age.TotalSeconds);
            }

            _logger.LogInformation("Cached rate too old for {From}:{To} (age: {Age}s), fetching fresh",
                from, to, age.TotalSeconds);
        }

        var freshRate = await FetchAndCacheAsync(from, to, cacheKey, cancellationToken);
        return freshRate != null ? MapToResponse(freshRate, isFresh: true) : null;
    }

    private async Task<ExchangeRate?> GetFromCacheAsync(
        string cacheKey,
        string from,
        string to,
        CancellationToken cancellationToken)
    {
        var cachedResponse = await _cacheService.GetAsync<ExchangeRateResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _metrics.RecordCacheRequest("hit");
            _metrics.RecordCacheHit();

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

        var dbRate = await _context.ExchangeRates
            .AsNoTracking()
            .Where(r => r.FromCurrency == from && r.ToCurrency == to)
            .OrderByDescending(r => r.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (dbRate == null)
        {
            _metrics.RecordCacheRequest("miss");
            _metrics.RecordCacheMiss();
        }

        return dbRate;
    }

    private async Task<ExchangeRate?> FetchAndCacheAsync(
        string from,
        string to,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var rate = await _providerChain.GetRateAsync(from, to, cancellationToken);

            if (rate == null)
            {
                _logger.LogWarning("No rate available from any provider for {From}:{To}", from, to);
                return null;
            }

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

    private async Task CacheRateAsync(ExchangeRate rate, string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            _context.ExchangeRates.Add(rate);
            await _context.SaveChangesAsync(cancellationToken);

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
    /// <param name="date">The specific date for the snapshot.</param>
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

        var cacheKey = $"snapshot:{from}:{to}:{date:yyyy-MM-dd}";
        var cachedResponse = await _cacheService.GetAsync<ExchangeRateResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogDebug("Snapshot cache hit for {From}:{To} on {Date}", from, to, date);
            _metrics.RecordCacheRequest("hit");
            _metrics.RecordCacheHit();
            return cachedResponse;
        }

        _logger.LogDebug("Snapshot cache miss for {From}:{To} on {Date}", from, to, date);
        _metrics.RecordCacheRequest("miss");
        _metrics.RecordCacheMiss();

        var snapshot = await _context.RateSnapshots
            .AsNoTracking()
            .Where(s => s.FromCurrency == from && s.ToCurrency == to && s.SnapshotDate == date)
            .FirstOrDefaultAsync(cancellationToken);

        if (snapshot == null)
        {
            _logger.LogWarning("No snapshot found for {From}:{To} on {Date}", from, to, date);
            return null;
        }

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

        await _cacheService.SetAsync(
            cacheKey,
            response,
            TimeSpan.FromHours(24),
            cancellationToken);

        _logger.LogInformation("Retrieved snapshot for {From}:{To} on {Date} = {Rate}",
            from, to, date, snapshot.Rate);

        return response;
    }

    /// <inheritdoc />
    public async Task UpdateRateAsync(string from, string to, decimal rate, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var exchangeRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = from.ToUpperInvariant(),
            ToCurrency = to.ToUpperInvariant(),
            Rate = rate,
            Provider = "ManualAdmin",
            IsTransitive = false,
            FetchedAt = now,
            ExpiresAt = now.AddSeconds(FreshCacheTtlSeconds),
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ExchangeRates.Add(exchangeRate);
        await _context.SaveChangesAsync(cancellationToken);

        var cacheKey = $"{RateCacheKeyPrefix}:{from.ToUpperInvariant()}:{to.ToUpperInvariant()}";
        await _cacheService.RemoveAsync(cacheKey, cancellationToken);

        _logger.LogInformation("Admin updated rate {From}:{To} to {Rate}", from, to, rate);
    }

    /// <inheritdoc />
    public async Task BulkUpdateRatesAsync(List<UpdateRateRequest> rates, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var newRates = rates.Select(r => new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = r.From.ToUpperInvariant(),
            ToCurrency = r.To.ToUpperInvariant(),
            Rate = r.Rate,
            Provider = "ManualAdminBulk",
            IsTransitive = false,
            FetchedAt = now,
            ExpiresAt = now.AddSeconds(FreshCacheTtlSeconds),
            CreatedAt = now,
            UpdatedAt = now
        }).ToList();

        _context.ExchangeRates.AddRange(newRates);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var r in rates)
        {
            var cacheKey = $"{RateCacheKeyPrefix}:{r.From.ToUpperInvariant()}:{r.To.ToUpperInvariant()}";
            await _cacheService.RemoveAsync(cacheKey, cancellationToken);
        }

        _logger.LogInformation("Admin bulk updated {Count} rates", rates.Count);
    }

    /// <inheritdoc />
    public async Task<ConversionResult?> ConvertCurrencyAsync(
        string fromCurrency,
        string toCurrency,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var from = fromCurrency.ToUpperInvariant();
        var to = toCurrency.ToUpperInvariant();

        _logger.LogDebug("ConvertCurrencyAsync: {Amount} {From} → {To}", amount, from, to);

        // Get the live exchange rate
        var rateResponse = await GetLiveRateAsync(from, to, cancellationToken);
        if (rateResponse == null)
        {
            _logger.LogWarning("No rate available for conversion {From}:{To}", from, to);
            return null;
        }

        // Calculate converted amount
        var convertedAmount = amount * rateResponse.Rate;

        _logger.LogInformation("Converted {Amount} {From} to {ConvertedAmount} {To} at rate {Rate}",
            amount, from, convertedAmount, to, rateResponse.Rate);

        return new ConversionResult
        {
            FromCurrency = from,
            ToCurrency = to,
            OriginalAmount = amount,
            ConvertedAmount = convertedAmount,
            ExchangeRate = rateResponse.Rate,
            RateTimestamp = rateResponse.Timestamp,
            Source = rateResponse.Source,
            IsTransitive = rateResponse.IsTransitive,
            IntermediateCurrency = rateResponse.IntermediateCurrency
        };
    }

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
