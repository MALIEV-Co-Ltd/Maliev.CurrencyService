using Maliev.CurrencyService.Data.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Maliev.CurrencyService.Api.Services.Background;

public class ExchangeRateDataRetentionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExchangeRateDataRetentionService> _logger;
    private readonly TimeSpan _cleanupInterval;
    private readonly TimeSpan _retentionPeriod;

    public ExchangeRateDataRetentionService(
        IServiceProvider serviceProvider,
        ILogger<ExchangeRateDataRetentionService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Default to daily cleanup
        _cleanupInterval = TimeSpan.FromDays(
            configuration.GetValue<int>("ExchangeRate:DataRetention:CleanupIntervalDays", 1));

        // Default to 90 days retention
        _retentionPeriod = TimeSpan.FromDays(
            configuration.GetValue<int>("ExchangeRate:DataRetention:RetentionDays", 90));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Exchange Rate Data Retention Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldExchangeRates(stoppingToken);
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during exchange rate data cleanup");
                // Wait before retrying
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Exchange Rate Data Retention Service stopped");
    }

    private async Task CleanupOldExchangeRates(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting cleanup of old exchange rate records");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();

        try
        {
            // Skip cleanup if using in-memory database (typically in testing)
            var isTesting = dbContext.Database.ProviderName?.Contains("InMemory") == true;
            if (isTesting)
            {
                _logger.LogInformation("Skipping cleanup for in-memory database");
                return;
            }

            var cutoffDate = DateTime.UtcNow - _retentionPeriod;
            var deletedCount = await dbContext.ExchangeRates
                .Where(er => er.FetchedAt < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogInformation(
                "Deleted {DeletedCount} exchange rate records older than {RetentionDays} days", 
                deletedCount, _retentionPeriod.TotalDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old exchange rate records");
            throw;
        }
    }
}