using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Services;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Maliev.CurrencyService.Tests.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// Unit tests for <see cref="RateService"/> using real PostgreSQL via Testcontainers.
/// </summary>
public class RateServiceTests : IClassFixture<BaseIntegrationTestFactory<Program, CurrencyDbContext>>, IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, CurrencyDbContext> _factory;
    private readonly Mock<ProviderChain> _providerChainMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<RateService>> _loggerMock;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly Mock<IHostApplicationLifetime> _appLifetimeMock;
    private CurrencyDbContext _context = null!;

    /// <summary>Initializes a new instance of the <see cref="RateServiceTests"/> class.</summary>
    public RateServiceTests(BaseIntegrationTestFactory<Program, CurrencyDbContext> factory)
    {
        _factory = factory;

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

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await _factory.CleanDatabaseAsync();
        _context = _factory.CreateDbContext();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private RateService CreateService() =>
        new RateService(_providerChainMock.Object, _cacheServiceMock.Object, _context, _loggerMock.Object, _metrics, _appLifetimeMock.Object);

    /// <summary>GetLiveRateAsync returns rate from cache when fresh.</summary>
    [Fact]
    public async Task GetLiveRateAsync_ReturnsFromCache_WhenFresh()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var rate = 0.85m;
        var cachedResponse = new ExchangeRateResponse
        {
            FromCurrency = from,
            ToCurrency = to,
            Rate = rate,
            Timestamp = DateTime.UtcNow,
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };

        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var service = CreateService();

        // Act
        var result = await service.GetLiveRateAsync(from, to);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(rate, result.Rate);
    }

    /// <summary>GetSnapshotRateAsync retrieves from DB when cache misses.</summary>
    [Fact]
    public async Task GetSnapshotRateAsync_ReturnsFromDb_WhenCacheMiss()
    {
        // Arrange
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

        _cacheServiceMock.Setup(c => c.GetAsync<ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var service = CreateService();

        // Act
        var result = await service.GetSnapshotRateAsync(from, to, date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.82m, result.Rate);
        Assert.Equal("snapshot", result.Mode);
    }

    /// <summary>UpdateRateAsync persists to DB and invalidates cache.</summary>
    [Fact]
    public async Task UpdateRateAsync_AddsToDbAndInvalidatesCache()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.UpdateRateAsync("USD", "EUR", 0.88m);

        // Assert
        var dbRate = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.FromCurrency == "USD" && r.ToCurrency == "EUR");
        Assert.NotNull(dbRate);
        Assert.Equal(0.88m, dbRate.Rate);
        _cacheServiceMock.Verify(c => c.RemoveAsync(It.Is<string>(s => s.Contains("USD") && s.Contains("EUR")), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>BulkUpdateRatesAsync processes all rates and invalidates cache.</summary>
    [Fact]
    public async Task BulkUpdateRatesAsync_Works()
    {
        // Arrange
        var service = CreateService();
        var updates = new List<UpdateRateRequest>
        {
            new() { From = "USD", To = "EUR", Rate = 0.9m },
            new() { From = "EUR", To = "USD", Rate = 1.1m }
        };

        // Act
        await service.BulkUpdateRatesAsync(updates);

        // Assert
        var count = await _context.ExchangeRates.CountAsync();
        Assert.True(count >= 2);
        _cacheServiceMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
