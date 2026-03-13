using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Testcontainers.PostgreSql;

namespace Maliev.CurrencyService.Tests;

public class RateServiceCacheTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = 
                #pragma warning disable CS0618
        new PostgreSqlBuilder().WithImage("postgres:18-alpine")
        .Build();

    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<Maliev.CurrencyService.Infrastructure.Services.RateService>> _loggerMock;
    private readonly Mock<IRateServiceMetrics> _metricsMock;
    private readonly Mock<IHostApplicationLifetime> _appLifetimeMock;
    private readonly Mock<ProviderChain> _providerChainMock;
    private CurrencyDbContext _context = null!;
    private Maliev.CurrencyService.Infrastructure.Services.RateService _service = null!;

    public RateServiceCacheTests()
    {
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<Maliev.CurrencyService.Infrastructure.Services.RateService>>();
        _metricsMock = new Mock<IRateServiceMetrics>();
        _appLifetimeMock = new Mock<IHostApplicationLifetime>();

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        var metrics = new CurrencyServiceMetrics(configMock.Object);

        _providerChainMock = new Mock<ProviderChain>(
            new List<IExchangeRateProvider>(),
            new Mock<ILogger<ProviderChain>>().Object,
            (IProviderMetrics)metrics);
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;
        _context = new CurrencyDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _service = new Maliev.CurrencyService.Infrastructure.Services.RateService(
            _providerChainMock.Object,
            _cacheServiceMock.Object,
            _context,
            _loggerMock.Object,
            _metricsMock.Object,
            _appLifetimeMock.Object);
    }

    public async Task DisposeAsync()
    {
        if (_context != null) await _context.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    public async Task GetLiveRateAsync_ReturnsFreshCache_WhenWithinFreshWindow()
    {
        var from = "USD";
        var to = "EUR";
        var cachedResponse = new ExchangeRateResponse
        {
            FromCurrency = from,
            ToCurrency = to,
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow.AddSeconds(-100),
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var result = await _service.GetLiveRateAsync(from, to);

        Assert.NotNull(result);
        Assert.Equal(0.85m, result.Rate);
        _metricsMock.Verify(m => m.RecordCacheHit(), Times.Once);
        _providerChainMock.Verify(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetLiveRateAsync_ReturnsStaleCacheAndTriggersBackgroundRefresh_WhenWithinStaleWindow()
    {
        var from = "USD";
        var to = "EUR";
        var staleCachedResponse = new ExchangeRateResponse
        {
            FromCurrency = from,
            ToCurrency = to,
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow.AddSeconds(-310),
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleCachedResponse);

        _providerChainMock
            .Setup(p => p.GetRateAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate
            {
                FromCurrency = from,
                ToCurrency = to,
                Rate = 0.86m,
                Provider = "Test",
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddSeconds(300)
            });

        var result = await _service.GetLiveRateAsync(from, to);

        Assert.NotNull(result);
        Assert.Equal(0.85m, result.Rate);
    }

    [Fact]
    public async Task GetLiveRateAsync_FetchesFresh_WhenCacheTooOld()
    {
        var from = "USD";
        var to = "EUR";
        var oldCachedResponse = new ExchangeRateResponse
        {
            FromCurrency = from,
            ToCurrency = to,
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow.AddSeconds(-400),
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldCachedResponse);

        _providerChainMock
            .Setup(p => p.GetRateAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate
            {
                FromCurrency = from,
                ToCurrency = to,
                Rate = 0.86m,
                Provider = "Test",
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddSeconds(300)
            });

        var result = await _service.GetLiveRateAsync(from, to);

        Assert.NotNull(result);
        Assert.Equal(0.86m, result.Rate);
    }

    [Fact]
    public async Task GetLiveRateAsync_FallsBackToDb_WhenCacheMiss()
    {
        var from = "USD";
        var to = "EUR";
        var dbRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = from,
            ToCurrency = to,
            Rate = 0.82m,
            Provider = "Test",
            FetchedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(300)
        };

        _context.ExchangeRates.Add(dbRate);
        await _context.SaveChangesAsync();

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var result = await _service.GetLiveRateAsync(from, to);

        Assert.NotNull(result);
        Assert.Equal(0.82m, result.Rate);
    }

    [Fact]
    public async Task GetLiveRateAsync_ReturnsNull_WhenNoRateAvailable()
    {
        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        _providerChainMock
            .Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);

        var result = await _service.GetLiveRateAsync("USD", "EUR");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSnapshotRateAsync_ReturnsFromCache_WhenCacheHit()
    {
        var from = "USD";
        var to = "EUR";
        var date = new DateOnly(2024, 1, 1);
        var cachedResponse = new ExchangeRateResponse
        {
            FromCurrency = from,
            ToCurrency = to,
            Rate = 0.82m,
            Timestamp = DateTime.UtcNow,
            Source = "Snapshot",
            Mode = "snapshot",
            SnapshotDate = date,
            IsTransitive = false
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var result = await _service.GetSnapshotRateAsync(from, to, date);

        Assert.NotNull(result);
        Assert.Equal(0.82m, result.Rate);
        _metricsMock.Verify(m => m.RecordCacheHit(), Times.Once);
    }

    [Fact]
    public async Task GetSnapshotRateAsync_QueriesDb_WhenCacheMiss()
    {
        var from = "USD";
        var to = "EUR";
        var date = new DateOnly(2024, 1, 1);
        var snapshot = new RateSnapshot
        {
            Id = Guid.NewGuid(),
            FromCurrency = from,
            ToCurrency = to,
            Rate = 0.82m,
            SnapshotDate = date,
            Source = "Manual",
            CreatedAt = DateTime.UtcNow
        };
        _context.RateSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var result = await _service.GetSnapshotRateAsync(from, to, date);

        Assert.NotNull(result);
        Assert.Equal(0.82m, result.Rate);
        Assert.Equal("snapshot", result.Mode);
        _cacheServiceMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<ExchangeRateResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSnapshotRateAsync_ReturnsNull_WhenNotFound()
    {
        _cacheServiceMock
            .Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var result = await _service.GetSnapshotRateAsync("USD", "EUR", new DateOnly(2024, 1, 1));

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateRateAsync_AddsToDbAndInvalidatesCache()
    {
        await _service.UpdateRateAsync("USD", "EUR", 0.88m);

        var dbRate = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.FromCurrency == "USD" && r.ToCurrency == "EUR");
        Assert.NotNull(dbRate);
        Assert.Equal(0.88m, dbRate.Rate);
        Assert.Equal("ManualAdmin", dbRate.Provider);

        _cacheServiceMock.Verify(c => c.RemoveAsync(It.Is<string>(s => s.Contains("USD") && s.Contains("EUR")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateRatesAsync_ProcessesAllRates()
    {
        var updates = new List<UpdateRateRequest>
        {
            new() { From = "USD", To = "EUR", Rate = 0.9m },
            new() { From = "EUR", To = "GBP", Rate = 0.85m },
            new() { From = "GBP", To = "USD", Rate = 1.25m }
        };

        await _service.BulkUpdateRatesAsync(updates);

        var count = await _context.ExchangeRates.CountAsync();
        Assert.Equal(3, count);

        _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}

public class SnapshotServiceBatchTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = 
                new PostgreSqlBuilder().WithImage("postgres:18-alpine")
        .Build();

    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<Maliev.CurrencyService.Infrastructure.Services.SnapshotService>> _loggerMock;
    private readonly Mock<IRateServiceMetrics> _metricsMock;
    private CurrencyDbContext _context = null!;
    private Maliev.CurrencyService.Infrastructure.Services.SnapshotService _service = null!;

    public SnapshotServiceBatchTests()
    {
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<Maliev.CurrencyService.Infrastructure.Services.SnapshotService>>();
        _metricsMock = new Mock<IRateServiceMetrics>();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;
        _context = new CurrencyDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _service = new Maliev.CurrencyService.Infrastructure.Services.SnapshotService(
            _context,
            _cacheServiceMock.Object,
            _loggerMock.Object,
            _metricsMock.Object);
    }

    public async Task DisposeAsync()
    {
        if (_context != null) await _context.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    private async Task SetupCurrencies()
    {
        var currencies = new[]
        {
            new Currency { Id = Guid.NewGuid(), Code = "USD", Name = "US Dollar", Symbol = "$", IsActive = true },
            new Currency { Id = Guid.NewGuid(), Code = "EUR", Name = "Euro", Symbol = "€", IsActive = true },
            new Currency { Id = Guid.NewGuid(), Code = "GBP", Name = "British Pound", Symbol = "£", IsActive = true },
            new Currency { Id = Guid.NewGuid(), Code = "JPY", Name = "Japanese Yen", Symbol = "¥", IsActive = true }
        };
        _context.Currencies.AddRange(currencies);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task ImportBatchAsync_StagesValidSnapshots()
    {
        await SetupCurrencies();

        var request = new SnapshotBatchRequest
        {
            SnapshotDate = new DateOnly(2024, 1, 1),
            Source = "Test",
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m },
                new() { From = "EUR", To = "GBP", Rate = 0.85m }
            },
            AutoPromote = false
        };

        var result = await _service.ImportBatchAsync(request);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal("staged", result.Status);
        Assert.NotNull(result.BatchId);

        var stagedCount = await _context.StagedSnapshots.CountAsync();
        Assert.Equal(2, stagedCount);
    }

    [Fact]
    public async Task ImportBatchAsync_RejectsInvalidCurrencies()
    {
        await SetupCurrencies();

        var request = new SnapshotBatchRequest
        {
            SnapshotDate = new DateOnly(2024, 1, 1),
            Source = "Test",
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m },
                new() { From = "XXX", To = "EUR", Rate = 0.85m }
            },
            AutoPromote = false
        };

        var result = await _service.ImportBatchAsync(request);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.NotNull(result.Errors);
        Assert.True(result.Errors.ContainsKey("XXX:EUR"));
    }

    [Fact]
    public async Task ImportBatchAsync_Promotes_WhenAutoPromoteIsTrue()
    {
        await SetupCurrencies();

        var request = new SnapshotBatchRequest
        {
            SnapshotDate = new DateOnly(2024, 1, 1),
            Source = "Test",
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m }
            },
            AutoPromote = true
        };

        var result = await _service.ImportBatchAsync(request);

        Assert.Equal("promoted", result.Status);

        var productionCount = await _context.RateSnapshots.CountAsync();
        Assert.Equal(1, productionCount);

        var stagedCount = await _context.StagedSnapshots.CountAsync();
        Assert.Equal(0, stagedCount);
    }

    [Fact]
    public async Task PromoteBatchAsync_MovesStagedToProduction()
    {
        await SetupCurrencies();

        var batchId = Guid.NewGuid();
        var staged = new StagedSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = new DateOnly(2024, 1, 1),
            Status = "Validated",
            CreatedAt = DateTime.UtcNow
        };
        _context.StagedSnapshots.Add(staged);
        await _context.SaveChangesAsync();

        var result = await _service.PromoteBatchAsync(batchId.ToString(), "Test");

        Assert.True(result);

        var productionCount = await _context.RateSnapshots.CountAsync();
        Assert.Equal(1, productionCount);

        var stagedCount = await _context.StagedSnapshots.CountAsync();
        Assert.Equal(0, stagedCount);
    }

    [Fact]
    public async Task PromoteBatchAsync_ReturnsFalse_WhenInvalidBatchId()
    {
        var result = await _service.PromoteBatchAsync("invalid-batch-id");

        Assert.False(result);
    }

    [Fact]
    public async Task PromoteBatchAsync_ReturnsFalse_WhenNoStagedSnapshots()
    {
        await SetupCurrencies();

        var result = await _service.PromoteBatchAsync(Guid.NewGuid().ToString());

        Assert.False(result);
    }

    [Fact]
    public async Task PromoteBatchAsync_InvalidatesCache()
    {
        await SetupCurrencies();

        var batchId = Guid.NewGuid();
        var staged = new StagedSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = new DateOnly(2024, 1, 1),
            Status = "Validated",
            CreatedAt = DateTime.UtcNow
        };
        _context.StagedSnapshots.Add(staged);
        await _context.SaveChangesAsync();

        await _service.PromoteBatchAsync(batchId.ToString(), "Test");

        _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CleanupOldSnapshotsAsync_DeletesOldSnapshots()
    {
        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-100));
        var recentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        var oldSnapshot = new RateSnapshot
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = oldDate,
            CreatedAt = DateTime.UtcNow.AddDays(-100)
        };
        var recentSnapshot = new RateSnapshot
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.86m,
            SnapshotDate = recentDate,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        _context.RateSnapshots.AddRange(oldSnapshot, recentSnapshot);
        await _context.SaveChangesAsync();

        var deletedCount = await _service.CleanupOldSnapshotsAsync();

        Assert.Equal(1, deletedCount);
        Assert.Equal(1, await _context.RateSnapshots.CountAsync());
    }

    [Fact]
    public async Task CleanupOldSnapshotsAsync_ReturnsZero_WhenNoOldSnapshots()
    {
        var recentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var snapshot = new RateSnapshot
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = recentDate,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        _context.RateSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        var deletedCount = await _service.CleanupOldSnapshotsAsync();

        Assert.Equal(0, deletedCount);
    }

    [Fact]
    public async Task GetBatchAuditAsync_ReturnsPromotedAudit_WhenPromoted()
    {
        var batchId = Guid.NewGuid();
        var snapshot = new RateSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = new DateOnly(2024, 1, 1),
            Source = "Test",
            CreatedAt = DateTime.UtcNow
        };
        _context.RateSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        var audit = await _service.GetBatchAuditAsync(batchId.ToString());

        Assert.NotNull(audit);
        Assert.Equal(batchId.ToString(), audit.BatchId);
        Assert.Equal(1, audit.RecordCount);
    }

    [Fact]
    public async Task GetBatchAuditAsync_ReturnsStagedAudit_WhenStaged()
    {
        var batchId = Guid.NewGuid();
        var staged = new StagedSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = new DateOnly(2024, 1, 1),
            Status = "Validated",
            CreatedAt = DateTime.UtcNow
        };
        _context.StagedSnapshots.Add(staged);
        await _context.SaveChangesAsync();

        var audit = await _service.GetBatchAuditAsync(batchId.ToString());

        Assert.NotNull(audit);
        Assert.Equal(batchId.ToString(), audit.BatchId);
        Assert.Equal(1, audit.RecordCount);
    }

    [Fact]
    public async Task GetBatchAuditAsync_ReturnsNull_WhenNotFound()
    {
        var audit = await _service.GetBatchAuditAsync(Guid.NewGuid().ToString());

        Assert.Null(audit);
    }
}

