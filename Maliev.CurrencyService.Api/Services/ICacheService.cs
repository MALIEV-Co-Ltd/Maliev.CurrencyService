namespace Maliev.CurrencyService.Api.Services;

/// <summary>
/// Two-tier caching service interface (in-process + Redis)
/// </summary>
/// <remarks>
/// Per research.md decision 3: Provides two-tier caching with in-process LRU (sub-5ms)
/// and Redis distributed cache (cross-instance consistency).
/// </remarks>
public interface ICacheService
{
    /// <summary>
    /// Gets a value from cache (checks in-process first, then Redis)
    /// </summary>
    /// <typeparam name="T">Type of the cached value</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached value or null if not found</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Sets a value in cache (both in-process and Redis)
    /// </summary>
    /// <typeparam name="T">Type of the value to cache</typeparam>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="ttl">Time-to-live for the cached value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Removes a value from cache (both in-process and Redis)
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes multiple values from cache by pattern (both in-process and Redis)
    /// </summary>
    /// <param name="pattern">Key pattern (e.g., "rate:USD:*")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key exists in cache (checks both layers)
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if key exists in either cache layer</returns>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
