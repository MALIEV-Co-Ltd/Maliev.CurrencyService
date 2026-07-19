using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class TestsInfra
{
    #region CacheTagService Tests

    public class CacheTagServiceTests
    {
        private readonly MemoryCache _cache;
        private readonly Mock<ILogger<CacheTagService>> _loggerMock;
        private readonly CacheTagService _service;

        public CacheTagServiceTests()
        {
            var options = new MemoryCacheOptions();
            _cache = new MemoryCache(options);
            _loggerMock = new Mock<ILogger<CacheTagService>>();
            _service = new CacheTagService(_cache, _loggerMock.Object);
        }

        [Fact]
        public void AddCacheKeyToTag_NewTag_CreatesTagWithKey()
        {
            var tag = "currencies";
            var cacheKey = "USD:EUR";

            _service.AddCacheKeyToTag(tag, cacheKey);

            var keys = _service.GetCacheKeysByTag(tag);
            Assert.Contains(cacheKey, keys);
        }

        [Fact]
        public void AddCacheKeyToTag_ExistingTag_AddsKeyToExistingSet()
        {
            var tag = "currencies";
            var existingKey = "USD:EUR";
            var newKey = "USD:GBP";

            _service.AddCacheKeyToTag(tag, existingKey);
            _service.AddCacheKeyToTag(tag, newKey);

            var keys = _service.GetCacheKeysByTag(tag);
            Assert.Equal(2, keys.Count());
            Assert.Contains(existingKey, keys);
            Assert.Contains(newKey, keys);
        }

        [Fact]
        public void RemoveCacheKeysByTag_WithKeys_RemovesAllKeysAndTag()
        {
            var tag = "currencies";
            var cacheKey1 = "USD:EUR";
            var cacheKey2 = "USD:GBP";

            _service.AddCacheKeyToTag(tag, cacheKey1);
            _service.AddCacheKeyToTag(tag, cacheKey2);

            _service.RemoveCacheKeysByTag(tag);

            var keys = _service.GetCacheKeysByTag(tag);
            Assert.Empty(keys);
        }

        [Fact]
        public void RemoveCacheKeysByTag_NoKeys_DoesNotThrow()
        {
            var tag = "nonexistent";

            var exception = Record.Exception(() => _service.RemoveCacheKeysByTag(tag));

            Assert.Null(exception);
        }

        [Fact]
        public void GetCacheKeysByTag_ExistingTag_ReturnsKeys()
        {
            var tag = "currencies";
            var cacheKey = "USD:EUR";

            _service.AddCacheKeyToTag(tag, cacheKey);

            var result = _service.GetCacheKeysByTag(tag);

            Assert.Single(result);
            Assert.Contains(cacheKey, result);
        }

        [Fact]
        public void GetCacheKeysByTag_NonExistingTag_ReturnsEmptyEnumerable()
        {
            var tag = "nonexistent";

            var result = _service.GetCacheKeysByTag(tag);

            Assert.Empty(result);
        }

        [Fact]
        public void AddCacheKeyToTag_DifferentTags_AreIsolated()
        {
            var tag1 = "currencies";
            var tag2 = "rates";
            var key1 = "USD:EUR";
            var key2 = "USD:GBP";

            _service.AddCacheKeyToTag(tag1, key1);
            _service.AddCacheKeyToTag(tag2, key2);

            var keys1 = _service.GetCacheKeysByTag(tag1);
            var keys2 = _service.GetCacheKeysByTag(tag2);

            Assert.Single(keys1);
            Assert.Single(keys2);
            Assert.Contains(key1, keys1);
            Assert.Contains(key2, keys2);
        }
    }

    #endregion

    #region CurrencyServiceMetrics Tests

    public class CurrencyServiceMetricsTests
    {
        private readonly Mock<IConfiguration> _configMock;
        private readonly CurrencyServiceMetrics _metrics;

        public CurrencyServiceMetricsTests()
        {
            _configMock = new Mock<IConfiguration>();
            _configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
            _metrics = new CurrencyServiceMetrics(_configMock.Object);
        }

        [Fact]
        public void Constructor_InitializesMetrics_CreatesCountersAndHistograms()
        {
            Assert.NotNull(_metrics);
        }

        [Fact]
        public void RecordCacheHit_IncrementsCounter()
        {
            _metrics.RecordCacheHit();
            _metrics.RecordCacheMiss();

            _metrics.RecordCacheRequest("hit");
        }

        [Fact]
        public void RecordCacheMiss_IncrementsCounter()
        {
            _metrics.RecordCacheMiss();
        }

        [Fact]
        public void RecordProviderCall_RecordsWithTags()
        {
            _metrics.RecordProviderCall("Fawazahmed", "success");
            _metrics.RecordProviderCall("Frankfurter", "error");
        }

        [Fact]
        public void RecordProviderCallDuration_RecordsDuration()
        {
            _metrics.RecordProviderCallDuration("Fawazahmed", 0.5);
            _metrics.RecordProviderCallDuration("Frankfurter", 1.2);
        }

        [Fact]
        public void RecordProviderRequest_RecordsWithCurrencyPair()
        {
            _metrics.RecordProviderRequest("Fawazahmed", "USD:EUR");
            _metrics.RecordProviderRequest("Frankfurter", "THB:USD");
        }

        [Fact]
        public void RecordProviderError_RecordsWithErrorType()
        {
            _metrics.RecordProviderError("Fawazahmed", "timeout");
            _metrics.RecordProviderError("Frankfurter", "invalid_response");
        }

        [Fact]
        public void RecordProviderLatency_RecordsLatency()
        {
            _metrics.RecordProviderLatency("Fawazahmed", 0.3);
            _metrics.RecordProviderLatency("Frankfurter", 0.8);
        }

        [Fact]
        public void RecordProviderFallback_RecordsFallbackEvent()
        {
            _metrics.RecordProviderFallback("Fawazahmed", "Frankfurter");
        }

        [Fact]
        public void RecordHttpRequest_RecordsWithEndpointAndMethod()
        {
            _metrics.RecordHttpRequest("/api/currencies", "GET", "200");
            _metrics.RecordHttpRequest("/api/rates", "POST", "400");
        }

        [Fact]
        public void RecordHttpRequestDuration_RecordsDuration()
        {
            _metrics.RecordHttpRequestDuration("/api/currencies", "GET", 0.15);
            _metrics.RecordHttpRequestDuration("/api/rates", "POST", 0.25);
        }

        [Fact]
        public void RecordTotalRequest_RecordsRequest()
        {
            _metrics.RecordTotalRequest("/api/currencies", "GET");
            _metrics.RecordTotalRequest("/api/rates", "POST");
        }

        [Fact]
        public void RecordFailedRequest_RecordsError()
        {
            _metrics.RecordFailedRequest("/api/currencies", "not_found");
            _metrics.RecordFailedRequest("/api/rates", "validation_error");
        }

        [Fact]
        public void RecordRequestDuration_RecordsDuration()
        {
            _metrics.RecordRequestDuration("/api/currencies", "GET", 0.2);
            _metrics.RecordRequestDuration("/api/rates", "POST", 0.35);
        }

        [Fact]
        public void RecordBackgroundJobExecution_RecordsJobRun()
        {
            _metrics.RecordBackgroundJobExecution("CurrencySyncJob");
            _metrics.RecordBackgroundJobExecution("SnapshotJob");
        }

        [Fact]
        public void RecordBackgroundJobFailure_RecordsFailure()
        {
            _metrics.RecordBackgroundJobFailure("CurrencySyncJob", "timeout");
            _metrics.RecordBackgroundJobFailure("SnapshotJob", "database_error");
        }

        [Fact]
        public void RecordBackgroundJobDuration_RecordsDuration()
        {
            _metrics.RecordBackgroundJobDuration("CurrencySyncJob", 5.5);
            _metrics.RecordBackgroundJobDuration("SnapshotJob", 10.2);
        }

        [Fact]
        public void SetLastBackgroundJobTimestamp_SetsTimestamp()
        {
            _metrics.SetLastBackgroundJobTimestamp("CurrencySyncJob", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _metrics.SetLastBackgroundJobTimestamp("SnapshotJob", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        [Fact]
        public void RecordSnapshotBatchProcessed_IncrementsCounter()
        {
            _metrics.RecordSnapshotBatchProcessed();
            _metrics.RecordSnapshotBatchProcessed();
        }

        [Fact]
        public void RecordSnapshotBatchSize_RecordsSize()
        {
            _metrics.RecordSnapshotBatchSize(100);
            _metrics.RecordSnapshotBatchSize(250);
        }

        [Fact]
        public void RecordSnapshotValidationError_RecordsError()
        {
            _metrics.RecordSnapshotValidationError("invalid_rate");
            _metrics.RecordSnapshotValidationError("missing_field");
        }

        [Fact]
        public void RecordSnapshotRecordsIngested_RecordsCount()
        {
            _metrics.RecordSnapshotRecordsIngested(100);
            _metrics.RecordSnapshotRecordsIngested(250);
        }

        [Fact]
        public void RecordDatabaseQueryDuration_RecordsDuration()
        {
            _metrics.RecordDatabaseQueryDuration("SELECT", 0.05);
            _metrics.RecordDatabaseQueryDuration("INSERT", 0.12);
        }

        [Fact]
        public void RecordDatabaseError_RecordsError()
        {
            _metrics.RecordDatabaseError("SELECT", "timeout");
            _metrics.RecordDatabaseError("INSERT", "constraint_violation");
        }

        [Fact]
        public void RecordDatabaseQuery_RecordsOperation()
        {
            _metrics.RecordDatabaseQuery("SELECT");
            _metrics.RecordDatabaseQuery("INSERT");
            _metrics.RecordDatabaseQuery("UPDATE");
        }

        [Fact]
        public void SetCacheSizeBytes_SetsSize()
        {
            _metrics.SetCacheSizeBytes(1024);
            _metrics.SetCacheSizeBytes(2048);
        }

        [Fact]
        public void RecordCacheRequest_RecordsWithResult()
        {
            _metrics.RecordCacheRequest("hit");
            _metrics.RecordCacheRequest("miss");
            _metrics.RecordCacheRequest("hit");
        }

        [Fact]
        public void RecordCacheInvalidationFailure_IncrementsCounter()
        {
            _metrics.RecordCacheInvalidationFailure();
            _metrics.RecordCacheInvalidationFailure();
        }

        [Fact]
        public void Dispose_DisposesMeter()
        {
            _metrics.Dispose();
            _metrics.Dispose();
        }
    }

    #endregion

    #region ProviderChain Tests

    public class ProviderChainTests
    {
        private readonly Mock<ILogger<ProviderChain>> _loggerMock;
        private readonly Mock<IProviderMetrics> _metricsMock;

        public ProviderChainTests()
        {
            _loggerMock = new Mock<ILogger<ProviderChain>>();
            _metricsMock = new Mock<IProviderMetrics>();
        }

        [Fact]
        public async Task GetRateAsync_FirstProviderSucceeds_ReturnsRate()
        {
            var fromCurrency = "USD";
            var toCurrency = "EUR";
            var expectedRate = new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                Rate = 0.85m,
                Provider = "Fawazahmed",
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var provider1Mock = new Mock<IExchangeRateProvider>();
            provider1Mock.Setup(p => p.ProviderName).Returns("Fawazahmed");
            provider1Mock
                .Setup(p => p.GetRateAsync(fromCurrency, toCurrency, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedRate);

            var providers = new[] { provider1Mock.Object };
            var service = new ProviderChain(providers, _loggerMock.Object, _metricsMock.Object);

            var result = await service.GetRateAsync(fromCurrency, toCurrency);

            Assert.NotNull(result);
            Assert.Equal(expectedRate.Rate, result.Rate);
            Assert.Equal("Fawazahmed", result.Provider);
        }

        [Fact]
        public async Task GetRateAsync_FirstProviderFails_FallbackToSecond()
        {
            var fromCurrency = "USD";
            var toCurrency = "EUR";
            var expectedRate = new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                Rate = 0.85m,
                Provider = "Frankfurter",
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var provider1Mock = new Mock<IExchangeRateProvider>();
            provider1Mock.Setup(p => p.ProviderName).Returns("Fawazahmed");
            provider1Mock
                .Setup(p => p.GetRateAsync(fromCurrency, toCurrency, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExchangeRate?)null);

            var provider2Mock = new Mock<IExchangeRateProvider>();
            provider2Mock.Setup(p => p.ProviderName).Returns("Frankfurter");
            provider2Mock
                .Setup(p => p.GetRateAsync(fromCurrency, toCurrency, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedRate);

            var providers = new[] { provider1Mock.Object, provider2Mock.Object };
            var service = new ProviderChain(providers, _loggerMock.Object, _metricsMock.Object);

            var result = await service.GetRateAsync(fromCurrency, toCurrency);

            Assert.NotNull(result);
            Assert.Equal("Frankfurter", result.Provider);
            _metricsMock.Verify(
                m => m.RecordProviderFallback("Fawazahmed", "Frankfurter"),
                Times.Once);
        }

        [Fact]
        public async Task GetRateAsync_AllProvidersFail_AttemptsTransitiveCalculation()
        {
            var fromCurrency = "THB";
            var toCurrency = "JPY";

            var provider1Mock = new Mock<IExchangeRateProvider>();
            provider1Mock.Setup(p => p.ProviderName).Returns("Fawazahmed");
            provider1Mock
                .Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExchangeRate?)null);

            var provider2Mock = new Mock<IExchangeRateProvider>();
            provider2Mock.Setup(p => p.ProviderName).Returns("Frankfurter");
            provider2Mock
                .Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExchangeRate?)null);

            var providers = new[] { provider1Mock.Object, provider2Mock.Object };
            var service = new ProviderChain(providers, _loggerMock.Object, _metricsMock.Object);

            var result = await service.GetRateAsync(fromCurrency, toCurrency);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetRateAsync_TransitiveCalculation_ViaIntermediary()
        {
            var fromCurrency = "THB";
            var toCurrency = "EUR";
            var usdRate = 0.028m;
            var eurRate = 0.92m;

            var providerMock = new Mock<IExchangeRateProvider>();
            providerMock.Setup(p => p.ProviderName).Returns("Fawazahmed");

            providerMock
                .SetupSequence(p => p.GetRateAsync(fromCurrency, "USD", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExchangeRate
                {
                    Id = Guid.NewGuid(),
                    FromCurrency = fromCurrency,
                    ToCurrency = "USD",
                    Rate = usdRate,
                    Provider = "Fawazahmed",
                    FetchedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            providerMock
                .SetupSequence(p => p.GetRateAsync("USD", toCurrency, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExchangeRate
                {
                    Id = Guid.NewGuid(),
                    FromCurrency = "USD",
                    ToCurrency = toCurrency,
                    Rate = eurRate,
                    Provider = "Fawazahmed",
                    FetchedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            var providers = new[] { providerMock.Object };
            var service = new ProviderChain(providers, _loggerMock.Object, _metricsMock.Object);

            var result = await service.GetRateAsync(fromCurrency, toCurrency);

            Assert.NotNull(result);
            Assert.True(result.IsTransitive);
            Assert.Equal("USD", result.IntermediateCurrency);
            var expectedRate = Math.Round(usdRate * eurRate, 6);
            Assert.Equal(expectedRate, result.Rate);
        }

        [Fact]
        public async Task GetRateAsync_SameCurrency_ReturnsDirect()
        {
            var fromCurrency = "USD";
            var toCurrency = "USD";

            var providerMock = new Mock<IExchangeRateProvider>();
            providerMock.Setup(p => p.ProviderName).Returns("Fawazahmed");
            providerMock
                .Setup(p => p.GetRateAsync(fromCurrency, toCurrency, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExchangeRate
                {
                    Id = Guid.NewGuid(),
                    FromCurrency = fromCurrency,
                    ToCurrency = toCurrency,
                    Rate = 1.0m,
                    Provider = "Fawazahmed",
                    FetchedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            var providers = new[] { providerMock.Object };
            var service = new ProviderChain(providers, _loggerMock.Object, _metricsMock.Object);

            var result = await service.GetRateAsync(fromCurrency, toCurrency);

            Assert.NotNull(result);
            Assert.Equal(1.0m, result.Rate);
        }

        [Fact]
        public async Task GetRateAsync_CancellationToken_IsPassedToProviders()
        {
            var fromCurrency = "USD";
            var toCurrency = "EUR";
            var cts = new CancellationTokenSource();

            var providerMock = new Mock<IExchangeRateProvider>();
            providerMock.Setup(p => p.ProviderName).Returns("Fawazahmed");
            providerMock
                .Setup(p => p.GetRateAsync(fromCurrency, toCurrency, cts.Token))
                .ReturnsAsync(new ExchangeRate
                {
                    Id = Guid.NewGuid(),
                    FromCurrency = fromCurrency,
                    ToCurrency = toCurrency,
                    Rate = 0.85m,
                    Provider = "Fawazahmed",
                    FetchedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            var providers = new[] { providerMock.Object };
            var service = new ProviderChain(providers, _loggerMock.Object, _metricsMock.Object);

            await service.GetRateAsync(fromCurrency, toCurrency, cts.Token);

            providerMock.Verify(
                p => p.GetRateAsync(fromCurrency, toCurrency, cts.Token),
                Times.Once);
        }

        [Fact]
        public async Task GetRateAsync_EmptyProviders_ReturnsNull()
        {
            var fromCurrency = "USD";
            var toCurrency = "EUR";

            var providers = Array.Empty<IExchangeRateProvider>();
            var service = new ProviderChain(providers, _loggerMock.Object, _metricsMock.Object);

            var result = await service.GetRateAsync(fromCurrency, toCurrency);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetRateAsync_TransitiveCalculation_SkipsIntermediaryIfMatchesFromOrTo()
        {
            var fromCurrency = "USD";
            var toCurrency = "EUR";

            var providerMock = new Mock<IExchangeRateProvider>();
            providerMock.Setup(p => p.ProviderName).Returns("Fawazahmed");
            providerMock
                .Setup(p => p.GetRateAsync(fromCurrency, toCurrency, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExchangeRate
                {
                    Id = Guid.NewGuid(),
                    FromCurrency = fromCurrency,
                    ToCurrency = toCurrency,
                    Rate = 0.85m,
                    Provider = "Fawazahmed",
                    FetchedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            var providers = new[] { providerMock.Object };
            var service = new ProviderChain(providers, _loggerMock.Object, _metricsMock.Object);

            var result = await service.GetRateAsync(fromCurrency, toCurrency);

            Assert.NotNull(result);
            Assert.False(result.IsTransitive);
        }

        [Fact]
        public async Task GetRateAsync_TransitiveCalculation_FallsBackToEurWhenUsdFails()
        {
            var fromCurrency = "THB";
            var toCurrency = "GBP";

            var providerMock = new Mock<IExchangeRateProvider>();
            providerMock.Setup(p => p.ProviderName).Returns("Fawazahmed");

            providerMock
                .SetupSequence(p => p.GetRateAsync("THB", "USD", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExchangeRate?)null);

            providerMock
                .SetupSequence(p => p.GetRateAsync("THB", "EUR", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExchangeRate
                {
                    Id = Guid.NewGuid(),
                    FromCurrency = "THB",
                    ToCurrency = "EUR",
                    Rate = 0.025m,
                    Provider = "Fawazahmed",
                    FetchedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            providerMock
                .SetupSequence(p => p.GetRateAsync("EUR", "GBP", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExchangeRate
                {
                    Id = Guid.NewGuid(),
                    FromCurrency = "EUR",
                    ToCurrency = "GBP",
                    Rate = 0.86m,
                    Provider = "Fawazahmed",
                    FetchedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            providerMock
                .SetupSequence(p => p.GetRateAsync("USD", "GBP", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExchangeRate?)null);

            var providers = new[] { providerMock.Object };
            var service = new ProviderChain(providers, _loggerMock.Object, _metricsMock.Object);

            var result = await service.GetRateAsync(fromCurrency, toCurrency);

            Assert.NotNull(result);
            Assert.True(result.IsTransitive);
            Assert.Equal("EUR", result.IntermediateCurrency);
        }
    }

    #endregion
}
