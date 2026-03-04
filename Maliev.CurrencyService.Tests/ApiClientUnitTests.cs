using System.Net;
using System.Net.Http;
using System.Text;
using Maliev.CurrencyService.Api.Services.Clients;
using Moq;
using Moq.Protected;

namespace Maliev.CurrencyService.Tests;

public class FrankfurterApiClientUnitTests
{
    private static HttpClient CreateHttpClient(Mock<HttpMessageHandler> handlerMock)
    {
        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://api.frankfurter.app")
        };
    }

    [Fact]
    public async Task GetLatestRatesAsync_WithValidJson_ReturnsRates()
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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"date\":\"2024-01-17\",\"base\":\"USD\",\"rates\":{\"EUR\":0.92,\"GBP\":0.79}}"),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FrankfurterApiClient(httpClient);

        var result = await client.GetLatestRatesAsync("USD", "EUR", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("USD", result.Base);
        Assert.Equal("2024-01-17", result.Date);
        Assert.Equal(0.92m, result.Rates["EUR"]);
    }

    [Fact]
    public async Task GetLatestRatesAsync_WithMultipleCurrencies_ReturnsAllRates()
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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"date\":\"2024-01-17\",\"base\":\"USD\",\"rates\":{\"EUR\":0.92,\"GBP\":0.79,\"JPY\":149.5}}"),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FrankfurterApiClient(httpClient);

        var result = await client.GetLatestRatesAsync("USD", new[] { "EUR", "GBP", "JPY" }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, result.Rates.Count);
        Assert.Equal(0.92m, result.Rates["EUR"]);
        Assert.Equal(0.79m, result.Rates["GBP"]);
        Assert.Equal(149.5m, result.Rates["JPY"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetLatestRatesAsync_WhenHttpError_ReturnsNull(HttpStatusCode statusCode)
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
                Content = new StringContent(""),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FrankfurterApiClient(httpClient);

        var result = await client.GetLatestRatesAsync("USD", "EUR", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestRatesAsync_WithInvalidJson_ThrowsJsonException()
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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("not valid json {"),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FrankfurterApiClient(httpClient);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => client.GetLatestRatesAsync("USD", "EUR", CancellationToken.None));
    }

    [Fact]
    public async Task GetLatestRatesAsync_WithEmptyJson_ThrowsJsonException()
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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(""),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FrankfurterApiClient(httpClient);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => client.GetLatestRatesAsync("USD", "EUR", CancellationToken.None));
    }

    [Fact]
    public async Task GetLatestRatesAsync_WithIncompleteJson_ReturnsModelWithNullProperties()
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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"date\":\"2024-01-17\"}"),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FrankfurterApiClient(httpClient);

        var result = await client.GetLatestRatesAsync("USD", "EUR", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("2024-01-17", result.Date);
    }

    [Fact]
    public async Task GetLatestRatesAsync_WhenTimeout_ThrowsTaskCanceledException()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException());

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FrankfurterApiClient(httpClient);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.GetLatestRatesAsync("USD", "EUR", CancellationToken.None));
    }
}

public class FawazahmedApiClientUnitTests
{
    private static HttpClient CreateHttpClient(Mock<HttpMessageHandler> handlerMock)
    {
        return new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://cdn.jsdelivr.net/npm/")
        };
    }

    [Fact]
    public async Task GetCurrencyRatesAsync_WithValidJson_ReturnsRates()
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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"usd\":{\"eur\":0.92,\"gbp\":0.79}}"),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FawazahmedApiClient(httpClient);

        var result = await client.GetCurrencyRatesAsync("USD", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("usd", result.Keys);
    }

    [Fact]
    public async Task GetCurrencyRatesAsync_WithUppercaseCurrencyCode_NormalizesToLowercase()
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
                Content = new StringContent("{\"usd\":{\"eur\":0.92}}"),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FawazahmedApiClient(httpClient);

        await client.GetCurrencyRatesAsync("USD", CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Contains("currencies/usd.json", capturedRequest.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetCurrencyRatesAsync_WhenHttpError_ReturnsNull(HttpStatusCode statusCode)
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
                Content = new StringContent(""),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FawazahmedApiClient(httpClient);

        var result = await client.GetCurrencyRatesAsync("USD", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrencyRatesAsync_WithInvalidJson_ThrowsJsonException()
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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("not valid json {"),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FawazahmedApiClient(httpClient);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => client.GetCurrencyRatesAsync("USD", CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrencyRatesAsync_WithEmptyJson_ThrowsJsonException()
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
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(""),
            });

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FawazahmedApiClient(httpClient);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => client.GetCurrencyRatesAsync("USD", CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrencyRatesAsync_WhenTimeout_ThrowsTaskCanceledException()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException());

        var httpClient = CreateHttpClient(handlerMock);
        var client = new FawazahmedApiClient(httpClient);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.GetCurrencyRatesAsync("USD", CancellationToken.None));
    }
}
