using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Data.DbContexts;
using Maliev.CurrencyService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Maliev.CurrencyService.Api.Services;

public class ExchangeRateService : IExchangeRateService
{
    private readonly IEnumerable<IExchangeRateProvider> _providers;
    private readonly IMemoryCache _cache;
    private readonly CurrencyDbContext _dbContext;
    private readonly ExchangeRateOptions _options;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(
        IEnumerable<IExchangeRateProvider> providers,
        IMemoryCache cache,
        CurrencyDbContext dbContext,
        IOptions<ExchangeRateOptions> options,
        ILogger<ExchangeRateService> logger)
    {
        _providers = providers;
        _cache = cache;
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ExchangeRateDto?> GetExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        // Normalize currency codes
        fromCurrency = fromCurrency.ToUpperInvariant();
        toCurrency = toCurrency.ToUpperInvariant();

        // Same currency conversion
        if (fromCurrency == toCurrency)
        {
            return new ExchangeRateDto
            {
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                Rate = 1.0m,
                FetchedAt = DateTime.UtcNow,
                Source = "Direct"
            };
        }

        // Check cache first
        var cacheKey = $"rate_{fromCurrency}_{toCurrency}";
        if (_cache.TryGetValue(cacheKey, out ExchangeRateDto? cachedRate))
        {
            _logger.LogDebug("Cache hit for {From} to {To}", fromCurrency, toCurrency);
            return cachedRate;
        }

        // Try each provider in order
        var orderedProviders = OrderProviders();
        foreach (var provider in orderedProviders)
        {
            try
            {
                _logger.LogDebug("Trying provider {Provider} for {From} to {To}", 
                    provider.Name, fromCurrency, toCurrency);

                var rate = await provider.GetExchangeRateAsync(fromCurrency, toCurrency, cancellationToken);
                if (rate != null)
                {
                    // Cache the result
                    var cacheExpiry = TimeSpan.FromMinutes(_options.CacheDurationMinutes);
                    _cache.Set(cacheKey, rate, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = cacheExpiry,
                        Size = 1  // Each exchange rate entry counts as 1 unit
                    });

                    // Store in database for historical purposes
                    await StoreRateInDatabaseAsync(rate, cancellationToken);

                    _logger.LogInformation("Successfully fetched rate from {Provider}: {From} to {To} = {Rate}", 
                        provider.Name, fromCurrency, toCurrency, rate.Rate);
                    return rate;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} failed for {From} to {To}", 
                    provider.Name, fromCurrency, toCurrency);
            }
        }

        // If all providers fail, try to get cached rate from database
        var dbRate = await GetRateFromDatabaseAsync(fromCurrency, toCurrency, cancellationToken);
        if (dbRate != null)
        {
            _logger.LogWarning("All providers failed, returning database cache for {From} to {To}", 
                fromCurrency, toCurrency);
            return dbRate;
        }

        _logger.LogError("All providers failed and no cached rate available for {From} to {To}", 
            fromCurrency, toCurrency);
        return null;
    }

    public async Task<Dictionary<string, ExchangeRateDto>> GetMultipleRatesAsync(string baseCurrency, IEnumerable<string> targetCurrencies, CancellationToken cancellationToken = default)
    {
        baseCurrency = baseCurrency.ToUpperInvariant();
        var targets = targetCurrencies.Select(c => c.ToUpperInvariant()).ToList();
        var result = new Dictionary<string, ExchangeRateDto>();

        // Check cache for each currency first
        var uncachedTargets = new List<string>();
        foreach (var target in targets)
        {
            if (baseCurrency == target)
            {
                result[target] = new ExchangeRateDto
                {
                    FromCurrency = baseCurrency,
                    ToCurrency = target,
                    Rate = 1.0m,
                    FetchedAt = DateTime.UtcNow,
                    Source = "Direct"
                };
                continue;
            }

            var cacheKey = $"rate_{baseCurrency}_{target}";
            if (_cache.TryGetValue(cacheKey, out ExchangeRateDto? cachedRate))
            {
                result[target] = cachedRate;
            }
            else
            {
                uncachedTargets.Add(target);
            }
        }

        if (!uncachedTargets.Any())
        {
            return result;
        }

        // Try each provider for bulk fetching
        var orderedProviders = OrderProviders();
        foreach (var provider in orderedProviders)
        {
            if (!uncachedTargets.Any()) break;

            try
            {
                _logger.LogDebug("Trying bulk fetch from {Provider} for {Base} to [{Targets}]", 
                    provider.Name, baseCurrency, string.Join(",", uncachedTargets));

                var rates = await provider.GetMultipleRatesAsync(baseCurrency, uncachedTargets, cancellationToken);
                if (rates != null && rates.Any())
                {
                    var cacheExpiry = TimeSpan.FromMinutes(_options.CacheDurationMinutes);
                    
                    foreach (var kvp in rates)
                    {
                        result[kvp.Key] = kvp.Value;
                        uncachedTargets.Remove(kvp.Key);

                        // Cache individual rates
                        var cacheKey = $"rate_{baseCurrency}_{kvp.Key}";
                        _cache.Set(cacheKey, kvp.Value, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = cacheExpiry,
                            Size = 1  // Each exchange rate entry counts as 1 unit
                        });

                        // Store in database
                        await StoreRateInDatabaseAsync(kvp.Value, cancellationToken);
                    }

                    _logger.LogInformation("Successfully fetched {Count} rates from {Provider}", 
                        rates.Count, provider.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bulk fetch from {Provider} failed for {Base}", provider.Name, baseCurrency);
            }
        }

        // For any remaining uncached targets, try individual fetches
        foreach (var target in uncachedTargets)
        {
            var rate = await GetExchangeRateAsync(baseCurrency, target, cancellationToken);
            if (rate != null)
            {
                result[target] = rate;
            }
        }

        return result;
    }

    public async Task<ConvertCurrencyResponse?> ConvertCurrencyAsync(ConvertCurrencyRequest request, CancellationToken cancellationToken = default)
    {
        var rate = await GetExchangeRateAsync(request.From, request.To, cancellationToken);
        if (rate == null)
        {
            return null;
        }

        var convertedAmount = request.Amount * rate.Rate;

        return new ConvertCurrencyResponse
        {
            FromCurrency = request.From,
            ToCurrency = request.To,
            OriginalAmount = request.Amount,
            ConvertedAmount = convertedAmount,
            ExchangeRate = rate.Rate,
            RateTimestamp = rate.FetchedAt,
            Source = rate.Source
        };
    }

    private IEnumerable<IExchangeRateProvider> OrderProviders()
    {
        var providerDict = _providers.ToDictionary(p => p.Name, p => p);
        
        // Return providers in configured order, then any remaining ones
        foreach (var providerName in _options.ProviderOrder)
        {
            if (providerDict.TryGetValue(providerName, out var provider))
            {
                yield return provider;
                providerDict.Remove(providerName);
            }
        }

        // Add any remaining providers
        foreach (var provider in providerDict.Values)
        {
            yield return provider;
        }
    }

    private async Task StoreRateInDatabaseAsync(ExchangeRateDto rate, CancellationToken cancellationToken)
    {
        try
        {
            var entity = new ExchangeRate
            {
                FromCurrencyCode = rate.FromCurrency,
                ToCurrencyCode = rate.ToCurrency,
                Rate = rate.Rate,
                FetchedAt = rate.FetchedAt,
                Source = rate.Source,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };

            _dbContext.ExchangeRates.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store rate in database: {From} to {To}", 
                rate.FromCurrency, rate.ToCurrency);
        }
    }

    private async Task<ExchangeRateDto?> GetRateFromDatabaseAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await _dbContext.ExchangeRates
                .Where(r => r.FromCurrencyCode == fromCurrency && r.ToCurrencyCode == toCurrency)
                .OrderByDescending(r => r.FetchedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null) return null;

            return new ExchangeRateDto
            {
                FromCurrency = entity.FromCurrencyCode,
                ToCurrency = entity.ToCurrencyCode,
                Rate = entity.Rate,
                FetchedAt = entity.FetchedAt,
                Source = $"{entity.Source} (cached)"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get rate from database: {From} to {To}", fromCurrency, toCurrency);
            return null;
        }
    }
}