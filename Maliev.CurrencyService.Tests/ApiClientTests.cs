using System.Net;
using Maliev.CurrencyService.Api.Models.ApiResponses;
using Moq;
using Moq.Protected;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class FrankfurterApiClientTests
{
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

    private static HttpClient CreateHttpClient(Mock<HttpMessageHandler> handlerMock)
    {
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.frankfurter.app")
        };
        return httpClient;
    }

    [Fact]
    public async Task GetLatestRatesAsync_WithTwoCurrencies_ReturnsRates()
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{\"date\":\"2024-01-17\",\"base\":\"USD\",\"rates\":{\"EUR\":0.85}}");
        var httpClient = CreateHttpClient(handlerMock);
        var client = new Api.Services.Clients.FrankfurterApiClient(httpClient);

        var result = await client.GetLatestRatesAsync("USD", "EUR", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("USD", result.Base);
        Assert.Equal("2024-01-17", result.Date);
        Assert.Contains("EUR", result.Rates);
        Assert.Equal(0.85m, result.Rates["EUR"]);
    }

    [Fact]
    public async Task GetLatestRatesAsync_WithMultipleCurrencies_ReturnsRates()
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{\"date\":\"2024-01-17\",\"base\":\"USD\",\"rates\":{\"EUR\":0.85,\"GBP\":0.75}}");
        var httpClient = CreateHttpClient(handlerMock);
        var client = new Api.Services.Clients.FrankfurterApiClient(httpClient);

        var result = await client.GetLatestRatesAsync("USD", new[] { "EUR", "GBP" }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("USD", result.Base);
        Assert.Equal(2, result.Rates.Count);
        Assert.Equal(0.85m, result.Rates["EUR"]);
        Assert.Equal(0.75m, result.Rates["GBP"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task GetLatestRatesAsync_WhenNotSuccessful_ReturnsNull(HttpStatusCode statusCode)
    {
        var handlerMock = CreateMockHandler(statusCode, "");
        var httpClient = CreateHttpClient(handlerMock);
        var client = new Api.Services.Clients.FrankfurterApiClient(httpClient);

        var result = await client.GetLatestRatesAsync("USD", "EUR", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestRatesAsync_CallsCorrectEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"date\":\"2024-01-17\",\"base\":\"USD\",\"rates\":{\"EUR\":0.85}}"),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new Api.Services.Clients.FrankfurterApiClient(httpClient);

        await client.GetLatestRatesAsync("USD", "EUR", CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Contains("latest", capturedRequest.RequestUri?.ToString());
        Assert.Contains("from=USD", capturedRequest.RequestUri?.ToString());
        Assert.Contains("to=EUR", capturedRequest.RequestUri?.ToString());
    }
}

public class FawazahmedApiClientTests
{
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

    private static HttpClient CreateHttpClient(Mock<HttpMessageHandler> handlerMock)
    {
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://cdn.jsdelivr.net/npm/")
        };
        return httpClient;
    }

    [Fact]
    public async Task GetCurrencyRatesAsync_WithValidCurrency_ReturnsRates()
    {
        var handlerMock = CreateMockHandler(HttpStatusCode.OK, "{\"usd\":{\"eur\":0.85,\"gbp\":0.75}}");
        var httpClient = CreateHttpClient(handlerMock);
        var client = new Api.Services.Clients.FawazahmedApiClient(httpClient);

        var result = await client.GetCurrencyRatesAsync("USD", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("usd", result.Keys);
    }

    [Fact]
    public async Task GetCurrencyRatesAsync_CurrencyCodeIsNormalizedToLowercase()
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"usd\":{\"eur\":0.85}}"),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new Api.Services.Clients.FawazahmedApiClient(httpClient);

        await client.GetCurrencyRatesAsync("USD", CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Contains("currencies/usd.json", capturedRequest.RequestUri?.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task GetCurrencyRatesAsync_WhenNotSuccessful_ReturnsNull(HttpStatusCode statusCode)
    {
        var handlerMock = CreateMockHandler(statusCode, "");
        var httpClient = CreateHttpClient(handlerMock);
        var client = new Api.Services.Clients.FawazahmedApiClient(httpClient);

        var result = await client.GetCurrencyRatesAsync("USD", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrencyRatesAsync_CallsCorrectEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"usd\":{\"eur\":0.85}}"),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new Api.Services.Clients.FawazahmedApiClient(httpClient);

        await client.GetCurrencyRatesAsync("EUR", CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Contains("currencies/eur.json", capturedRequest.RequestUri?.ToString());
    }
}
