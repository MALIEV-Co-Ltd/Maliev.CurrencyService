using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Maliev.CurrencyService.Api.HealthChecks;

/// <summary>
/// Health check for cache connectivity (Redis or in-memory)
/// </summary>
/// <remarks>
/// Checks if cache is available and responsive. Returns Healthy when using in-memory cache (test/dev mode)
/// or when Redis is connected. Only returns Degraded if Redis was expected but is unavailable.
/// </remarks>
public class RedisHealthCheck : IHealthCheck
{
    private readonly ILogger<RedisHealthCheck> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IServiceProvider _serviceProvider;

    public RedisHealthCheck(
        ILogger<RedisHealthCheck> logger,
        IHostEnvironment environment,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _environment = environment;
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to resolve IConnectionMultiplexer from DI (may be null if not registered)
            var redis = _serviceProvider.GetService<IConnectionMultiplexer>();

            // If Redis is not configured in test/development environment, return Healthy (using in-memory cache)
            if (redis == null || !redis.IsConnected)
            {
                // In Testing or Development environments, it's expected to use in-memory cache
                if (_environment.IsEnvironment("Testing") || _environment.IsDevelopment())
                {
                    _logger.LogDebug("Redis not configured - using in-memory cache ({Environment} mode)", _environment.EnvironmentName);
                    return HealthCheckResult.Healthy($"Using in-memory cache ({_environment.EnvironmentName} mode)");
                }

                _logger.LogWarning("Redis is not configured or not connected - service running in degraded mode");
                return HealthCheckResult.Degraded("Redis is not configured or not connected - using memory cache only");
            }

            // Try to ping Redis
            var database = redis.GetDatabase();
            var ping = await database.PingAsync();

            return HealthCheckResult.Healthy($"Redis is healthy (ping: {ping.TotalMilliseconds}ms)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed");
            return HealthCheckResult.Degraded($"Redis health check failed: {ex.Message} - using memory cache only", ex);
        }
    }
}
