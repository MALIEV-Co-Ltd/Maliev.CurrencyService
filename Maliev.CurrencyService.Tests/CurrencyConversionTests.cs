using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Infrastructure.Services;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Maliev.CurrencyService.Tests.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// Unit tests for currency conversion functionality.
/// </summary>
public class CurrencyConversionTests : IClassFixture<BaseIntegrationTestFactory<Program, CurrencyDbContext>>, IAsyncLifetime
{
    private readonly BaseIntegrationTestFactory<Program, CurrencyDbContext> _factory;
    private CurrencyDbContext _context = null!;

    /// <summary>Initializes a new instance of the <see cref="CurrencyConversionTests"/> class.</summary>
    public CurrencyConversionTests(BaseIntegrationTestFactory<Program, CurrencyDbContext> factory)
    {
        _factory = factory;
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

    /// <summary>ConvertCurrencyAsync returns conversion result when rate is available.</summary>
    [Fact]
    public async Task ConvertCurrencyAsync_Should_Return_Conversion_When_Rate_Available()
    {
        // Arrange - Setup mocks for the provider chain
        var fawazahmedMock = new Mock<IExchangeRateProvider>();
        var frankfurterMock = new Mock<IExchangeRateProvider>();
        
        var expectedRate = 0.0285m; // THB to USD approximate rate
        fawazahmedMock.Setup(x => x.GetRateAsync("THB", "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = "THB",
                ToCurrency = "USD",
                Rate = expectedRate,
                Provider = "Fawazahmed",
                IsTransitive = false,
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var service = CreateRateServiceWithMocks(fawazahmedMock, frankfurterMock);
        var amount = 1000m;

        // Act
        var result = await service.ConvertCurrencyAsync("THB", "USD", amount);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("THB", result!.FromCurrency);
        Assert.Equal("USD", result.ToCurrency);
        Assert.Equal(amount, result.OriginalAmount);
        Assert.Equal(expectedRate, result.ExchangeRate);
        Assert.Equal(amount * expectedRate, result.ConvertedAmount);
        Assert.Equal("Fawazahmed", result.Source);
        Assert.False(result.IsTransitive);
    }

    private RateService CreateRateServiceWithMocks(
        Mock<IExchangeRateProvider> fawazahmedMock,
        Mock<IExchangeRateProvider> frankfurterMock)
    {
        var cacheMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<RateService>>();
        var metricsMock = new Mock<IRateServiceMetrics>();
        var appLifetimeMock = new Mock<IHostApplicationLifetime>();
        var providerMetricsMock = new Mock<IProviderMetrics>();
        var providerLoggerMock = new Mock<ILogger<ProviderChain>>();
        
        appLifetimeMock.Setup(x => x.ApplicationStopping).Returns(CancellationToken.None);

        var providerChain = new ProviderChain(
            new[] { fawazahmedMock.Object, frankfurterMock.Object },
            providerLoggerMock.Object,
            providerMetricsMock.Object);
        
        return new RateService(
            providerChain,
            cacheMock.Object,
            _context,
            loggerMock.Object,
            metricsMock.Object,
            appLifetimeMock.Object);
    }

    /// <summary>ConvertCurrencyAsync returns null when no rate is available.</summary>
    [Fact]
    public async Task ConvertCurrencyAsync_Should_Return_Null_When_Rate_Unavailable()
    {
        // Arrange
        var fawazahmedMock = new Mock<IExchangeRateProvider>();
        var frankfurterMock = new Mock<IExchangeRateProvider>();
        
        fawazahmedMock.Setup(x => x.GetRateAsync("THB", "XXX", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);
        frankfurterMock.Setup(x => x.GetRateAsync("THB", "XXX", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);

        var service = CreateRateServiceWithMocks(fawazahmedMock, frankfurterMock);

        // Act
        var result = await service.ConvertCurrencyAsync("THB", "XXX", 1000m);

        // Assert
        Assert.Null(result);
    }

    /// <summary>ConvertCurrencyAsync handles transitive rates correctly.</summary>
    [Fact]
    public async Task ConvertCurrencyAsync_Should_Handle_Transitive_Rates()
    {
        // Arrange
        var fawazahmedMock = new Mock<IExchangeRateProvider>();
        var frankfurterMock = new Mock<IExchangeRateProvider>();
        
        var expectedRate = 0.024m;
        fawazahmedMock.Setup(x => x.GetRateAsync("THB", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = "THB",
                ToCurrency = "EUR",
                Rate = expectedRate,
                Provider = "Transitive:Fawazahmed",
                IsTransitive = true,
                IntermediateCurrency = "USD",
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var service = CreateRateServiceWithMocks(fawazahmedMock, frankfurterMock);
        var amount = 5000m;

        // Act
        var result = await service.ConvertCurrencyAsync("THB", "EUR", amount);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("THB", result!.FromCurrency);
        Assert.Equal("EUR", result.ToCurrency);
        Assert.True(result.IsTransitive);
        Assert.Equal("USD", result.IntermediateCurrency);
        Assert.Equal(amount * expectedRate, result.ConvertedAmount);
    }

    /// <summary>ConvertCurrencyAsync handles zero amount.</summary>
    [Fact]
    public async Task ConvertCurrencyAsync_Should_Handle_Zero_Amount()
    {
        // Arrange
        var fawazahmedMock = new Mock<IExchangeRateProvider>();
        var frankfurterMock = new Mock<IExchangeRateProvider>();
        
        fawazahmedMock.Setup(x => x.GetRateAsync("THB", "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = "THB",
                ToCurrency = "USD",
                Rate = 0.0285m,
                Provider = "Fawazahmed",
                IsTransitive = false,
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var service = CreateRateServiceWithMocks(fawazahmedMock, frankfurterMock);

        // Act
        var result = await service.ConvertCurrencyAsync("THB", "USD", 0m);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0m, result!.OriginalAmount);
        Assert.Equal(0m, result.ConvertedAmount);
    }

    /// <summary>ConvertCurrencyAsync handles large amounts.</summary>
    [Fact]
    public async Task ConvertCurrencyAsync_Should_Handle_Large_Amounts()
    {
        // Arrange
        var fawazahmedMock = new Mock<IExchangeRateProvider>();
        var frankfurterMock = new Mock<IExchangeRateProvider>();
        
        var expectedRate = 0.0285m;
        fawazahmedMock.Setup(x => x.GetRateAsync("THB", "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = "THB",
                ToCurrency = "USD",
                Rate = expectedRate,
                Provider = "Fawazahmed",
                IsTransitive = false,
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var service = CreateRateServiceWithMocks(fawazahmedMock, frankfurterMock);
        var amount = 1000000000m; // 1 billion THB

        // Act
        var result = await service.ConvertCurrencyAsync("THB", "USD", amount);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(amount, result!.OriginalAmount);
        Assert.Equal(amount * expectedRate, result.ConvertedAmount);
    }

    /// <summary>ConvertCurrencyAsync converts from THB to major currencies.</summary>
    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("CNY")]
    [InlineData("SGD")]
    [InlineData("AUD")]
    [InlineData("HKD")]
    public async Task ConvertCurrencyAsync_Should_Convert_THB_To_Major_Currencies(string targetCurrency)
    {
        // Arrange
        var fawazahmedMock = new Mock<IExchangeRateProvider>();
        var frankfurterMock = new Mock<IExchangeRateProvider>();
        
        var expectedRate = targetCurrency switch
        {
            "USD" => 0.0285m,
            "EUR" => 0.024m,
            "GBP" => 0.022m,
            "JPY" => 4.35m,
            "CNY" => 0.205m,
            "SGD" => 0.038m,
            "AUD" => 0.045m,
            "HKD" => 0.222m,
            _ => 0.0285m
        };

        fawazahmedMock.Setup(x => x.GetRateAsync("THB", targetCurrency, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = "THB",
                ToCurrency = targetCurrency,
                Rate = expectedRate,
                Provider = "Fawazahmed",
                IsTransitive = false,
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var service = CreateRateServiceWithMocks(fawazahmedMock, frankfurterMock);
        var amount = 10000m;

        // Act
        var result = await service.ConvertCurrencyAsync("THB", targetCurrency, amount);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("THB", result!.FromCurrency);
        Assert.Equal(targetCurrency, result.ToCurrency);
        Assert.Equal(amount, result.OriginalAmount);
        Assert.Equal(expectedRate, result.ExchangeRate);
        Assert.Equal(amount * expectedRate, result.ConvertedAmount);
    }

    /// <summary>ConvertCurrencyAsync is case-insensitive for currency codes.</summary>
    [Fact]
    public async Task ConvertCurrencyAsync_Should_Be_Case_Insensitive()
    {
        // Arrange
        var fawazahmedMock = new Mock<IExchangeRateProvider>();
        var frankfurterMock = new Mock<IExchangeRateProvider>();
        
        fawazahmedMock.Setup(x => x.GetRateAsync("THB", "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = "THB",
                ToCurrency = "USD",
                Rate = 0.0285m,
                Provider = "Fawazahmed",
                IsTransitive = false,
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var service = CreateRateServiceWithMocks(fawazahmedMock, frankfurterMock);

        // Act - using lowercase
        var result = await service.ConvertCurrencyAsync("thb", "usd", 1000m);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("THB", result!.FromCurrency);
        Assert.Equal("USD", result.ToCurrency);
    }

    /// <summary>ConversionResult has all required properties.</summary>
    [Fact]
    public void ConversionResult_Has_All_Required_Properties()
    {
        // Arrange & Act
        var result = new ConversionResult
        {
            FromCurrency = "THB",
            ToCurrency = "USD",
            OriginalAmount = 1000m,
            ConvertedAmount = 28.5m,
            ExchangeRate = 0.0285m,
            RateTimestamp = DateTime.UtcNow,
            Source = "Fawazahmed",
            IsTransitive = false,
            IntermediateCurrency = null
        };

        // Assert
        Assert.Equal("THB", result.FromCurrency);
        Assert.Equal("USD", result.ToCurrency);
        Assert.Equal(1000m, result.OriginalAmount);
        Assert.Equal(28.5m, result.ConvertedAmount);
        Assert.Equal(0.0285m, result.ExchangeRate);
        Assert.Equal("Fawazahmed", result.Source);
        Assert.False(result.IsTransitive);
        Assert.Null(result.IntermediateCurrency);
    }
}
