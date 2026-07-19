using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Services;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Testcontainers.PostgreSql;

namespace Maliev.CurrencyService.Tests;

public class RateServiceUnitTestsWithMocks : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer =
#pragma warning disable CS0618
        new PostgreSqlBuilder().WithImage("postgres:18-alpine")
        .Build();

    private readonly Mock<ProviderChain> _providerChainMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<RateService>> _loggerMock;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly Mock<IHostApplicationLifetime> _appLifetimeMock;
    private CurrencyDbContext _context = null!;

    public RateServiceUnitTestsWithMocks()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        _metrics = new CurrencyServiceMetrics(configMock.Object);

        _providerChainMock = new Mock<ProviderChain>(
            new List<IExchangeRateProvider>(),
            new Mock<ILogger<ProviderChain>>().Object,
            (IProviderMetrics)_metrics);
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<RateService>>();
        _appLifetimeMock = new Mock<IHostApplicationLifetime>();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;
        _context = new CurrencyDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null) await _context.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    private RateService CreateService() =>
        new(_providerChainMock.Object, _cacheServiceMock.Object, _context, _loggerMock.Object, _metrics, _appLifetimeMock.Object);

    [Fact]
    public async Task GetLiveRateAsync_ReturnsNull_WhenProviderAndCacheHaveNoData()
    {
        var service = CreateService();
        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);
        _providerChainMock.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);

        var result = await service.GetLiveRateAsync("USD", "EUR");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLiveRateAsync_ReturnsCachedRate_WhenFresh()
    {
        var service = CreateService();
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
        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var result = await service.GetLiveRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal(0.85m, result.Rate);
    }

    [Fact]
    public async Task GetLiveRateAsync_NormalizesCurrencyCodes()
    {
        var service = CreateService();
        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);
        _providerChainMock.Setup(p => p.GetRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.9m,
                Provider = "Test",
                IsTransitive = false,
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            });

        var result = await service.GetLiveRateAsync("usd", "eur");

        Assert.NotNull(result);
        Assert.Equal("USD", result.FromCurrency);
        Assert.Equal("EUR", result.ToCurrency);
    }

    [Fact]
    public async Task GetLiveRateAsync_FetchesFromProvider_WhenCacheMiss()
    {
        var service = CreateService();
        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);
        var fetchedRate = new ExchangeRate
        {
            FromCurrency = "USD",
            ToCurrency = "GBP",
            Rate = 0.75m,
            Provider = "Provider",
            IsTransitive = false,
            FetchedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        _providerChainMock.Setup(p => p.GetRateAsync("USD", "GBP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fetchedRate);

        var result = await service.GetLiveRateAsync("USD", "GBP");

        Assert.NotNull(result);
        Assert.Equal(0.75m, result.Rate);
    }

    [Fact]
    public async Task GetLiveRateAsync_ServesStaleCache_AndTriggersRefresh()
    {
        var service = CreateService();
        var staleCachedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "JPY",
            Rate = 149.5m,
            Timestamp = DateTime.UtcNow.AddMinutes(-6),
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };
        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(staleCachedResponse);
        _providerChainMock.Setup(p => p.GetRateAsync("USD", "JPY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate
            {
                FromCurrency = "USD",
                ToCurrency = "JPY",
                Rate = 150.0m,
                Provider = "Provider",
                IsTransitive = false,
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            });

        var result = await service.GetLiveRateAsync("USD", "JPY");

        Assert.NotNull(result);
        Assert.Equal(150.0m, result.Rate);
    }

    [Fact]
    public async Task GetSnapshotRateAsync_ReturnsNull_WhenNotFound()
    {
        var service = CreateService();
        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);
        _context.RateSnapshots.Add(new RateSnapshot
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = new DateOnly(2024, 1, 1),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await service.GetSnapshotRateAsync("USD", "EUR", new DateOnly(2024, 6, 15));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSnapshotRateAsync_CachesResult_WhenFound()
    {
        var service = CreateService();
        var snapshotDate = new DateOnly(2024, 1, 15);
        _context.RateSnapshots.Add(new RateSnapshot
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.88m,
            SnapshotDate = snapshotDate,
            Source = "Manual",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var result = await service.GetSnapshotRateAsync("USD", "EUR", snapshotDate);

        Assert.NotNull(result);
        Assert.Equal(0.88m, result.Rate);
        _cacheServiceMock.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<ExchangeRateResponse>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRateAsync_AddsToDb_AndInvalidatesCache()
    {
        var service = CreateService();

        await service.UpdateRateAsync("USD", "JPY", 150.0m);

        var dbRate = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.FromCurrency == "USD" && r.ToCurrency == "JPY");
        Assert.NotNull(dbRate);
        Assert.Equal(150.0m, dbRate.Rate);
        _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateRatesAsync_AddsAllRates_AndInvalidatesCache()
    {
        var service = CreateService();
        var updates = new List<UpdateRateRequest>
        {
            new() { From = "USD", To = "EUR", Rate = 0.9m },
            new() { From = "EUR", To = "GBP", Rate = 0.85m },
            new() { From = "GBP", To = "JPY", Rate = 180.0m }
        };

        await service.BulkUpdateRatesAsync(updates);

        var count = await _context.ExchangeRates.CountAsync();
        Assert.Equal(3, count);
        _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}

public class SnapshotServiceUnitTestsWithMocks : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer =
                new PostgreSqlBuilder().WithImage("postgres:18-alpine")
        .Build();

    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<SnapshotService>> _loggerMock;
    private readonly CurrencyServiceMetrics _metrics;
    private CurrencyDbContext _context = null!;

    public SnapshotServiceUnitTestsWithMocks()
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        _metrics = new CurrencyServiceMetrics(configMock.Object);

        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<SnapshotService>>();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;
        _context = new CurrencyDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null) await _context.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    private SnapshotService CreateService() =>
        new(_context, _cacheServiceMock.Object, _loggerMock.Object, _metrics);

    [Fact]
    public async Task ImportBatchAsync_FailsValidation_WhenCurrencyNotFound()
    {
        var service = CreateService();
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true });
        await _context.SaveChangesAsync();

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
    }

    [Fact]
    public async Task ImportBatchAsync_FailsValidation_WhenCurrencyInactive()
    {
        var service = CreateService();
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true });
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2, IsActive = false });
        await _context.SaveChangesAsync();

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

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
    }

    [Fact]
    public async Task ImportBatchAsync_StagesValidSnapshots()
    {
        var service = CreateService();
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true });
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2, IsActive = true });
        await _context.SaveChangesAsync();

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
        var stagedCount = await _context.StagedSnapshots.CountAsync();
        Assert.Equal(1, stagedCount);
    }

    [Fact]
    public async Task ImportBatchAsync_AutoPromotes_WhenEnabled()
    {
        var service = CreateService();
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true });
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2, IsActive = true });
        await _context.SaveChangesAsync();

        var request = new SnapshotBatchRequest
        {
            Source = "Test",
            SnapshotDate = new DateOnly(2024, 1, 1),
            AutoPromote = true,
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m }
            }
        };

        var result = await service.ImportBatchAsync(request);

        Assert.Equal("promoted", result.Status);
        var productionCount = await _context.RateSnapshots.CountAsync();
        Assert.Equal(1, productionCount);
    }

    [Fact]
    public async Task PromoteBatchAsync_ReturnsFalse_WhenInvalidBatchId()
    {
        var service = CreateService();

        var result = await service.PromoteBatchAsync("invalid-guid");

        Assert.False(result);
    }

    [Fact]
    public async Task PromoteBatchAsync_ReturnsFalse_WhenNoValidatedSnapshots()
    {
        var service = CreateService();
        var batchId = Guid.NewGuid();
        _context.StagedSnapshots.Add(new StagedSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = new DateOnly(2024, 1, 1),
            Status = "Failed",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await service.PromoteBatchAsync(batchId.ToString());

        Assert.False(result);
    }

    [Fact]
    public async Task PromoteBatchAsync_MovesToProduction_AndInvalidatesCache()
    {
        var service = CreateService();
        var batchId = Guid.NewGuid();
        _context.StagedSnapshots.Add(new StagedSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = new DateOnly(2024, 1, 1),
            Status = "Validated",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await service.PromoteBatchAsync(batchId.ToString(), "Test");

        Assert.True(result);
        var productionCount = await _context.RateSnapshots.CountAsync();
        Assert.Equal(1, productionCount);
        var stagedCount = await _context.StagedSnapshots.CountAsync();
        Assert.Equal(0, stagedCount);
        _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CleanupOldSnapshotsAsync_DeletesOldSnapshots()
    {
        var service = CreateService();
        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-100));
        var recentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        _context.RateSnapshots.Add(new RateSnapshot { Id = Guid.NewGuid(), FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.85m, SnapshotDate = oldDate, CreatedAt = DateTime.UtcNow });
        _context.RateSnapshots.Add(new RateSnapshot { Id = Guid.NewGuid(), FromCurrency = "USD", ToCurrency = "GBP", Rate = 0.75m, SnapshotDate = recentDate, CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var deletedCount = await service.CleanupOldSnapshotsAsync();

        Assert.Equal(1, deletedCount);
        var remainingCount = await _context.RateSnapshots.CountAsync();
        Assert.Equal(1, remainingCount);
    }

    [Fact]
    public async Task CleanupOldSnapshotsAsync_ReturnsZero_WhenNoOldSnapshots()
    {
        var service = CreateService();
        var recentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        _context.RateSnapshots.Add(new RateSnapshot { Id = Guid.NewGuid(), FromCurrency = "USD", ToCurrency = "EUR", Rate = 0.85m, SnapshotDate = recentDate, CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var deletedCount = await service.CleanupOldSnapshotsAsync();

        Assert.Equal(0, deletedCount);
    }

    [Fact]
    public async Task GetBatchAuditAsync_ReturnsNull_WhenInvalidBatchId()
    {
        var service = CreateService();

        var result = await service.GetBatchAuditAsync("invalid-guid");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBatchAuditAsync_ReturnsNull_WhenBatchNotFound()
    {
        var service = CreateService();

        var result = await service.GetBatchAuditAsync(Guid.NewGuid().ToString());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBatchAuditAsync_ReturnsAuditLog_ForPromotedBatch()
    {
        var service = CreateService();
        var batchId = Guid.NewGuid();
        _context.RateSnapshots.Add(new RateSnapshot
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
        await _context.SaveChangesAsync();

        var result = await service.GetBatchAuditAsync(batchId.ToString());

        Assert.NotNull(result);
        Assert.Equal(1, result.RecordCount);
        Assert.Equal("TestSource", result.Source);
    }

    [Fact]
    public async Task GetBatchAuditAsync_ReturnsAuditLog_ForStagedBatch()
    {
        var service = CreateService();
        var batchId = Guid.NewGuid();
        _context.StagedSnapshots.Add(new StagedSnapshot
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            SnapshotDate = new DateOnly(2024, 1, 1),
            Status = "Validated",
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await service.GetBatchAuditAsync(batchId.ToString());

        Assert.NotNull(result);
        Assert.Equal(1, result.RecordCount);
        Assert.Equal("Staging", result.Source);
    }
}

public class CurrencyServiceUnitTestsWithMocks : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer =
                new PostgreSqlBuilder().WithImage("postgres:18-alpine")
        .Build();
#pragma warning restore CS0618

    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<Infrastructure.Services.CurrencyService>> _loggerMock;
    private CurrencyDbContext _context = null!;

    public CurrencyServiceUnitTestsWithMocks()
    {
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<Infrastructure.Services.CurrencyService>>();
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;
        _context = new CurrencyDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null) await _context.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    private Infrastructure.Services.CurrencyService CreateService() =>
        new(_context, _cacheServiceMock.Object, _loggerMock.Object);

    [Fact]
    public async Task GetAllAsync_ReturnsCachedResponse_WhenAvailable()
    {
        var service = CreateService();
        var cachedResponse = new PaginatedCurrencyResponse
        {
            Items = new List<CurrencyResponse>(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0,
            TotalPages = 0
        };
        _cacheServiceMock.Setup(c => c.GetAsync<PaginatedCurrencyResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var result = await service.GetAllAsync();

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAllAsync_ClampsPageSize_ToMax200()
    {
        var service = CreateService();
        _cacheServiceMock.Setup(c => c.GetAsync<PaginatedCurrencyResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaginatedCurrencyResponse?)null);

        var result = await service.GetAllAsync(pageSize: 500);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByActiveStatus()
    {
        var service = CreateService();
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();
        _cacheServiceMock.Setup(c => c.GetAsync<PaginatedCurrencyResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaginatedCurrencyResponse?)null);

        var result = await service.GetAllAsync(isActive: true);

        Assert.Single(result.Items);
        Assert.Equal("USD", result.Items.First().Code);
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsNull_WhenCodeEmpty()
    {
        var service = CreateService();

        var result = await service.GetByCodeAsync("");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsNull_WhenNotFound()
    {
        var service = CreateService();

        var result = await service.GetByCodeAsync("XYZ");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsCurrency_WhenExists()
    {
        var service = CreateService();
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await service.GetByCodeAsync("USD");

        Assert.NotNull(result);
        Assert.Equal("USD", result.Code);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ReturnsNull_WhenEmpty()
    {
        var service = CreateService();

        var result = await service.GetByCountryCodeAsync("");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ReturnsNull_WhenInvalidFormat()
    {
        var service = CreateService();

        var result = await service.GetByCountryCodeAsync("INVALID");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ReturnsNull_WhenNoMapping()
    {
        var service = CreateService();

        var result = await service.GetByCountryCodeAsync("TH");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ReturnsCurrency_ForIso2()
    {
        var service = CreateService();
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "THB", Symbol = "฿", Name = "Thai Baht", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _context.CountryCurrencies.Add(new CountryCurrency { CountryIso2 = "TH", CountryIso3 = "THA", CurrencyCode = "THB", IsPrimary = true });
        await _context.SaveChangesAsync();

        var result = await service.GetByCountryCodeAsync("TH");

        Assert.NotNull(result);
        Assert.Equal("THB", result.Code);
    }

    [Fact]
    public async Task GetByCountryCodeAsync_ReturnsCurrency_ForIso3()
    {
        var service = CreateService();
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _context.CountryCurrencies.Add(new CountryCurrency { CountryIso2 = "US", CountryIso3 = "USA", CurrencyCode = "USD", IsPrimary = true });
        await _context.SaveChangesAsync();

        var result = await service.GetByCountryCodeAsync("USA");

        Assert.NotNull(result);
        Assert.Equal("USD", result.Code);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenCurrencyExists()
    {
        var service = CreateService();
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var request = new Application.DTOs.Currencies.CreateCurrencyRequest { Code = "USD", Name = "New Dollar", Symbol = "$", DecimalPlaces = 2 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task CreateAsync_CreatesCurrency_AndInvalidatesCache()
    {
        var service = CreateService();
        var request = new Application.DTOs.Currencies.CreateCurrencyRequest { Code = "JPY", Name = "Japanese Yen", Symbol = "¥", DecimalPlaces = 0 };

        var result = await service.CreateAsync(request);

        Assert.NotNull(result);
        Assert.Equal("JPY", result.Code);
        _cacheServiceMock.Verify(c => c.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenNotFound()
    {
        var service = CreateService();

        var result = await service.UpdateAsync("XYZ", new Application.DTOs.Currencies.UpdateCurrencyRequest { Name = "New Name" });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCurrency_AndInvalidatesCache()
    {
        var service = CreateService();
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await service.UpdateAsync("USD", new Application.DTOs.Currencies.UpdateCurrencyRequest { Name = "Updated Dollar" });

        Assert.NotNull(result);
        Assert.Equal("Updated Dollar", result.Name);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        var service = CreateService();

        var result = await service.DeleteAsync("XYZ");

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_AndInvalidatesCache()
    {
        var service = CreateService();
        _context.Currencies.Add(new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await service.DeleteAsync("USD");

        Assert.True(result);
        var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == "USD");
        Assert.False(currency!.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var service = CreateService();

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCurrency_WhenExists()
    {
        var service = CreateService();
        var id = Guid.NewGuid();
        _context.Currencies.Add(new Currency { Id = id, Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await service.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("USD", result.Code);
    }

    [Fact]
    public async Task UpdateByIdAsync_ReturnsNull_WhenNotFound()
    {
        var service = CreateService();

        var result = await service.UpdateByIdAsync(Guid.NewGuid(), new Application.DTOs.Currencies.UpdateCurrencyRequest { Name = "New Name" });

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateByIdAsync_UpdatesCurrency()
    {
        var service = CreateService();
        var id = Guid.NewGuid();
        _context.Currencies.Add(new Currency { Id = id, Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await service.UpdateByIdAsync(id, new Application.DTOs.Currencies.UpdateCurrencyRequest { Name = "Updated Name" });

        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Name);
    }

    [Fact]
    public async Task DeleteByIdAsync_Throws_WhenHasCountryMappings()
    {
        var service = CreateService();
        var id = Guid.NewGuid();
        _context.Currencies.Add(new Currency { Id = id, Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _context.CountryCurrencies.Add(new CountryCurrency { CountryIso2 = "US", CountryIso3 = "USA", CurrencyCode = "USD", IsPrimary = true });
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteByIdAsync(id));
    }

    [Fact]
    public async Task DeleteByIdAsync_SoftDeletes_WhenNoMappings()
    {
        var service = CreateService();
        var id = Guid.NewGuid();
        _context.Currencies.Add(new Currency { Id = id, Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await service.DeleteByIdAsync(id);

        Assert.True(result);
        var currency = await _context.Currencies.FindAsync(id);
        Assert.False(currency!.IsActive);
    }

    [Fact]
    public async Task ActivateAsync_ReturnsFalse_WhenNotFound()
    {
        var service = CreateService();

        var result = await service.ActivateAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task ActivateAsync_SetsIsActiveTrue()
    {
        var service = CreateService();
        var id = Guid.NewGuid();
        _context.Currencies.Add(new Currency { Id = id, Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await service.ActivateAsync(id);

        Assert.True(result);
        var currency = await _context.Currencies.FindAsync(id);
        Assert.True(currency!.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_ReturnsFalse_WhenNotFound()
    {
        var service = CreateService();

        var result = await service.DeactivateAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveFalse()
    {
        var service = CreateService();
        var id = Guid.NewGuid();
        _context.Currencies.Add(new Currency { Id = id, Code = "USD", Symbol = "$", Name = "US Dollar", DecimalPlaces = 2, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await service.DeactivateAsync(id);

        Assert.True(result);
        var currency = await _context.Currencies.FindAsync(id);
        Assert.False(currency!.IsActive);
    }
}




