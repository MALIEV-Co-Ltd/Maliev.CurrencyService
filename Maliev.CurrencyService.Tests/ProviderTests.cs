using System.Net;
using System.Text.Json;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class ProviderTests
{
    private readonly Mock<ILogger<FawazahmedProvider>> _fawazLoggerMock;
    private readonly Mock<ILogger<FrankfurterProvider>> _frankLoggerMock;
    private readonly CurrencyServiceMetrics _metrics;

    public ProviderTests()
    {
        _fawazLoggerMock = new Mock<ILogger<FawazahmedProvider>>();
        _frankLoggerMock = new Mock<ILogger<FrankfurterProvider>>();

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        _metrics = new CurrencyServiceMetrics(configMock.Object);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"usd\": {\"eur\": 0.85}}", "0.85")]
    [InlineData(HttpStatusCode.NotFound, "", null)]
    [InlineData(HttpStatusCode.InternalServerError, "", null)]
    [InlineData(HttpStatusCode.TooManyRequests, "", null)]
    public async Task FawazahmedProvider_GetRateAsync_HandlesResponses(HttpStatusCode statusCode, string content, string? expectedRateStr)
    {
        // Arrange
        decimal? expectedRate = expectedRateStr != null ? decimal.Parse(expectedRateStr) : null;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content),
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FawazahmedProvider(httpClient, _fawazLoggerMock.Object, _metrics);

        // Act
        var result = await provider.GetRateAsync("USD", "EUR", CancellationToken.None);

        // Assert
        if (expectedRate.HasValue)
        {
            Assert.NotNull(result);
            Assert.Equal(expectedRate.Value, result.Rate);
            Assert.Equal("Fawazahmed", result.Provider);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task FawazahmedProvider_GetRateAsync_HandlesTimeout()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException());

        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FawazahmedProvider(httpClient, _fawazLoggerMock.Object, _metrics);

        // Act
        var result = await provider.GetRateAsync("USD", "EUR", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "{\"amount\":1.0,\"base\":\"USD\",\"date\":\"2024-01-17\",\"rates\":{\"EUR\":0.85}}", "0.85")]
    [InlineData(HttpStatusCode.NotFound, "", null)]
    [InlineData(HttpStatusCode.InternalServerError, "", null)]
    [InlineData(HttpStatusCode.TooManyRequests, "", null)]
    public async Task FrankfurterProvider_GetRateAsync_HandlesResponses(HttpStatusCode statusCode, string content, string? expectedRateStr)
    {
        // Arrange
        decimal? expectedRate = expectedRateStr != null ? decimal.Parse(expectedRateStr) : null;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content),
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FrankfurterProvider(httpClient, _frankLoggerMock.Object, _metrics);

        // Act
        var result = await provider.GetRateAsync("USD", "EUR", CancellationToken.None);

        // Assert
        if (expectedRate.HasValue)
        {
            Assert.NotNull(result);
            Assert.Equal(expectedRate.Value, result.Rate);
            Assert.Equal("Frankfurter", result.Provider);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task ProviderChain_GetRateAsync_FallsBack()
    {
        // Arrange
        var provider1Mock = new Mock<IExchangeRateProvider>();
        provider1Mock.Setup(p => p.ProviderName).Returns("P1");
        provider1Mock.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Maliev.CurrencyService.Domain.Entities.ExchangeRate?)null);

        var provider2Mock = new Mock<IExchangeRateProvider>();
        provider2Mock.Setup(p => p.ProviderName).Returns("P2");
        provider2Mock.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Maliev.CurrencyService.Domain.Entities.ExchangeRate
            {
                Rate = 1.2m,
                Provider = "P2",
                FromCurrency = "USD",
                ToCurrency = "EUR",
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var loggerMock = new Mock<ILogger<ProviderChain>>();
        var chain = new ProviderChain(new[] { provider1Mock.Object, provider2Mock.Object }, loggerMock.Object, _metrics);

        // Act
        var result = await chain.GetRateAsync("USD", "EUR", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1.2m, result.Rate);
        Assert.Equal("P2", result.Provider);
    }

    [Fact]
    public async Task ProviderChain_GetRateAsync_TransitiveCalculation()
    {
        // Arrange
        var providerMock = new Mock<IExchangeRateProvider>();
        providerMock.Setup(p => p.ProviderName).Returns("P1");

        // Return null for direct GBP:JPY
        providerMock.Setup(p => p.GetRateAsync("GBP", "JPY", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Maliev.CurrencyService.Domain.Entities.ExchangeRate?)null);

        // Return rates for transitive calculation via USD
        providerMock.Setup(p => p.GetRateAsync("GBP", "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Maliev.CurrencyService.Domain.Entities.ExchangeRate
            {
                FromCurrency = "GBP",
                ToCurrency = "USD",
                Rate = 1.25m,
                Provider = "P1",
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        providerMock.Setup(p => p.GetRateAsync("USD", "JPY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Maliev.CurrencyService.Domain.Entities.ExchangeRate
            {
                FromCurrency = "USD",
                ToCurrency = "JPY",
                Rate = 150m,
                Provider = "P1",
                FetchedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        var loggerMock = new Mock<ILogger<ProviderChain>>();
        var chain = new ProviderChain(new[] { providerMock.Object }, loggerMock.Object, _metrics);

        // Act
        var result = await chain.GetRateAsync("GBP", "JPY", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1.25m * 150m, result.Rate);
        Assert.True(result.IsTransitive);
        Assert.Equal("USD", result.IntermediateCurrency);
    }
}
