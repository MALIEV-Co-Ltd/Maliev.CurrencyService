using System.Net;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Models.Common;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Maliev.CurrencyService.Infrastructure.Services.External;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class ExternalTests
{
    private readonly Mock<ILogger<FawazahmedProvider>> _fawazLoggerMock;
    private readonly Mock<ILogger<FrankfurterProvider>> _frankLoggerMock;
    private readonly Mock<ILogger<ProviderChain>> _chainLoggerMock;
    private readonly Mock<IProviderMetrics> _metricsMock;

    public ExternalTests()
    {
        _fawazLoggerMock = new Mock<ILogger<FawazahmedProvider>>();
        _frankLoggerMock = new Mock<ILogger<FrankfurterProvider>>();
        _chainLoggerMock = new Mock<ILogger<ProviderChain>>();
        _metricsMock = new Mock<IProviderMetrics>();
    }

    #region FawazahmedProvider Tests

    [Fact]
    public async Task FawazahmedProvider_GetRateAsync_SuccessfulResponse_ReturnsExchangeRate()
    {
        var json = "{\"usd\": {\"eur\": 0.92, \"gbp\": 0.79}}";
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, json);
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FawazahmedProvider(httpClient, _fawazLoggerMock.Object, _metricsMock.Object);

        var result = await provider.GetRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal("USD", result.FromCurrency);
        Assert.Equal("EUR", result.ToCurrency);
        Assert.Equal(0.92m, result.Rate);
        Assert.Equal("Fawazahmed", result.Provider);
        Assert.False(result.IsTransitive);
    }

    [Fact]
    public async Task FawazahmedProvider_GetRateAsync_InvalidJson_ReturnsNull()
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "invalid json");
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FawazahmedProvider(httpClient, _fawazLoggerMock.Object, _metricsMock.Object);

        var result = await provider.GetRateAsync("USD", "EUR");

        Assert.Null(result);
    }

    [Fact]
    public async Task FawazahmedProvider_GetRateAsync_HttpError_ReturnsNull()
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.ServiceUnavailable, "");
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FawazahmedProvider(httpClient, _fawazLoggerMock.Object, _metricsMock.Object);

        var result = await provider.GetRateAsync("USD", "EUR");

        Assert.Null(result);
    }

    [Fact]
    public void FawazahmedProvider_SupportsPair_AlwaysReturnsTrue()
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FawazahmedProvider(httpClient, _fawazLoggerMock.Object, _metricsMock.Object);

        Assert.True(provider.SupportsPair("USD", "EUR"));
        Assert.True(provider.SupportsPair("THB", "JPY"));
    }

    [Fact]
    public async Task FawazahmedProvider_GetSupportedCurrenciesAsync_ReturnsEmptySet()
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FawazahmedProvider(httpClient, _fawazLoggerMock.Object, _metricsMock.Object);

        var result = await provider.GetSupportedCurrenciesAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task FawazahmedProvider_GetRateAsync_CaseInsensitive_Currencies()
    {
        var json = "{\"usd\": {\"eur\": 0.85}}";
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, json);
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FawazahmedProvider(httpClient, _fawazLoggerMock.Object, _metricsMock.Object);

        var result = await provider.GetRateAsync("usd", "eur");

        Assert.NotNull(result);
        Assert.Equal("USD", result.FromCurrency);
        Assert.Equal("EUR", result.ToCurrency);
    }

    #endregion

    #region FrankfurterProvider Tests

    [Fact]
    public async Task FrankfurterProvider_GetRateAsync_SuccessfulResponse_ReturnsExchangeRate()
    {
        var json = "{\"amount\":1.0,\"base\":\"USD\",\"date\":\"2024-01-01\",\"rates\":{\"EUR\":0.92}}";
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, json);
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FrankfurterProvider(httpClient, _frankLoggerMock.Object, _metricsMock.Object);

        var result = await provider.GetRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal("USD", result.FromCurrency);
        Assert.Equal("EUR", result.ToCurrency);
        Assert.Equal(0.92m, result.Rate);
        Assert.Equal("Frankfurter", result.Provider);
        Assert.False(result.IsTransitive);
    }

    [Fact]
    public async Task FrankfurterProvider_GetRateAsync_UnsupportedPair_ReturnsNull()
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FrankfurterProvider(httpClient, _frankLoggerMock.Object, _metricsMock.Object);

        var result = await provider.GetRateAsync("XXX", "EUR");

        Assert.Null(result);
    }

    [Fact]
    public async Task FrankfurterProvider_GetRateAsync_HttpError_ReturnsNull()
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.NotFound, "");
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FrankfurterProvider(httpClient, _frankLoggerMock.Object, _metricsMock.Object);

        var result = await provider.GetRateAsync("USD", "EUR");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("USD", "EUR", true)]
    [InlineData("USD", "THB", true)]
    [InlineData("XXX", "EUR", false)]
    [InlineData("USD", "XXX", false)]
    public void FrankfurterProvider_SupportsPair_ReturnsExpected(string from, string to, bool expected)
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FrankfurterProvider(httpClient, _frankLoggerMock.Object, _metricsMock.Object);

        var result = provider.SupportsPair(from, to);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task FrankfurterProvider_GetSupportedCurrenciesAsync_ReturnsExpectedCurrencies()
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new FrankfurterProvider(httpClient, _frankLoggerMock.Object, _metricsMock.Object);

        var result = await provider.GetSupportedCurrenciesAsync();

        Assert.Contains("USD", result);
        Assert.Contains("EUR", result);
        Assert.Contains("GBP", result);
    }

    #endregion

    #region ProviderChain Tests

    [Fact]
    public async Task ProviderChain_GetRateAsync_FirstProviderSucceeds_ReturnsRate()
    {
        var providerMock = CreateProviderMock("Fawazahmed", "USD", "EUR", 0.85m);
        var chain = new ProviderChain(new[] { providerMock.Object }, _chainLoggerMock.Object, _metricsMock.Object);

        var result = await chain.GetRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal(0.85m, result.Rate);
    }

    [Fact]
    public async Task ProviderChain_GetRateAsync_FirstProviderFails_FallsBackToSecond()
    {
        var provider1Mock = CreateFailingProviderMock("Fawazahmed");
        var provider2Mock = CreateProviderMock("Frankfurter", "USD", "EUR", 0.86m);
        var chain = new ProviderChain(new[] { provider1Mock.Object, provider2Mock.Object }, _chainLoggerMock.Object, _metricsMock.Object);

        var result = await chain.GetRateAsync("USD", "EUR");

        Assert.NotNull(result);
        Assert.Equal(0.86m, result.Rate);
    }

    [Fact]
    public async Task ProviderChain_GetRateAsync_AllProvidersFail_AttemptsTransitive()
    {
        var providerMock = CreateFailingProviderMock("Fawazahmed");
        providerMock.Setup(p => p.GetSupportedCurrenciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "USD", "EUR", "GBP", "JPY" });

        var chain = new ProviderChain(new[] { providerMock.Object }, _chainLoggerMock.Object, _metricsMock.Object);

        var result = await chain.GetRateAsync("GBP", "JPY");

        Assert.Null(result);
    }

    [Fact]
    public async Task ProviderChain_GetRateAsync_TransitiveCalculation_ViaUSD()
    {
        var providerMock = new Mock<IExchangeRateProvider>();
        providerMock.Setup(p => p.ProviderName).Returns("Fawazahmed");
        providerMock.Setup(p => p.GetRateAsync("GBP", "JPY", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);
        providerMock.Setup(p => p.GetRateAsync("GBP", "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateExchangeRate("GBP", "USD", 1.25m, "Fawazahmed"));
        providerMock.Setup(p => p.GetRateAsync("USD", "JPY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateExchangeRate("USD", "JPY", 150m, "Fawazahmed"));

        var chain = new ProviderChain(new[] { providerMock.Object }, _chainLoggerMock.Object, _metricsMock.Object);

        var result = await chain.GetRateAsync("GBP", "JPY");

        Assert.NotNull(result);
        Assert.True(result.IsTransitive);
        Assert.Equal("USD", result.IntermediateCurrency);
        Assert.Equal(1.25m * 150m, result.Rate);
    }

    [Fact]
    public async Task ProviderChain_GetRateAsync_TransitiveCalculation_ViaEUR()
    {
        var providerMock = new Mock<IExchangeRateProvider>();
        providerMock.Setup(p => p.ProviderName).Returns("Fawazahmed");
        providerMock.Setup(p => p.GetRateAsync("GBP", "JPY", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);
        providerMock.Setup(p => p.GetRateAsync("GBP", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateExchangeRate("GBP", "EUR", 0.85m, "Fawazahmed"));
        providerMock.Setup(p => p.GetRateAsync("EUR", "JPY", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateExchangeRate("EUR", "JPY", 165m, "Fawazahmed"));

        var chain = new ProviderChain(new[] { providerMock.Object }, _chainLoggerMock.Object, _metricsMock.Object);

        var result = await chain.GetRateAsync("GBP", "JPY");

        Assert.NotNull(result);
        Assert.True(result.IsTransitive);
        Assert.Equal("EUR", result.IntermediateCurrency);
    }

    #endregion

    #region Model Tests - Using Application DTOs

    [Fact]
    public void CurrencyResponse_Properties_SetCorrectly()
    {
        var response = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            Name = "United States Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.Equal("USD", response.Code);
        Assert.Equal("$", response.Symbol);
        Assert.Equal("United States Dollar", response.Name);
        Assert.Equal(2, response.DecimalPlaces);
        Assert.True(response.IsActive);
        Assert.True(response.IsPrimary);
    }

    [Fact]
    public void ConvertCurrencyRequest_ValidatesRequiredProperties()
    {
        var request = new ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 100.00m
        };

        Assert.Equal("USD", request.From);
        Assert.Equal("EUR", request.To);
        Assert.Equal(100.00m, request.Amount);
    }

    [Fact]
    public void ConvertCurrencyResponse_Properties_SetCorrectly()
    {
        var response = new ConvertCurrencyResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            OriginalAmount = 100m,
            ConvertedAmount = 85m,
            ExchangeRate = 0.85m,
            RateTimestamp = DateTime.UtcNow,
            Source = "Fawazahmed"
        };

        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("EUR", response.ToCurrency);
        Assert.Equal(100m, response.OriginalAmount);
        Assert.Equal(85m, response.ConvertedAmount);
        Assert.Equal(0.85m, response.ExchangeRate);
    }

    [Fact]
    public void ErrorResponse_Properties_SetCorrectly()
    {
        var response = new Maliev.CurrencyService.Api.Models.Common.ErrorResponse
        {
            Error = "BadRequest",
            Message = "Invalid request",
            Timestamp = DateTime.UtcNow,
            CorrelationId = "test-correlation-id",
            Details = new Dictionary<string, string[]>
            {
                { "Amount", new[] { "Amount must be greater than 0" } }
            }
        };

        Assert.Equal("BadRequest", response.Error);
        Assert.Equal("Invalid request", response.Message);
        Assert.Equal("test-correlation-id", response.CorrelationId);
        Assert.NotNull(response.Details);
        Assert.Contains("Amount", response.Details.Keys);
    }

    [Fact]
    public void ErrorResponse_CanBeCreatedWithMinimalProperties()
    {
        var response = new Maliev.CurrencyService.Api.Models.Common.ErrorResponse
        {
            Error = "NotFound",
            Message = "Resource not found",
            Timestamp = DateTime.UtcNow
        };

        Assert.Equal("NotFound", response.Error);
        Assert.Null(response.CorrelationId);
        Assert.Null(response.Details);
    }

    #endregion

    #region Interface Tests

    [Fact]
    public void IExchangeRateProvider_ProviderName_ReturnsProviderName()
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handlerMock.Object);

        var fawazProvider = new FawazahmedProvider(httpClient, _fawazLoggerMock.Object, _metricsMock.Object);
        var frankProvider = new FrankfurterProvider(httpClient, _frankLoggerMock.Object, _metricsMock.Object);

        Assert.Equal("Fawazahmed", fawazProvider.ProviderName);
        Assert.Equal("Frankfurter", frankProvider.ProviderName);
    }

    [Fact]
    public void IProviderMetrics_RecordsMetrics()
    {
        _metricsMock.Object.RecordProviderRequest("Fawazahmed", "USD:EUR");
        _metricsMock.Object.RecordProviderError("Fawazahmed", "network_error");
        _metricsMock.Object.RecordProviderLatency("Fawazahmed", 0.5);
        _metricsMock.Object.RecordProviderCallDuration("Fawazahmed", 0.5);
        _metricsMock.Object.RecordProviderCall("Fawazahmed", "success");
        _metricsMock.Object.RecordProviderFallback("Fawazahmed", "Frankfurter");

        _metricsMock.Verify(m => m.RecordProviderRequest("Fawazahmed", "USD:EUR"), Times.Once);
        _metricsMock.Verify(m => m.RecordProviderError("Fawazahmed", "network_error"), Times.Once);
        _metricsMock.Verify(m => m.RecordProviderLatency("Fawazahmed", 0.5), Times.Once);
        _metricsMock.Verify(m => m.RecordProviderCallDuration("Fawazahmed", 0.5), Times.Once);
        _metricsMock.Verify(m => m.RecordProviderCall("Fawazahmed", "success"), Times.Once);
        _metricsMock.Verify(m => m.RecordProviderFallback("Fawazahmed", "Frankfurter"), Times.Once);
    }

    #endregion

    #region Domain Entity Tests

    [Fact]
    public void ExchangeRate_Properties_SetCorrectly()
    {
        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Provider = "Fawazahmed",
            IsTransitive = false,
            IntermediateCurrency = null,
            FetchedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.Equal("USD", rate.FromCurrency);
        Assert.Equal("EUR", rate.ToCurrency);
        Assert.Equal(0.85m, rate.Rate);
        Assert.Equal("Fawazahmed", rate.Provider);
        Assert.False(rate.IsTransitive);
        Assert.Null(rate.IntermediateCurrency);
    }

    [Fact]
    public void ExchangeRate_TransitiveFlag_SetCorrectly()
    {
        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = "GBP",
            ToCurrency = "JPY",
            Rate = 187.5m,
            Provider = "Transitive:Fawazahmed,Fawazahmed",
            IsTransitive = true,
            IntermediateCurrency = "USD",
            FetchedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(60),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.True(rate.IsTransitive);
        Assert.Equal("USD", rate.IntermediateCurrency);
    }

    [Fact]
    public void Currency_Properties_SetCorrectly()
    {
        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            Name = "United States Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.Equal("USD", currency.Code);

        Assert.Equal("$", currency.Symbol);
        Assert.Equal("United States Dollar", currency.Name);
        Assert.Equal(2, currency.DecimalPlaces);
        Assert.True(currency.IsActive);
        Assert.True(currency.IsPrimary);
    }

    #endregion

    #region Helper Methods

    private static Mock<HttpMessageHandler> CreateMockHandler(HttpStatusCode statusCode, string content)
    {
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
        return handlerMock;
    }

    private static Mock<IExchangeRateProvider> CreateProviderMock(string name, string from, string to, decimal rate)
    {
        var mock = new Mock<IExchangeRateProvider>();
        mock.Setup(p => p.ProviderName).Returns(name);
        mock.Setup(p => p.GetRateAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateExchangeRate(from, to, rate, name));
        return mock;
    }

    private static Mock<IExchangeRateProvider> CreateFailingProviderMock(string name)
    {
        var mock = new Mock<IExchangeRateProvider>();
        mock.Setup(p => p.ProviderName).Returns(name);
        mock.Setup(p => p.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRate?)null);
        return mock;
    }

    private static ExchangeRate CreateExchangeRate(string from, string to, decimal rate, string provider)
    {
        return new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = from,
            ToCurrency = to,
            Rate = rate,
            Provider = provider,
            IsTransitive = false,
            FetchedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