public class CurrencyServiceCachingTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = 
                new PostgreSqlBuilder().WithImage("postgres:18-alpine")
        .Build();
#pragma warning restore CS0618

    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<Maliev.CurrencyService.Infrastructure.Services.CurrencyService>> _loggerMock;
    private CurrencyDbContext _context = null!;
    private Maliev.CurrencyService.Infrastructure.Services.CurrencyService _service = null!;

    public CurrencyServiceCachingTests()
    {
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<Maliev.CurrencyService.Infrastructure.Services.CurrencyService>>();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;
        _context = new CurrencyDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _service = new Maliev.CurrencyService.Infrastructure.Services.CurrencyService(
            _context,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    public async Task DisposeAsync()
    {
        if (_context != null) await _context.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    private async Task SetupCurrencies()
    {
        var currencies = new[]
        {
            new Currency { Id = Guid.NewGuid(), Code = "USD", Name = "US Dollar", Symbol = "$", IsActive = true },
            new Currency { Id = Guid.NewGuid(), Code = "EUR", Name = "Euro", Symbol = "€", IsActive = true },
            new Currency { Id = Guid.NewGuid(), Code = "GBP", Name = "British Pound", Symbol = "£", IsActive = false }
        };
        _context.Currencies.AddRange(currencies);

        var countryCurrencies = new[]
        {
            new CountryCurrency { Id = Guid.NewGuid(), CountryIso2 = "US", CountryIso3 = "USA", CurrencyCode = "USD", IsPrimary = true },
            new CountryCurrency { Id = Guid.NewGuid(), CountryIso2 = "GB", CountryIso3 = "GBR", CurrencyCode = "GBP", IsPrimary = true }
        };
        _context.CountryCurrencies.AddRange(countryCurrencies);

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsFromCache_WhenCacheHit()
    {
        await SetupCurrencies();

        var cachedResponse = new PaginatedCurrencyResponse
        {
            Items = new List<CurrencyResponse>(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0,
            TotalPages = 0
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<PaginatedCurrencyResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var result = await _service.GetAllAsync();

        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetAllAsync_QueriesDb_WhenCacheMiss()
    {
        await SetupCurrencies();

        _cacheServiceMock
            .Setup(c => c.GetAsync<PaginatedCurrencyResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaginatedCurrencyResponse?)null);

        var result = await _service.GetAllAsync();

        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count());

        _cacheServiceMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<PaginatedCurrencyResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_AppliesIsActiveFilter()
    {
        await SetupCurrencies();

        _cacheServiceMock
            .Setup(c => c.GetAsync<PaginatedCurrencyResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaginatedCurrencyResponse?)null);

        var result = await _service.GetAllAsync(isActive: true);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ReturnsFromCache_WhenCacheHit()
    {
        await SetupCurrencies();

        var cachedResponse = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Name = "US Dollar",
            Symbol = "$",
            IsActive = true,
            DecimalPlaces = 2,
            IsPrimary = false
        };

        _cacheServiceMock
            .Setup(c => c.GetAsync<CurrencyResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var result = await _service.GetByCountryCodeAsync("US");

        Assert.NotNull(result);
        Assert.Equal("USD", result.Code);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_QueriesDb_WhenCacheMiss()
    {
        await SetupCurrencies();

        _cacheServiceMock
            .Setup(c => c.GetAsync<CurrencyResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await _service.GetByCountryCodeAsync("US");

        Assert.NotNull(result);
        Assert.Equal("USD", result.Code);

        _cacheServiceMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<CurrencyResponse>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ReturnsNull_WhenInvalidCode()
    {
        var result = await _service.GetByCountryCodeAsync("");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ReturnsNull_WhenNotFound()
    {
        await SetupCurrencies();

        _cacheServiceMock
            .Setup(c => c.GetAsync<CurrencyResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await _service.GetByCountryCodeAsync("XX");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCodeAsync_QueriesDb()
    {
        await SetupCurrencies();

        var result = await _service.GetByCodeAsync("USD");

        Assert.NotNull(result);
        Assert.Equal("USD", result.Code);
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsNull_WhenNotFound()
    {
        await SetupCurrencies();

        var result = await _service.GetByCodeAsync("XXX");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAsync_AddsToDbAndInvalidatesCache()
    {
        await SetupCurrencies();

        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "THB",
            Name = "Thai Baht",
            Symbol = "฿",
            DecimalPlaces = 2
        };

        var result = await _service.CreateAsync(request);

        Assert.NotNull(result);
        Assert.Equal("THB", result.Code);

        var dbCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == "THB");
        Assert.NotNull(dbCurrency);

        _cacheServiceMock.Verify(c => c.RemoveByPatternAsync(It.Is<string>(s => s.Contains("currency:list")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenDuplicateCode()
    {
        await SetupCurrencies();

        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "USD",
            Name = "US Dollar",
            Symbol = "$"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(request));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesAndInvalidatesCache()
    {
        await SetupCurrencies();

        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "US Dollar Updated"
        };

        var result = await _service.UpdateAsync("USD", request);

        Assert.NotNull(result);
        Assert.Equal("US Dollar Updated", result.Name);

        _cacheServiceMock.Verify(c => c.RemoveByPatternAsync(It.Is<string>(s => s.Contains("currency:list")), It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(c => c.RemoveByPatternAsync(It.Is<string>(s => s.Contains("country:currency")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotFound()
    {
        await SetupCurrencies();

        var result = await _service.UpdateAsync("XXX", new Application.DTOs.Currencies.UpdateCurrencyRequest { Name = "Test" });

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesAndInvalidatesCache()
    {
        await SetupCurrencies();

        var result = await _service.DeleteAsync("GBP");

        Assert.True(result);

        var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == "GBP");
        Assert.NotNull(currency);
        Assert.False(currency.IsActive);

        _cacheServiceMock.Verify(c => c.RemoveByPatternAsync(It.Is<string>(s => s.Contains("currency:list")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        await SetupCurrencies();

        var result = await _service.DeleteAsync("XXX");

        Assert.False(result);
    }

    [Fact]
    public async Task GetByIdAsync_QueriesDb()
    {
        await SetupCurrencies();

        var currency = await _context.Currencies.FirstAsync(c => c.Code == "USD");

        var result = await _service.GetByIdAsync(currency.Id);

        Assert.NotNull(result);
        Assert.Equal("USD", result.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        await SetupCurrencies();

        var result = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ActivateAsync_ActivatesCurrency()
    {
        await SetupCurrencies();

        var currency = await _context.Currencies.FirstAsync(c => c.Code == "GBP");
        Assert.False(currency.IsActive);

        var result = await _service.ActivateAsync(currency.Id);

        Assert.True(result);

        await _context.Entry(currency).ReloadAsync();
        Assert.True(currency.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_DeactivatesCurrency()
    {
        await SetupCurrencies();

        var currency = await _context.Currencies.FirstAsync(c => c.Code == "USD");
        Assert.True(currency.IsActive);

        var result = await _service.DeactivateAsync(currency.Id);

        Assert.True(result);

        await _context.Entry(currency).ReloadAsync();
        Assert.False(currency.IsActive);
    }

    [Fact]
    public async Task DeleteByIdAsync_Throws_WhenHasCountryMappings()
    {
        await SetupCurrencies();

        var currency = await _context.Currencies.FirstAsync(c => c.Code == "USD");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteByIdAsync(currency.Id));
    }

    [Fact]
    public async Task DeleteByIdAsync_SoftDeletes_WhenNoMappings()
    {
        await SetupCurrencies();

        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            Code = "XXX",
            Name = "Test",
            Symbol = "X",
            IsActive = true
        };
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        var result = await _service.DeleteByIdAsync(currency.Id);

        Assert.True(result);

        await _context.Entry(currency).ReloadAsync();
        Assert.False(currency.IsActive);
    }
}




