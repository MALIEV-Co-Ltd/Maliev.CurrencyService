using Maliev.CurrencyService.Api.Metrics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Text.Json;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Two-tier caching service implementation (in-process MemoryCache + Redis)
/// </summary>
/// <remarks>
/// Per research.md decision 3: Provides sub-5ms response time from in-process cache
/// with cross-instance consistency via Redis distributed cache.
/// </remarks>
public class RedisCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheService"/> class.
    /// </summary>
    /// <param name="memoryCache">The in-memory cache instance.</param>
    /// <param name="logger">The logger for this service.</param>
    /// <param name="metrics">The metrics service.</param>
    /// <param name="distributedCache">The distributed cache instance (optional, for Redis).</param>
    /// <param name="redis">The Redis connection multiplexer (optional, for direct Redis commands).</param>
    public RedisCacheService(
        IMemoryCache memoryCache,
        ILogger<RedisCacheService> logger,
        CurrencyServiceMetrics metrics,
        IDistributedCache? distributedCache = null,
        IConnectionMultiplexer? redis = null)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _redis = redis;
        _logger = logger;
        _metrics = metrics;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Asynchronously retrieves a value from the two-tier cache (in-memory first, then Redis).
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, yielding the cached value or null if not found.</returns>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        // Check in-process cache first (sub-5ms)
        if (_memoryCache.TryGetValue(key, out T? value))
        {
            _metrics.RecordCacheHit();
            _metrics.RecordCacheRequest("hit");
            _logger.LogDebug("In-process cache hit for key: {Key}", key);
            return value;
        }

        // Check Redis distributed cache
        if (_distributedCache != null)
        {
            try
            {
                var json = await _distributedCache.GetStringAsync(key, cancellationToken);
                if (json != null)
                {
                    var redisValue = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                    if (redisValue != null)
                    {
                        // Populate in-process cache from Redis hit
                        var options = new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60),
                            Priority = CacheItemPriority.Normal
                        };
                        _memoryCache.Set(key, redisValue, options);

                        _metrics.RecordCacheHit();
                        _metrics.RecordCacheRequest("hit");
                        _logger.LogDebug("Redis cache hit for key: {Key}", key);
                        return redisValue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve from Redis cache for key: {Key}", key);
            }
        }

        // Cache miss
        _metrics.RecordCacheMiss();
        _metrics.RecordCacheRequest("miss");
        _logger.LogDebug("Cache miss for key: {Key}", key);
        return null;
    }

    /// <summary>
    /// Asynchronously sets a value in the two-tier cache (both in-process and Redis).
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="ttl">The time-to-live for the cached value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        // Set in-process cache (use shorter TTL for in-process, max 60s)
        var memoryTtl = TimeSpan.FromSeconds(Math.Min(60, ttl.TotalSeconds));
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = memoryTtl,
            Priority = CacheItemPriority.Normal
        };
        _memoryCache.Set(key, value, options);
        _logger.LogDebug("Set in-process cache for key: {Key} with TTL: {TTL}s", key, memoryTtl.TotalSeconds);

        // Set Redis distributed cache
        if (_distributedCache != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                var distributedOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                };
                await _distributedCache.SetStringAsync(key, json, distributedOptions, cancellationToken);
                _logger.LogDebug("Set Redis cache for key: {Key} with TTL: {TTL}s", key, ttl.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set Redis cache for key: {Key}", key);
            }
        }
    }

    /// <summary>
    /// Asynchronously removes a value from the two-tier cache (both in-process and Redis).
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        // Remove from in-process cache
        _memoryCache.Remove(key);
        _logger.LogDebug("Removed from in-process cache: {Key}", key);

        // Remove from Redis distributed cache with retry logic (per research.md)
        if (_distributedCache != null)
        {
            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    await _distributedCache.RemoveAsync(key, cancellationToken);
                    _logger.LogDebug("Removed from Redis cache: {Key}", key);
                    return;
                }
                catch when (retry < 2)
                {
                    _logger.LogWarning("Failed to remove from Redis cache (retry {Retry}/3): {Key}", retry + 1, key);
                    await Task.Delay(TimeSpan.FromMilliseconds(50 * (retry + 1)), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove from Redis cache after 3 retries: {Key}", key);
                    _metrics.RecordCacheInvalidationFailure();
                }
            }
        }
    }

    /// <summary>
    /// Asynchronously removes multiple values from the cache by pattern (in-process is not directly supported, Redis via SCAN + DEL).
    /// </summary>
    /// <param name="pattern">The key pattern (e.g., "rate:USD:*").</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        // For in-process cache, we cannot easily remove by pattern without iterating all keys
        // This is typically handled by TTL expiration
        _logger.LogDebug("Pattern-based removal requested: {Pattern} (in-process cache relies on TTL)", pattern);

        // Remove from Redis using SCAN + DEL
        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var database = _redis.GetDatabase();
                var server = _redis.GetServer(_redis.GetEndPoints().First());

                var keys = server.Keys(pattern: pattern).ToArray();
                if (keys.Length > 0)
                {
                    await database.KeyDeleteAsync(keys);
                    _logger.LogInformation("Removed {Count} keys matching pattern {Pattern} from Redis", keys.Length, pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove keys by pattern {Pattern} from Redis", pattern);
                _metrics.RecordCacheInvalidationFailure();
            }
        }
    }

    /// <summary>
    /// Asynchronously checks if a key exists in the two-tier cache (in-memory first, then Redis).
    /// </summary>
    /// <param name="key">The cache key to check.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation, yielding true if the key exists, otherwise false.</returns>
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        // Check in-process cache first
        if (_memoryCache.TryGetValue(key, out _))
        {
            return true;
        }

        // Check Redis
        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var database = _redis.GetDatabase();
                return await database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check key existence in Redis: {Key}", key);
            }
        }

        return false;
    }
}
