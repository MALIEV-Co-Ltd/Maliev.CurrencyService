using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Services;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Testcontainers.PostgreSql;

namespace Maliev.CurrencyService.Tests;

public class InfrastructureTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = 
                #pragma warning disable CS0618
        new PostgreSqlBuilder().WithImage("postgres:18-alpine")
        .Build();
#pragma warning restore CS0618

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
    }

    private CurrencyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;

        var context = new CurrencyDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    #region CacheTagServiceTests

    [Fact]
    public void AddCacheKeyToTag_CreatesNewTag_WhenTagDoesNotExist()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<CacheTagService>>();
        var service = new CacheTagService(memoryCache, loggerMock.Object);

        service.AddCacheKeyToTag("currencies", "rate:USD:EUR");

        var keys = service.GetCacheKeysByTag("currencies");
        Assert.Contains("rate:USD:EUR", keys);
    }

    [Fact]
    public void AddCacheKeyToTag_AddsToExistingTag_WhenTagExists()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<CacheTagService>>();
        var service = new CacheTagService(memoryCache, loggerMock.Object);

        service.AddCacheKeyToTag("currencies", "rate:USD:EUR");
        service.AddCacheKeyToTag("currencies", "rate:USD:GBP");

        var keys = service.GetCacheKeysByTag("currencies").ToList();
        Assert.Equal(2, keys.Count);
        Assert.Contains("rate:USD:EUR", keys);
        Assert.Contains("rate:USD:GBP", keys);
    }

    [Fact]
    public void RemoveCacheKeysByTag_RemovesAllKeys_AndTagItself()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<CacheTagService>>();
        var service = new CacheTagService(memoryCache, loggerMock.Object);

        service.AddCacheKeyToTag("currencies", "rate:USD:EUR");
        service.AddCacheKeyToTag("currencies", "rate:USD:GBP");

        service.RemoveCacheKeysByTag("currencies");

        var keys = service.GetCacheKeysByTag("currencies");
        Assert.Empty(keys);
    }

    [Fact]
    public void GetCacheKeysByTag_ReturnsEmpty_WhenTagDoesNotExist()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<CacheTagService>>();
        var service = new CacheTagService(memoryCache, loggerMock.Object);

        var keys = service.GetCacheKeysByTag("nonexistent");

        Assert.Empty(keys);
    }

    #endregion

    #region SnapshotServiceUnitTests

    [Fact]
    public async Task ImportBatchAsync_ReturnsFailure_WhenCurrencyNotFound()
    {
        using var context = CreateDbContext();
        var cacheServiceMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<SnapshotService>>();
        var metricsMock = new Mock<IRateServiceMetrics>();

        context.Currencies.Add(new Currency { Code = "USD", IsActive = true });
        await context.SaveChangesAsync();

        var service = new SnapshotService(context, cacheServiceMock.Object, loggerMock.Object, metricsMock.Object);

        var request = new SnapshotBatchRequest
        {
            Source = "Test",
            SnapshotDate = new DateOnly(2024, 1, 1),
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "INVALID", Rate = 0.85m }
            }
        };

        var result = await service.ImportBatchAsync(request);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Contains("INVALID", result.Errors!.Keys.First());
    }

    [Fact]
    public async Task ImportBatchAsync_StagesValidSnapshots_WhenCurrenciesExist()
    {
        using var context = CreateDbContext();
        var cacheServiceMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<SnapshotService>>();
        var metricsMock = new Mock<IRateServiceMetrics>();

        context.Currencies.Add(new Currency { Code = "USD", IsActive = true });
        context.Currencies.Add(new Currency { Code = "EUR", IsActive = true });
        await context.SaveChangesAsync();

        var service = new SnapshotService(context, cacheServiceMock.Object, loggerMock.Object, metricsMock.Object);

        var request = new SnapshotBatchRequest
        {
            Source = "Test",
            SnapshotDate = new DateOnly(2024, 1, 1),
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m }
            }
        };

        var result = await service.ImportBatchAsync(request);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal("staged", result.Status);
    }

    [Fact]
    public async Task PromoteBatchAsync_ReturnsFalse_WhenInvalidBatchId()
    {
        using var context = CreateDbContext();
        var cacheServiceMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<SnapshotService>>();
        var metricsMock = new Mock<IRateServiceMetrics>();

        var service = new SnapshotService(context, cacheServiceMock.Object, loggerMock.Object, metricsMock.Object);

        var result = await service.PromoteBatchAsync("invalid-guid");

        Assert.False(result);
    }

    [Fact]
    public async Task CleanupOldSnapshotsAsync_ReturnsZero_WhenNoOldSnapshots()
    {
        using var context = CreateDbContext();
        var cacheServiceMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<SnapshotService>>();
        var metricsMock = new Mock<IRateServiceMetrics>();

        var service = new SnapshotService(context, cacheServiceMock.Object, loggerMock.Object, metricsMock.Object);

        var result = await service.CleanupOldSnapshotsAsync();

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetBatchAuditAsync_ReturnsNull_WhenInvalidBatchId()
    {
        using var context = CreateDbContext();
        var cacheServiceMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<SnapshotService>>();
        var metricsMock = new Mock<IRateServiceMetrics>();

        var service = new SnapshotService(context, cacheServiceMock.Object, loggerMock.Object, metricsMock.Object);

        var result = await service.GetBatchAuditAsync("invalid-guid");

        Assert.Null(result);
    }

    #endregion

    #region RateServiceUnitTests

    private static CurrencyServiceMetrics CreateMetrics()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        return new CurrencyServiceMetrics(configMock.Object);
    }

    [Fact]
    public async Task GetLiveRateAsync_ReturnsCachedRate_WhenFresh()
    {
        var metrics = CreateMetrics();

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            new Mock<ILogger<ProviderChain>>().Object,
            metrics);

        var cacheServiceMock = new Mock<ICacheService>();
        var cachedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };
        cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var loggerMock = new Mock<ILogger<RateService>>();
        var appLifetimeMock = new Mock<IHostApplicationLifetime>();

        using var context = CreateDbContext();

        var service = new RateService(
            providerChain,
            cacheServiceMock.Object,
            context,
            loggerMock.Object,
            metrics,
            appLifetimeMock.Object);

        var result = await service.GetLiveRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal(0.85m, result.Rate);
    }

    [Fact]
    public async Task GetLiveRateAsync_ReturnsNull_WhenNoRateAvailable()
    {
        var metrics = CreateMetrics();

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            new Mock<ILogger<ProviderChain>>().Object,
            metrics);

        var cacheServiceMock = new Mock<ICacheService>();
        cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var loggerMock = new Mock<ILogger<RateService>>();
        var appLifetimeMock = new Mock<IHostApplicationLifetime>();

        using var context = CreateDbContext();

        var service = new RateService(
            providerChain,
            cacheServiceMock.Object,
            context,
            loggerMock.Object,
            metrics,
            appLifetimeMock.Object);

        var result = await service.GetLiveRateAsync("USD", "EUR");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSnapshotRateAsync_ReturnsNull_WhenNotFound()
    {
        var metrics = CreateMetrics();

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            new Mock<ILogger<ProviderChain>>().Object,
            metrics);

        var cacheServiceMock = new Mock<ICacheService>();
        cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var loggerMock = new Mock<ILogger<RateService>>();
        var appLifetimeMock = new Mock<IHostApplicationLifetime>();

        using var context = CreateDbContext();

        var service = new RateService(
            providerChain,
            cacheServiceMock.Object,
            context,
            loggerMock.Object,
            metrics,
            appLifetimeMock.Object);

        var result = await service.GetSnapshotRateAsync("USD", "EUR", new DateOnly(2024, 1, 1));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateRateAsync_InvalidatesCache()
    {
        var metrics = CreateMetrics();

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            new Mock<ILogger<ProviderChain>>().Object,
            metrics);

        var cacheServiceMock = new Mock<ICacheService>();
        cacheServiceMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<RateService>>();
        var appLifetimeMock = new Mock<IHostApplicationLifetime>();

        using var context = CreateDbContext();

        var service = new RateService(
            providerChain,
            cacheServiceMock.Object,
            context,
            loggerMock.Object,
            metrics,
            appLifetimeMock.Object);

        await service.UpdateRateAsync("USD", "EUR", 0.88m);

        cacheServiceMock.Verify(
            c => c.RemoveAsync(It.Is<string>(s => s.Contains("USD") && s.Contains("EUR")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BulkUpdateRatesAsync_RemovesCacheForAllRates()
    {
        var metrics = CreateMetrics();

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            new Mock<ILogger<ProviderChain>>().Object,
            metrics);

        var cacheServiceMock = new Mock<ICacheService>();
        cacheServiceMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<RateService>>();
        var appLifetimeMock = new Mock<IHostApplicationLifetime>();

        using var context = CreateDbContext();

        var service = new RateService(
            providerChain,
            cacheServiceMock.Object,
            context,
            loggerMock.Object,
            metrics,
            appLifetimeMock.Object);

        var updates = new List<UpdateRateRequest>
        {
            new() { From = "USD", To = "EUR", Rate = 0.9m },
            new() { From = "EUR", To = "GBP", Rate = 0.85m }
        };

        await service.BulkUpdateRatesAsync(updates);

        cacheServiceMock.Verify(
            c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    #endregion
}




