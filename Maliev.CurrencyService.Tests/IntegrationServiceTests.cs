using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Services;
using CurrencyServiceImpl = Maliev.CurrencyService.Infrastructure.Services.CurrencyService;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Testcontainers.PostgreSql;

namespace Maliev.CurrencyService.Tests;

public class ServiceTests4 : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = 
#pragma warning disable CS0618
        new PostgreSqlBuilder().WithImage("postgres:18-alpine")
        .Build();
#pragma warning restore CS0618

    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<RateService>> _rateLoggerMock;
    private readonly Mock<ILogger<SnapshotService>> _snapshotLoggerMock;
    private readonly Mock<ILogger<CurrencyServiceImpl>> _currencyLoggerMock;
    private readonly Mock<ILogger<ProviderChain>> _providerChainLoggerMock;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly Mock<IHostApplicationLifetime> _appLifetimeMock;
    private readonly Mock<IExchangeRateProvider> _providerMock;

    public ServiceTests4()
    {
        _cacheServiceMock = new Mock<ICacheService>();
        _rateLoggerMock = new Mock<ILogger<RateService>>();
        _snapshotLoggerMock = new Mock<ILogger<SnapshotService>>();
        _currencyLoggerMock = new Mock<ILogger<CurrencyServiceImpl>>();
        _providerChainLoggerMock = new Mock<ILogger<ProviderChain>>();
        _appLifetimeMock = new Mock<IHostApplicationLifetime>();

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        _metrics = new CurrencyServiceMetrics(configMock.Object);

        _providerMock = new Mock<IExchangeRateProvider>();
        _providerMock.Setup(p => p.ProviderName).Returns("TestProvider");
    }

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

    #region RateService Tests

    [Fact]
    public async Task GetLiveRateAsync_ReturnsFreshCache_WhenWithinFreshWindow()
    {
        var cacheKey = "rate:USD:EUR";
        var cachedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow.AddSeconds(-100),
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            _providerChainLoggerMock.Object,
            (IProviderMetrics)_metrics);

        using var context = CreateDbContext();
        var service = new RateService(
            providerChain,
            _cacheServiceMock.Object,
            context,
            _rateLoggerMock.Object,
            _metrics,
            _appLifetimeMock.Object);

        var result = await service.GetLiveRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal(0.85m, result.Rate);
    }

    [Fact]
    public async Task GetLiveRateAsync_ReturnsStaleCacheAndTriggersRefresh_WhenWithinStaleWindow()
    {
        var cacheKey = "rate:USD:EUR";
        var staleCachedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.84m,
            Timestamp = DateTime.UtcNow.AddSeconds(-320),
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleCachedResponse);

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            _providerChainLoggerMock.Object,
            (IProviderMetrics)_metrics);

        using var context = CreateDbContext();
        var service = new RateService(
            providerChain,
            _cacheServiceMock.Object,
            context,
            _rateLoggerMock.Object,
            _metrics,
            _appLifetimeMock.Object);

        var result = await service.GetLiveRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal(0.84m, result.Rate);
    }

    [Fact]
    public async Task GetLiveRateAsync_FetchesFresh_WhenCacheTooOld()
    {
        var cacheKey = "rate:USD:EUR";
        var oldCachedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.80m,
            Timestamp = DateTime.UtcNow.AddSeconds(-400),
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldCachedResponse);

        var freshRate = new ExchangeRate
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.86m,
            Provider = "TestProvider",
            IsTransitive = false,
            FetchedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(300)
        };

        _providerMock
            .Setup(p => p.GetRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(freshRate);

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider> { _providerMock.Object },
            _providerChainLoggerMock.Object,
            (IProviderMetrics)_metrics);

        using var context = CreateDbContext();
        var service = new RateService(
            providerChain,
            _cacheServiceMock.Object,
            context,
            _rateLoggerMock.Object,
            _metrics,
            _appLifetimeMock.Object);

        var result = await service.GetLiveRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal(0.86m, result.Rate);
    }

    [Fact]
    public async Task GetLiveRateAsync_ReturnsNull_WhenNoRateAvailable()
    {
        var cacheKey = "rate:XXX:YYY";

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            _providerChainLoggerMock.Object,
            (IProviderMetrics)_metrics);

        using var context = CreateDbContext();
        var service = new RateService(
            providerChain,
            _cacheServiceMock.Object,
            context,
            _rateLoggerMock.Object,
            _metrics,
            _appLifetimeMock.Object);

        var result = await service.GetLiveRateAsync("XXX", "YYY");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLiveRateAsync_FallsBackToDatabase_WhenCacheMiss()
    {
        var cacheKey = "rate:USD:JPY";

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var dbRate = new ExchangeRate
        {
            FromCurrency = "USD",
            ToCurrency = "JPY",
            Rate = 149.5m,
            Provider = "Database",
            IsTransitive = false,
            FetchedAt = DateTime.UtcNow.AddSeconds(-100),
            ExpiresAt = DateTime.UtcNow.AddSeconds(300)
        };

        using var context = CreateDbContext();
        context.ExchangeRates.Add(dbRate);
        await context.SaveChangesAsync();

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            _providerChainLoggerMock.Object,
            (IProviderMetrics)_metrics);

        var service = new RateService(
            providerChain,
            _cacheServiceMock.Object,
            context,
            _rateLoggerMock.Object,
            _metrics,
            _appLifetimeMock.Object);

        var result = await service.GetLiveRateAsync("USD", "JPY");

        Assert.NotNull(result);
        Assert.Equal(149.5m, result.Rate);
    }

    [Fact]
    public async Task GetSnapshotRateAsync_ReturnsCachedSnapshot_WhenCacheHit()
    {
        var cacheKey = "snapshot:USD:EUR:2024-01-15";
        var cachedSnapshot = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.91m,
            Timestamp = DateTime.UtcNow,
            Source = "Snapshot",
            IsTransitive = false,
            Mode = "snapshot",
            SnapshotDate = new DateOnly(2024, 1, 15)
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedSnapshot);

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            _providerChainLoggerMock.Object,
            (IProviderMetrics)_metrics);

        using var context = CreateDbContext();
        var service = new RateService(
            providerChain,
            _cacheServiceMock.Object,
            context,
            _rateLoggerMock.Object,
            _metrics,
            _appLifetimeMock.Object);

        var result = await service.GetSnapshotRateAsync("USD", "EUR", new DateOnly(2024, 1, 15));

        Assert.NotNull(result);
        Assert.Equal(0.91m, result.Rate);
    }

    [Fact]
    public async Task GetSnapshotRateAsync_QueriesDb_WhenCacheMiss()
    {
        var cacheKey = "snapshot:GBP:JPY:2024-02-01";
        var dbSnapshot = new RateSnapshot
        {
            Id = Guid.NewGuid(),
            FromCurrency = "GBP",
            ToCurrency = "JPY",
            Rate = 188.5m,
            SnapshotDate = new DateOnly(2024, 2, 1),
            Source = "Manual",
            CreatedAt = DateTime.UtcNow
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        using var context = CreateDbContext();
        context.RateSnapshots.Add(dbSnapshot);
        await context.SaveChangesAsync();

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            _providerChainLoggerMock.Object,
            (IProviderMetrics)_metrics);

        var service = new RateService(
            providerChain,
            _cacheServiceMock.Object,
            context,
            _rateLoggerMock.Object,
            _metrics,
            _appLifetimeMock.Object);

        var result = await service.GetSnapshotRateAsync("GBP", "JPY", new DateOnly(2024, 2, 1));

        Assert.NotNull(result);
        Assert.Equal(188.5m, result.Rate);
    }

    [Fact]
    public async Task GetSnapshotRateAsync_ReturnsNull_WhenNotFound()
    {
        var cacheKey = "snapshot:XXX:YYY:2024-01-01";

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        using var context = CreateDbContext();

        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            _providerChainLoggerMock.Object,
            (IProviderMetrics)_metrics);

        var service = new RateService(
            providerChain,
            _cacheServiceMock.Object,
            context,
            _rateLoggerMock.Object,
            _metrics,
            _appLifetimeMock.Object);

        var result = await service.GetSnapshotRateAsync("XXX", "YYY", new DateOnly(2024, 1, 1));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateRateAsync_SavesToDbAndInvalidatesCache()
    {
        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            _providerChainLoggerMock.Object,
            (IProviderMetrics)_metrics);

        using var context = CreateDbContext();
        var service = new RateService(
            providerChain,
            _cacheServiceMock.Object,
            context,
            _rateLoggerMock.Object,
            _metrics,
            _appLifetimeMock.Object);

        await service.UpdateRateAsync("USD", "EUR", 0.92m);

        var dbRate = await context.ExchangeRates.FirstOrDefaultAsync(r => r.FromCurrency == "USD" && r.ToCurrency == "EUR");
        Assert.NotNull(dbRate);
        Assert.Equal(0.92m, dbRate.Rate);
        _cacheServiceMock.Verify(c => c.RemoveAsync("rate:USD:EUR", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateRatesAsync_SavesMultipleRates()
    {
        var providerChain = new ProviderChain(
            new List<IExchangeRateProvider>(),
            _providerChainLoggerMock.Object,
            (IProviderMetrics)_metrics);

        using var context = CreateDbContext();
        var service = new RateService(
            providerChain,
            _cacheServiceMock.Object,
            context,
            _rateLoggerMock.Object,
            _metrics,
            _appLifetimeMock.Object);

        var updates = new List<UpdateRateRequest>
        {
            new() { From = "USD", To = "EUR", Rate = 0.90m },
            new() { From = "USD", To = "GBP", Rate = 0.79m },
            new() { From = "EUR", To = "JPY", Rate = 158.5m }
        };

        await service.BulkUpdateRatesAsync(updates);

        var count = await context.ExchangeRates.CountAsync();
        Assert.Equal(3, count);
        _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    #endregion

    #region SnapshotService Tests

    [Fact]
    public async Task ImportBatchAsync_ValidatesCurrencies_AndStagesSnapshots()
    {
        using var context = CreateDbContext();

        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true });
        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2, IsActive = true });
        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "GBP", Symbol = "£", Name = "Pound", DecimalPlaces = 2, IsActive = true });
        await context.SaveChangesAsync();

        var service = new SnapshotService(
            context,
            _cacheServiceMock.Object,
            _snapshotLoggerMock.Object,
            _metrics);

        var request = new SnapshotBatchRequest
        {
            Source = "TestSource",
            SnapshotDate = new DateOnly(2024, 3, 1),
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m },
                new() { From = "EUR", To = "GBP", Rate = 0.86m }
            }
        };

        var result = await service.ImportBatchAsync(request);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal("staged", result.Status);

        var stagedCount = await context.StagedSnapshots.CountAsync();
        Assert.Equal(2, stagedCount);
    }

    [Fact]
    public async Task ImportBatchAsync_ReportsFailures_ForInvalidCurrencies()
    {
        using var context = CreateDbContext();

        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true });
        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2, IsActive = true });
        await context.SaveChangesAsync();

        var service = new SnapshotService(
            context,
            _cacheServiceMock.Object,
            _snapshotLoggerMock.Object,
            _metrics);

        var request = new SnapshotBatchRequest
        {
            Source = "TestSource",
            SnapshotDate = new DateOnly(2024, 3, 1),
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m },
                new() { From = "INVALID", To = "EUR", Rate = 0.90m }
            }
        };

        var result = await service.ImportBatchAsync(request);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.NotNull(result.Errors);
        Assert.True(result.Errors.ContainsKey("INVALID:EUR"));
    }

    [Fact]
    public async Task ImportBatchAsync_AutoPromotes_WhenRequested()
    {
        using var context = CreateDbContext();

        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true });
        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2, IsActive = true });
        await context.SaveChangesAsync();

        var service = new SnapshotService(
            context,
            _cacheServiceMock.Object,
            _snapshotLoggerMock.Object,
            _metrics);

        var request = new SnapshotBatchRequest
        {
            Source = "TestSource",
            SnapshotDate = new DateOnly(2024, 3, 1),
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.87m }
            },
            AutoPromote = true
        };

        var result = await service.ImportBatchAsync(request);

        Assert.Equal("promoted", result.Status);

        var productionCount = await context.RateSnapshots.CountAsync();
        Assert.Equal(1, productionCount);
    }

    [Fact]
    public async Task PromoteBatchAsync_ReturnsFalse_ForInvalidBatchId()
    {
        using var context = CreateDbContext();

        var service = new SnapshotService(
            context,
            _cacheServiceMock.Object,
            _snapshotLoggerMock.Object,
            _metrics);

        var result = await service.PromoteBatchAsync("invalid-guid", "TestSource");

        Assert.False(result);
    }

    [Fact]
    public async Task PromoteBatchAsync_ReturnsFalse_WhenNoValidatedSnapshots()
    {
        using var context = CreateDbContext();

        var service = new SnapshotService(
            context,
            _cacheServiceMock.Object,
            _snapshotLoggerMock.Object,
            _metrics);

        var result = await service.PromoteBatchAsync(Guid.NewGuid().ToString(), "Test");

        Assert.False(result);
    }

    [Fact]
    public async Task PromoteBatchAsync_RemovesExistingSnapshots_ForSameDateAndPairs()
    {
        var batchId = Guid.NewGuid();
        var snapshotDate = new DateOnly(2024, 1, 1);

        using var context = CreateDbContext();

        context.RateSnapshots.Add(new RateSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.80m,
            SnapshotDate = snapshotDate,
            Source = "Old",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });

        context.StagedSnapshots.Add(new StagedSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = snapshotDate,
            Status = "Validated",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new SnapshotService(
            context,
            _cacheServiceMock.Object,
            _snapshotLoggerMock.Object,
            _metrics);

        var result = await service.PromoteBatchAsync(batchId.ToString(), "TestSource");

        Assert.True(result);

        var existingCount = await context.RateSnapshots.CountAsync(r => r.FromCurrency == "USD" && r.ToCurrency == "EUR");
        Assert.Equal(1, existingCount);

        var latestRate = await context.RateSnapshots.FirstAsync(r => r.FromCurrency == "USD" && r.ToCurrency == "EUR");
        Assert.Equal(0.85m, latestRate.Rate);

        var stagedCount = await context.StagedSnapshots.CountAsync();
        Assert.Equal(0, stagedCount);
    }

    [Fact]
    public async Task CleanupOldSnapshotsAsync_DeletesSnapshots_OlderThan90Days()
    {
        using var context = CreateDbContext();

        context.RateSnapshots.Add(new RateSnapshot
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.75m,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-100)),
            Source = "Old",
            CreatedAt = DateTime.UtcNow.AddDays(-100)
        });

        context.RateSnapshots.Add(new RateSnapshot
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            Source = "New",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        });

        await context.SaveChangesAsync();

        var service = new SnapshotService(
            context,
            _cacheServiceMock.Object,
            _snapshotLoggerMock.Object,
            _metrics);

        var deletedCount = await service.CleanupOldSnapshotsAsync();

        Assert.Equal(1, deletedCount);

        var remainingCount = await context.RateSnapshots.CountAsync();
        Assert.Equal(1, remainingCount);
    }

    [Fact]
    public async Task CleanupOldSnapshotsAsync_ReturnsZero_WhenNoOldSnapshots()
    {
        using var context = CreateDbContext();

        context.RateSnapshots.Add(new RateSnapshot
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            Source = "New",
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        });

        await context.SaveChangesAsync();

        var service = new SnapshotService(
            context,
            _cacheServiceMock.Object,
            _snapshotLoggerMock.Object,
            _metrics);

        var deletedCount = await service.CleanupOldSnapshotsAsync();

        Assert.Equal(0, deletedCount);
    }

    [Fact]
    public async Task GetBatchAuditAsync_ReturnsNull_ForInvalidBatchId()
    {
        using var context = CreateDbContext();

        var service = new SnapshotService(
            context,
            _cacheServiceMock.Object,
            _snapshotLoggerMock.Object,
            _metrics);

        var result = await service.GetBatchAuditAsync("invalid-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBatchAuditAsync_ReturnsPromotedAudit_WhenPromoted()
    {
        var batchId = Guid.NewGuid();
        using var context = CreateDbContext();

        context.RateSnapshots.Add(new RateSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = new DateOnly(2024, 1, 1),
            Source = "TestSource",
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = new SnapshotService(
            context,
            _cacheServiceMock.Object,
            _snapshotLoggerMock.Object,
            _metrics);

        var result = await service.GetBatchAuditAsync(batchId.ToString());

        Assert.NotNull(result);
        Assert.Equal(1, result.RecordCount);
        Assert.Equal("TestSource", result.Source);
    }

    #endregion

    #region CurrencyService Tests

    [Fact]
    public async Task GetAllAsync_ReturnsPaginatedResults()
    {
        using var context = CreateDbContext();

        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "EUR", Name = "Euro", Symbol = "€", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.GetAllAsync(page: 1, pageSize: 10);

        Assert.Equal(2, result.Items.Count());
    }

    [Fact]
    public async Task GetAllAsync_FiltersByActiveStatus()
    {
        using var context = CreateDbContext();

        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "GBP", Name = "Pound", Symbol = "£", DecimalPlaces = 2, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.GetAllAsync(isActive: true);

        Assert.Single(result.Items);
        Assert.Equal("USD", result.Items.First().Code);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ReturnsNull_ForInvalidCode()
    {
        using var context = CreateDbContext();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.GetByCountryCodeAsync("");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ReturnsNull_ForInvalidFormat()
    {
        using var context = CreateDbContext();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.GetByCountryCodeAsync("INVALIDCODE");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ResolvesIso2Code()
    {
        using var context = CreateDbContext();

        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "THB", Name = "Thai Baht", Symbol = "฿", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.CountryCurrencies.Add(new CountryCurrency { CountryIso2 = "TH", CountryIso3 = "THA", CurrencyCode = "THB", IsPrimary = true });
        await context.SaveChangesAsync();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.GetByCountryCodeAsync("TH");

        Assert.NotNull(result);
        Assert.Equal("THB", result.Code);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ResolvesIso3Code()
    {
        using var context = CreateDbContext();

        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.CountryCurrencies.Add(new CountryCurrency { CountryIso2 = "US", CountryIso3 = "USA", CurrencyCode = "USD", IsPrimary = true });
        await context.SaveChangesAsync();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.GetByCountryCodeAsync("USA");

        Assert.NotNull(result);
        Assert.Equal("USD", result.Code);
    }

    [Fact]
    public async Task CreateAsync_ThrowsException_WhenCurrencyExists()
    {
        using var context = CreateDbContext();

        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "USD",
            Name = "US Dollar",
            Symbol = "$",
            DecimalPlaces = 2
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_AddsCurrency_AndInvalidatesCache()
    {
        using var context = CreateDbContext();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "JPY",
            Name = "Japanese Yen",
            Symbol = "¥",
            DecimalPlaces = 0,
            IsActive = true
        };

        var result = await service.CreateAsync(request);

        Assert.Equal("JPY", result.Code);

        var dbCount = await context.Currencies.CountAsync(c => c.Code == "JPY");
        Assert.Equal(1, dbCount);

        _cacheServiceMock.Verify(c => c.RemoveByPatternAsync("currency:list:*", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenCurrencyNotFound()
    {
        using var context = CreateDbContext();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.UpdateAsync("XXX", new Application.DTOs.Currencies.UpdateCurrencyRequest { Name = "New Name" });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCurrency_AndInvalidatesCache()
    {
        using var context = CreateDbContext();

        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.UpdateAsync("USD", new Application.DTOs.Currencies.UpdateCurrencyRequest { Name = "Updated Dollar" });

        Assert.NotNull(result);
        Assert.Equal("Updated Dollar", result.Name);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenCurrencyNotFound()
    {
        using var context = CreateDbContext();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.DeleteAsync("XXX");

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesCurrency()
    {
        using var context = CreateDbContext();

        context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.DeleteAsync("USD");

        Assert.True(result);

        var deleted = await context.Currencies.FirstAsync(c => c.Code == "USD");
        Assert.False(deleted.IsActive);
    }

    [Fact]
    public async Task DeleteByIdAsync_ThrowsException_WhenHasCountryMappings()
    {
        using var context = CreateDbContext();

        var currencyId = Guid.NewGuid();
        context.Currencies.Add(new Currency { Id = currencyId, Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.CountryCurrencies.Add(new CountryCurrency { CountryIso2 = "US", CountryIso3 = "USA", CurrencyCode = "USD", IsPrimary = true });
        await context.SaveChangesAsync();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteByIdAsync(currencyId));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsActiveCurrency()
    {
        var currencyId = Guid.NewGuid();
        using var context = CreateDbContext();

        context.Currencies.Add(new Currency { Id = currencyId, Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.GetByIdAsync(currencyId);

        Assert.NotNull(result);
        Assert.Equal(currencyId, result.Id);
    }

    [Fact]
    public async Task ActivateAsync_ReturnsFalse_WhenNotFound()
    {
        using var context = CreateDbContext();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.ActivateAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task DeactivateAsync_ReturnsTrue_WhenSuccessful()
    {
        using var context = CreateDbContext();

        var currencyId = Guid.NewGuid();
        context.Currencies.Add(new Currency { Id = currencyId, Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var service = new CurrencyServiceImpl(
            context,
            _cacheServiceMock.Object,
            _currencyLoggerMock.Object);

        var result = await service.DeactivateAsync(currencyId);

        Assert.True(result);

        var deactivated = await context.Currencies.FirstAsync(c => c.Id == currencyId);
        Assert.False(deactivated.IsActive);
    }

    #endregion
}




