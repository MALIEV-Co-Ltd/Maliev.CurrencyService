using System.Net;
using System.Text.Json;
using Maliev.CurrencyService.Api.Models.ApiResponses;
using Maliev.CurrencyService.Api.Services.Clients;
using Moq;
using Moq.Protected;
using Xunit;

namespace Maliev.CurrencyService.Tests.Services;

public class FrankfurterApiClientTests
{
    private readonly JsonSerializerOptions _jsonOptions;

    public FrankfurterApiClientTests()
    {
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    [Fact]
    public async Task GetLatestRatesAsync_Single_ReturnsModel_OnSuccess()
    {
        // Arrange
        var model = new OpenRatesModel { Base = "USD", Rates = new Dictionary<string, decimal> { ["EUR"] = 0.85m } };
        var handlerMock = new Mock<HttpMessageHandler>();
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
                Content = new StringContent(JsonSerializer.Serialize(model))
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var client = new FrankfurterApiClient(httpClient);

        // Act
        var result = await client.GetLatestRatesAsync("USD", "EUR");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("USD", result.Base);
        Assert.Equal(0.85m, result.Rates["EUR"]);
    }

    [Fact]
    public async Task GetLatestRatesAsync_Single_ReturnsNull_OnError()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var client = new FrankfurterApiClient(httpClient);

        // Act
        var result = await client.GetLatestRatesAsync("USD", "EUR");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestRatesAsync_Multiple_ReturnsModel_OnSuccess()
    {
        // Arrange
        var model = new OpenRatesModel { Base = "USD", Rates = new Dictionary<string, decimal> { ["EUR"] = 0.85m, ["GBP"] = 0.75m } };
        var handlerMock = new Mock<HttpMessageHandler>();
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
                Content = new StringContent(JsonSerializer.Serialize(model))
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var client = new FrankfurterApiClient(httpClient);

        // Act
        var result = await client.GetLatestRatesAsync("USD", new[] { "EUR", "GBP" });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Rates.Count);
    }

    [Fact]
    public async Task GetLatestRatesAsync_Multiple_ReturnsNull_OnError()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var client = new FrankfurterApiClient(httpClient);

        // Act
        var result = await client.GetLatestRatesAsync("USD", new[] { "EUR", "GBP" });

        // Assert
        Assert.Null(result);
    }
}
