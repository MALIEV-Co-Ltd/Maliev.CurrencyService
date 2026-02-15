using Microsoft.Extensions.Caching.Memory;

namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Defines the interface for a service that manages cache tags and associated cache keys.
/// </summary>
public interface ICacheTagService
{
    /// <summary>
    /// Adds a cache key to a specified tag.
    /// </summary>
    /// <param name="tag">The tag to associate the cache key with.</param>
    /// <param name="cacheKey">The cache key to add.</param>
    void AddCacheKeyToTag(string tag, string cacheKey);
    /// <summary>
    /// Removes all cache keys associated with a specified tag, and then removes the tag itself.
    /// </summary>
    /// <param name="tag">The tag whose associated cache keys and the tag itself should be removed.</param>
    void RemoveCacheKeysByTag(string tag);
    /// <summary>
    /// Retrieves all cache keys associated with a specified tag.
    /// </summary>
    /// <param name="tag">The tag to retrieve cache keys for.</param>
    /// <returns>An enumerable collection of cache keys associated with the tag.</returns>
    IEnumerable<string> GetCacheKeysByTag(string tag);
}

/// <summary>
/// Provides a service for managing cache tags and their associated cache keys using an in-memory cache.
/// </summary>
public class CacheTagService : ICacheTagService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheTagService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheTagService"/> class.
    /// </summary>
    /// <param name="cache">The in-memory cache instance.</param>
    /// <param name="logger">The logger for this service.</param>
    public CacheTagService(IMemoryCache cache, ILogger<CacheTagService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Adds a cache key to a specified tag. If the tag does not exist, it will be created.
    /// </summary>
    /// <param name="tag">The tag to associate the cache key with.</param>
    /// <param name="cacheKey">The cache key to add.</param>
    public void AddCacheKeyToTag(string tag, string cacheKey)
    {
        try
        {
            var tagKey = $"cache_tag_{tag}";

            HashSet<string>? cacheKeys;
            if (_cache.TryGetValue(tagKey, out cacheKeys) && cacheKeys != null)
            {
                cacheKeys.Add(cacheKey);
                _cache.Set(tagKey, cacheKeys, new MemoryCacheEntryOptions
                {
                    Priority = CacheItemPriority.NeverRemove,
                    Size = 1 // Required when SizeLimit is set
                });
            }
            else
            {
                var newCacheKeys = new HashSet<string> { cacheKey };
                _cache.Set(tagKey, newCacheKeys, new MemoryCacheEntryOptions
                {
                    Priority = CacheItemPriority.NeverRemove
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding cache key {CacheKey} to tag {Tag}", cacheKey, tag);
        }
    }

    /// <summary>
    /// Removes all cache keys associated with a specified tag from the cache, and then removes the tag itself.
    /// </summary>
    /// <param name="tag">The tag whose associated cache keys and the tag itself should be removed.</param>
    public void RemoveCacheKeysByTag(string tag)
    {
        try
        {
            var tagKey = $"cache_tag_{tag}";

            HashSet<string>? cacheKeys;
            if (_cache.TryGetValue(tagKey, out cacheKeys) && cacheKeys != null)
            {
                foreach (var cacheKey in cacheKeys)
                {
                    _cache.Remove(cacheKey);
                    _logger.LogDebug("Removed cache key {CacheKey} by tag {Tag}", cacheKey, tag);
                }

                // Remove the tag itself
                _cache.Remove(tagKey);
                _logger.LogDebug("Removed cache tag {Tag}", tag);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache keys by tag {Tag}", tag);
        }
    }

    /// <summary>
    /// Retrieves all cache keys associated with a specified tag.
    /// </summary>
    /// <param name="tag">The tag to retrieve cache keys for.</param>
    /// <returns>An enumerable collection of cache keys associated with the tag. Returns an empty enumerable if the tag does not exist.</returns>
    public IEnumerable<string> GetCacheKeysByTag(string tag)
    {
        var tagKey = $"cache_tag_{tag}";

        HashSet<string>? cacheKeys;
        if (_cache.TryGetValue(tagKey, out cacheKeys) && cacheKeys != null)
        {
            return cacheKeys;
        }

        return Enumerable.Empty<string>();
    }
}
