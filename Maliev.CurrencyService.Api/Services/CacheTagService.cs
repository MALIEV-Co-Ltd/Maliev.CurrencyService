using Microsoft.Extensions.Caching.Memory;

namespace Maliev.CurrencyService.Api.Services;

public interface ICacheTagService
{
    void AddCacheKeyToTag(string tag, string cacheKey);
    void RemoveCacheKeysByTag(string tag);
    IEnumerable<string> GetCacheKeysByTag(string tag);
}

public class CacheTagService : ICacheTagService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheTagService> _logger;

    public CacheTagService(IMemoryCache cache, ILogger<CacheTagService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

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
                    Priority = CacheItemPriority.NeverRemove
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