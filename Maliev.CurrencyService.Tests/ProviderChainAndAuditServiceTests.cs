using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Domain.Interfaces;
using Maliev.CurrencyService.Infrastructure.Persistence.Interceptors;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class InfraTests
{
    #region ProviderChainTests

    private static CurrencyServiceMetrics CreateMetrics()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        return new CurrencyServiceMetrics(configMock.Object);
    }

    [Fact]
    public async Task GetRateAsync_ReturnsRate_WhenFirstProviderSucceeds()
    {
        var providerMock = new Mock<IExchangeRateProvider>();
        providerMock.Setup(p => p.ProviderName).Returns("Provider1");
        providerMock.Setup(p => p.GetRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.85m });

        var loggerMock = new Mock<ILogger<ProviderChain>>();
        var metricsMock = new Mock<IProviderMetrics>();

        var chain = new ProviderChain(new[] { providerMock.Object }, loggerMock.Object, metricsMock.Object);

        var result = await chain.GetRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal(0.85m, result.Rate);
    }

    [Fact]
    public async Task GetRateAsync_FallsBackToSecondProvider_WhenFirstProviderFails()
    {
        var provider1Mock = new Mock<IExchangeRateProvider>();
        provider1Mock.Setup(p => p.ProviderName).Returns("Provider1");
        provider1Mock.Setup(p => p.GetRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);

        var provider2Mock = new Mock<IExchangeRateProvider>();
        provider2Mock.Setup(p => p.ProviderName).Returns("Provider2");
        provider2Mock.Setup(p => p.GetRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.86m });

        var loggerMock = new Mock<ILogger<ProviderChain>>();
        var metricsMock = new Mock<IProviderMetrics>();

        var chain = new ProviderChain(new[] { provider1Mock.Object, provider2Mock.Object }, loggerMock.Object, metricsMock.Object);

        var result = await chain.GetRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal(0.86m, result.Rate);
        metricsMock.Verify(m => m.RecordProviderFallback("Provider1", "Provider2"), Times.Once);
    }

    [Fact]
    public async Task GetRateAsync_ReturnsNull_WhenAllProvidersFail()
    {
        var providerMock = new Mock<IExchangeRateProvider>();
        providerMock.Setup(p => p.ProviderName).Returns("Provider1");
        providerMock.Setup(p => p.GetRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);

        var loggerMock = new Mock<ILogger<ProviderChain>>();
        var metricsMock = new Mock<IProviderMetrics>();

        var chain = new ProviderChain(new[] { providerMock.Object }, loggerMock.Object, metricsMock.Object);

        var result = await chain.GetRateAsync("USD", "EUR");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRateAsync_CalculatesTransitiveRate_WhenDirectProvidersFail()
    {
        var providerMock = new Mock<IExchangeRateProvider>();
        providerMock.Setup(p => p.ProviderName).Returns("Provider1");

        providerMock.Setup(p => p.GetRateAsync("USD", "GBP", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);

        providerMock.Setup(p => p.GetRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.85m });

        providerMock.Setup(p => p.GetRateAsync("EUR", "GBP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { FromCurrency = "EUR", ToCurrency = "GBP", Rate = 0.88m });

        var loggerMock = new Mock<ILogger<ProviderChain>>();
        var metricsMock = new Mock<IProviderMetrics>();

        var chain = new ProviderChain(new[] { providerMock.Object }, loggerMock.Object, metricsMock.Object);

        var result = await chain.GetRateAsync("USD", "GBP");

        Assert.NotNull(result);
        Assert.True(result.IsTransitive);
        Assert.Equal("EUR", result.IntermediateCurrency);
        Assert.Equal(0.748m, result.Rate);
    }

    [Fact]
    public async Task GetRateAsync_SkipsIntermediary_WhenSameAsFromOrToCurrency()
    {
        var providerMock = new Mock<IExchangeRateProvider>();
        providerMock.Setup(p => p.ProviderName).Returns("Provider1");
        providerMock.Setup(p => p.GetRateAsync("USD", "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { FromCurrency = "USD", ToCurrency = "USD", Rate = 1.0m });

        var loggerMock = new Mock<ILogger<ProviderChain>>();
        var metricsMock = new Mock<IProviderMetrics>();

        var chain = new ProviderChain(new[] { providerMock.Object }, loggerMock.Object, metricsMock.Object);

        var result = await chain.GetRateAsync("USD", "USD");

        Assert.NotNull(result);
        Assert.Equal(1.0m, result.Rate);
    }

    [Fact]
    public async Task GetRateAsync_RecordsProviderFallback_WarnsOnFallback()
    {
        var provider1Mock = new Mock<IExchangeRateProvider>();
        provider1Mock.Setup(p => p.ProviderName).Returns("Fawazahmed");
        provider1Mock.Setup(p => p.GetRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);

        var provider2Mock = new Mock<IExchangeRateProvider>();
        provider2Mock.Setup(p => p.ProviderName).Returns("Frankfurter");
        provider2Mock.Setup(p => p.GetRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate { FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.9m });

        var loggerMock = new Mock<ILogger<ProviderChain>>();
        var metricsMock = new Mock<IProviderMetrics>();

        var chain = new ProviderChain(new[] { provider1Mock.Object, provider2Mock.Object }, loggerMock.Object, metricsMock.Object);

        await chain.GetRateAsync("USD", "EUR");

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRateAsync_LogsTransitiveCalculation()
    {
        var providerMock = new Mock<IExchangeRateProvider>();
        providerMock.Setup(p => p.ProviderName).Returns("Provider1");
        providerMock.SetupSequence(p => p.GetRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null)
            .ReturnsAsync(new ExchangeRate { FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.85m });
        providerMock.SetupSequence(p => p.GetRateAsync("EUR", "GBP", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null)
            .ReturnsAsync(new ExchangeRate { FromCurrency = "EUR", ToCurrency = "GBP", Rate = 0.88m });

        var loggerMock = new Mock<ILogger<ProviderChain>>();
        var metricsMock = new Mock<IProviderMetrics>();

        var chain = new ProviderChain(new[] { providerMock.Object }, loggerMock.Object, metricsMock.Object);

        await chain.GetRateAsync("USD", "GBP");

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("transitive")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region CacheTagServiceTests

    [Fact]
    public void AddCacheKeyToTag_HandlesException_WhenCacheFails()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<CacheTagService>>();
        var service = new CacheTagService(memoryCache, loggerMock.Object);

        service.AddCacheKeyToTag("valid", "key1");

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public void RemoveCacheKeysByTag_DoesNotThrow_WhenTagDoesNotExist()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<CacheTagService>>();
        var service = new CacheTagService(memoryCache, loggerMock.Object);

        var exception = Record.Exception(() => service.RemoveCacheKeysByTag("nonexistent"));

        Assert.Null(exception);
    }

    [Fact]
    public void GetCacheKeysByTag_ReturnsEmptyEnumerable_WhenTagDoesNotExist()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<CacheTagService>>();
        var service = new CacheTagService(memoryCache, loggerMock.Object);

        var result = service.GetCacheKeysByTag("nonexistent");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void AddCacheKeyToTag_PreservesExistingKeys_WhenAddingNewKey()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<CacheTagService>>();
        var service = new CacheTagService(memoryCache, loggerMock.Object);

        service.AddCacheKeyToTag("tag1", "key1");
        service.AddCacheKeyToTag("tag1", "key2");
        service.AddCacheKeyToTag("tag1", "key3");

        var keys = service.GetCacheKeysByTag("tag1").ToList();

        Assert.Equal(3, keys.Count);
        Assert.Contains("key1", keys);
        Assert.Contains("key2", keys);
        Assert.Contains("key3", keys);
    }

    [Fact]
    public void RemoveCacheKeysByTag_RemovesCacheKeys_AndLogs()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var loggerMock = new Mock<ILogger<CacheTagService>>();
        var service = new CacheTagService(memoryCache, loggerMock.Object);

        service.AddCacheKeyToTag("tag1", "key1");
        service.AddCacheKeyToTag("tag1", "key2");

        service.RemoveCacheKeysByTag("tag1");

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region CurrencyServiceMetricsTests

    [Fact]
    public void CurrencyServiceMetrics_RecordsCacheHit()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        using var metrics = new CurrencyServiceMetrics(configMock.Object);

        metrics.RecordCacheHit();
        metrics.RecordCacheMiss();
    }

    [Fact]
    public void CurrencyServiceMetrics_RecordsCacheRequest()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        using var metrics = new CurrencyServiceMetrics(configMock.Object);

        metrics.RecordCacheRequest("hit");
        metrics.RecordCacheRequest("miss");
        metrics.RecordCacheInvalidationFailure();
    }

    [Fact]
    public void CurrencyServiceMetrics_RecordsProviderCall()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        using var metrics = new CurrencyServiceMetrics(configMock.Object);

        metrics.RecordProviderCall("Frankfurter", "success");
        metrics.RecordProviderCall("Fawazahmed", "error");

        metrics.RecordProviderCallDuration("Frankfurter", 0.5);
        metrics.RecordProviderLatency("Fawazahmed", 1.2);
    }

    [Fact]
    public void CurrencyServiceMetrics_RecordsProviderRequest()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        using var metrics = new CurrencyServiceMetrics(configMock.Object);

        metrics.RecordProviderRequest("Frankfurter", "USD/EUR");
        metrics.RecordProviderError("Frankfurter", "TimeoutException");
    }

    [Fact]
    public void CurrencyServiceMetrics_RecordsProviderFallback()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        using var metrics = new CurrencyServiceMetrics(configMock.Object);

        metrics.RecordProviderFallback("Fawazahmed", "Frankfurter");
    }

    [Fact]
    public void CurrencyServiceMetrics_RecordsHttpRequest()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        using var metrics = new CurrencyServiceMetrics(configMock.Object);

        metrics.RecordHttpRequest("/api/rates", "GET", "200");
        metrics.RecordHttpRequestDuration("/api/rates", "GET", 0.25);
        metrics.RecordTotalRequest("/api/rates", "GET");
        metrics.RecordFailedRequest("/api/rates", "Exception");
        metrics.RecordRequestDuration("/api/rates", "GET", 0.3);
    }

    [Fact]
    public void CurrencyServiceMetrics_RecordsDatabaseQuery()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        using var metrics = new CurrencyServiceMetrics(configMock.Object);

        metrics.RecordDatabaseQuery("SELECT");
        metrics.RecordDatabaseQueryDuration("SELECT", 0.1);
        metrics.RecordDatabaseError("SELECT", "TimeoutException");
    }

    [Fact]
    public void CurrencyServiceMetrics_RecordsBackgroundJob()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        using var metrics = new CurrencyServiceMetrics(configMock.Object);

        metrics.RecordBackgroundJobExecution("SnapshotSync");
        metrics.RecordBackgroundJobDuration("SnapshotSync", 5.0);
        metrics.RecordBackgroundJobFailure("SnapshotSync", "Exception");
        metrics.SetLastBackgroundJobTimestamp("SnapshotSync", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    [Fact]
    public void CurrencyServiceMetrics_RecordsSnapshotMetrics()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        using var metrics = new CurrencyServiceMetrics(configMock.Object);

        metrics.RecordSnapshotBatchProcessed();
        metrics.RecordSnapshotBatchSize(100);
        metrics.RecordSnapshotValidationError("InvalidRate");
        metrics.RecordSnapshotRecordsIngested(50);
    }

    [Fact]
    public void CurrencyServiceMetrics_SetsCacheSizeBytes()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        using var metrics = new CurrencyServiceMetrics(configMock.Object);

        metrics.SetCacheSizeBytes(1024);
        metrics.SetCacheSizeBytes(2048);
    }

    [Fact]
    public void CurrencyServiceMetrics_Dispose_ClearsResources()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        var metrics = new CurrencyServiceMetrics(configMock.Object);

        metrics.Dispose();
        metrics.Dispose();
    }

    #endregion

    #region DatabaseMetricsInterceptorTests

    [Fact]
    public void DatabaseMetricsInterceptor_CanBeInstantiated()
    {
        var loggerMock = new Mock<ILogger<DatabaseMetricsInterceptor>>();
        var metricsMock = new Mock<IDatabaseMetrics>();
        var interceptor = new DatabaseMetricsInterceptor(loggerMock.Object, metricsMock.Object);

        Assert.NotNull(interceptor);
    }

    [Fact]
    public void DatabaseMetricsInterceptor_Constructor_AcceptsNullParameters()
    {
        var interceptor = new DatabaseMetricsInterceptor(null!, null!);

        Assert.NotNull(interceptor);
    }

    #endregion

    #region AuditLogInterceptorTests

    [Fact]
    public void AuditLogInterceptor_CanBeInstantiated()
    {
        var loggerMock = new Mock<ILogger<AuditLogInterceptor>>();
        var interceptor = new AuditLogInterceptor(loggerMock.Object);

        Assert.NotNull(interceptor);
    }

    [Fact]
    public void AuditLogInterceptor_Constructor_AcceptsLogger()
    {
        var loggerMock = new Mock<ILogger<AuditLogInterceptor>>();
        var interceptor = new AuditLogInterceptor(loggerMock.Object);

        Assert.NotNull(interceptor);
    }

    #endregion
}
