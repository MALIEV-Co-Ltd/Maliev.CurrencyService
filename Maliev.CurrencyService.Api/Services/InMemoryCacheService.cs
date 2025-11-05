using Maliev.CurrencyService.Api.Metrics;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Simple in-memory cache implementation for testing
/// </summary>
public class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<InMemoryCacheService> _logger;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly ConcurrentDictionary<string, byte> _keys = new();

    public InMemoryCacheService(
        IMemoryCache cache,
        ILogger<InMemoryCacheService> logger,
        CurrencyServiceMetrics metrics)
    {
        _cache = cache;
        _logger = logger;
        _metrics = metrics;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogDebug("Getting from in-memory cache: {Key}", key);
        var value = _cache.Get<T>(key);

        if (value != null)
        {
            _metrics.CacheHits.Inc();
            _metrics.CacheRequests.WithLabels("hit").Inc();
            _logger.LogDebug("In-memory cache hit for key: {Key}", key);
        }
        else
        {
            _metrics.CacheMisses.Inc();
            _metrics.CacheRequests.WithLabels("miss").Inc();
            _logger.LogDebug("In-memory cache miss for key: {Key}", key);
        }

        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        _logger.LogDebug("Setting in-memory cache: {Key}, TTL: {TTL}", key, ttl);
        _cache.Set(key, value, ttl);
        _keys.TryAdd(key, 0); // Track the key
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Removing from in-memory cache: {Key}", key);
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // Convert glob pattern to regex (simple implementation for * wildcard)
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var regex = new System.Text.RegularExpressions.Regex(regexPattern);

        var keysToRemove = _keys.Keys.Where(k => regex.IsMatch(k)).ToList();

        _logger.LogDebug("Removing {Count} keys matching pattern: {Pattern}", keysToRemove.Count, pattern);

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var exists = _cache.TryGetValue(key, out _);
        return Task.FromResult(exists);
    }
}
