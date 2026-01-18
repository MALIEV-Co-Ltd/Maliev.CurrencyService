using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Api.Services.External;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class RateServiceTests
{
    private readonly Mock<ProviderChain> _providerChainMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<RateService>> _loggerMock;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly Mock<IHostApplicationLifetime> _appLifetimeMock;
    private readonly CurrencyDbContext _context;

    public RateServiceTests()
    {
        var options = new DbContextOptionsBuilder<CurrencyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new CurrencyDbContext(options);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        _metrics = new CurrencyServiceMetrics(configMock.Object);

        _providerChainMock = new Mock<ProviderChain>(new List<IExchangeRateProvider>(), new Mock<ILogger<ProviderChain>>().Object, _metrics);
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<RateService>>();
        _appLifetimeMock = new Mock<IHostApplicationLifetime>();
    }

    [Fact]
    public async Task GetLiveRateAsync_ReturnsFromCache_WhenFresh()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var rate = 0.85m;
        var cachedResponse = new Maliev.CurrencyService.Api.Models.Rates.ExchangeRateResponse
        {
            FromCurrency = from,
            ToCurrency = to,
            Rate = rate,
            Timestamp = DateTime.UtcNow,
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };

        _cacheServiceMock.Setup(c => c.GetAsync<Maliev.CurrencyService.Api.Models.Rates.ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        var service = new RateService(_providerChainMock.Object, _cacheServiceMock.Object, _context, _loggerMock.Object, _metrics, _appLifetimeMock.Object);

        // Act
        var result = await service.GetLiveRateAsync(from, to);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(rate, result.Rate);
    }

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

        _cacheServiceMock.Setup(c => c.GetAsync<Maliev.CurrencyService.Api.Models.Rates.ExchangeRateResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Maliev.CurrencyService.Api.Models.Rates.ExchangeRateResponse?)null);

        var service = new RateService(_providerChainMock.Object, _cacheServiceMock.Object, _context, _loggerMock.Object, _metrics, _appLifetimeMock.Object);

        // Act
        var result = await service.GetSnapshotRateAsync(from, to, date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.82m, result.Rate);
        Assert.Equal("snapshot", result.Mode);
    }

    [Fact]
    public async Task UpdateRateAsync_AddsToDbAndInvalidatesCache()
    {
        // Arrange
        var service = new RateService(_providerChainMock.Object, _cacheServiceMock.Object, _context, _loggerMock.Object, _metrics, _appLifetimeMock.Object);

        // Act
        await service.UpdateRateAsync("USD", "EUR", 0.88m);

        // Assert
        var dbRate = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.FromCurrency == "USD" && r.ToCurrency == "EUR");
        Assert.NotNull(dbRate);
        Assert.Equal(0.88m, dbRate.Rate);
        _cacheServiceMock.Verify(c => c.RemoveAsync(It.Is<string>(s => s.Contains("USD") && s.Contains("EUR")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkUpdateRatesAsync_Works()
    {
        // Arrange
        var service = new RateService(_providerChainMock.Object, _cacheServiceMock.Object, _context, _loggerMock.Object, _metrics, _appLifetimeMock.Object);
        var updates = new List<Maliev.CurrencyService.Api.Models.Rates.UpdateRateRequest>
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
