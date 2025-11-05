using Maliev.CurrencyService.Api.Services;

namespace Maliev.CurrencyService.Api.BackgroundServices;

/// <summary>
/// Background service for automatic cleanup of old snapshots
/// </summary>
/// <remarks>
/// Per FR-RET-001: Runs daily at 2 AM UTC to delete snapshots older than 90 days.
/// Ensures compliance with data retention policy without manual intervention.
/// </remarks>
public class SnapshotCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SnapshotCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24); // Run daily

    public SnapshotCleanupService(
        IServiceProvider serviceProvider,
        ILogger<SnapshotCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Snapshot cleanup service started");

        try
        {
            // Calculate initial delay to run at 2 AM UTC
            var initialDelay = CalculateInitialDelay();
            _logger.LogInformation("First cleanup scheduled in {Delay}", initialDelay);

            await Task.Delay(initialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting scheduled snapshot cleanup");

                    // Create a scope for scoped services
                    using var scope = _serviceProvider.CreateScope();
                    var snapshotService = scope.ServiceProvider.GetRequiredService<ISnapshotService>();

                    var deletedCount = await snapshotService.CleanupOldSnapshotsAsync(stoppingToken);

                    _logger.LogInformation("Scheduled snapshot cleanup completed: {Count} snapshots deleted", deletedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during scheduled snapshot cleanup");
                }

                // Wait 24 hours before next cleanup
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during application shutdown - log gracefully
            _logger.LogInformation("Snapshot cleanup service is shutting down");
        }
        catch (Exception ex)
        {
            // Unexpected errors during background service execution
            _logger.LogError(ex, "Unexpected error in snapshot cleanup service");
            throw;
        }

        _logger.LogInformation("Snapshot cleanup service stopped");
    }

    /// <summary>
    /// Calculates delay to next 2 AM UTC
    /// </summary>
    private static TimeSpan CalculateInitialDelay()
    {
        var now = DateTime.UtcNow;
        var nextRun = DateTime.UtcNow.Date.AddHours(2); // Today at 2 AM

        // If already past 2 AM today, schedule for tomorrow
        if (now >= nextRun)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - now;
    }
}
