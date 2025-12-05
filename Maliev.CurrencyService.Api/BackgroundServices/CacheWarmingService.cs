using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Services;

namespace Maliev.CurrencyService.Api.BackgroundServices;

/// <summary>
/// Background service for warming cache with top currency pairs on startup
/// </summary>
/// <remarks>
/// Per research.md decision 5: Startup-only warming of top 20 pairs.
/// Covers ~80% of traffic (Pareto principle) with minimal startup delay (3-5 seconds).
/// Runs as a true background task to avoid blocking application startup.
/// </remarks>
public class CacheWarmingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CacheWarmingService> _logger;
    private readonly CurrencyServiceMetrics _metrics;

    // Top 20 currency pairs covering ~80% of traffic (per research.md decision 5)
    private static readonly string[] TopPairs = new[]
    {
        "USD:THB", "EUR:THB", "GBP:THB", "JPY:THB", "CNY:THB",
        "USD:EUR", "USD:GBP", "USD:JPY", "EUR:USD", "GBP:USD",
        "THB:USD", "THB:EUR", "THB:GBP", "THB:JPY", "THB:CNY",
        "EUR:GBP", "EUR:JPY", "GBP:JPY", "USD:CNY", "EUR:CNY"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheWarmingService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider to create scoped services.</param>
    /// <param name="logger">The logger for this service.</param>
    /// <param name="metrics">The application metrics.</param>
    public CacheWarmingService(
        IServiceProvider serviceProvider,
        ILogger<CacheWarmingService> logger,
        CurrencyServiceMetrics metrics)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Executes the cache warming logic.
    /// </summary>
    /// <param name="stoppingToken">A cancellation token to stop the service.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cache warming started for {Count} top currency pairs", TopPairs.Length);
        _metrics.RecordBackgroundJobExecution("cache_warming");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Create a scope for scoped services
            using var scope = _serviceProvider.CreateScope();
            var rateService = scope.ServiceProvider.GetRequiredService<IRateService>();

            var successCount = 0;
            var failureCount = 0;

            // Warm cache for each pair
            foreach (var pair in TopPairs)
            {
                // Check for cancellation before processing each pair
                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Cache warming cancelled after {Success} successful pairs", successCount);
                    break;
                }

                try
                {
                    var parts = pair.Split(':');
                    if (parts.Length != 2)
                    {
                        _logger.LogWarning("Invalid pair format: {Pair}", pair);
                        continue;
                    }

                    var from = parts[0];
                    var to = parts[1];

                    var rate = await rateService.GetLiveRateAsync(from, to, stoppingToken);

                    if (rate != null)
                    {
                        successCount++;
                        _logger.LogDebug("Warmed cache for {Pair}: {Rate}", pair, rate.Rate);
                    }
                    else
                    {
                        failureCount++;
                        _logger.LogWarning("Failed to warm cache for {Pair}: rate unavailable", pair);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancellation during rate fetch is expected during shutdown
                    _logger.LogInformation("Cache warming cancelled during pair {Pair} fetch", pair);
                    break;
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, "Error warming cache for pair {Pair}", pair);
                }

                // Brief delay to avoid overwhelming providers
                try
                {
                    await Task.Delay(100, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation during delay is expected during shutdown
                    _logger.LogInformation("Cache warming cancelled after {Success} successful pairs", successCount);
                    break;
                }
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "Cache warming completed: {Success} successful, {Failures} failed, {Elapsed}ms total",
                successCount, failureCount, stopwatch.ElapsedMilliseconds);

            _metrics.RecordBackgroundJobDuration("cache_warming", stopwatch.Elapsed.TotalSeconds);

            if (failureCount > 0)
            {
                _metrics.RecordBackgroundJobFailure("cache_warming", "partial_failure");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during application shutdown
            stopwatch.Stop();
            _logger.LogInformation("Cache warming cancelled during startup, elapsed: {Elapsed}ms", stopwatch.ElapsedMilliseconds);
            _metrics.RecordBackgroundJobDuration("cache_warming", stopwatch.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Cache warming failed completely");
            _metrics.RecordBackgroundJobFailure("cache_warming", "complete_failure");
            _metrics.RecordBackgroundJobDuration("cache_warming", stopwatch.Elapsed.TotalSeconds);
        }
    }
}
