using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class RatesControllerTests
{
    private readonly Mock<IRateService> _rateServiceMock;
    private readonly Mock<ILogger<RatesController>> _loggerMock;
    private readonly RatesController _controller;

    public RatesControllerTests()
    {
        _rateServiceMock = new Mock<IRateService>();
        _loggerMock = new Mock<ILogger<RatesController>>();
        _controller = new RatesController(_rateServiceMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetExchangeRate_Live_ReturnsOk()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var response = new ExchangeRateResponse
        {
            FromCurrency = from,
            ToCurrency = to,
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Test",
            IsTransitive = false,
            Mode = "live"
        };
        _rateServiceMock.Setup(s => s.GetLiveRateAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetExchangeRate(from, to, "live");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(response, okResult.Value);
    }

    [Fact]
    public async Task GetExchangeRate_Snapshot_ReturnsOk()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var date = new DateOnly(2024, 1, 1);
        var response = new ExchangeRateResponse
        {
            FromCurrency = from,
            ToCurrency = to,
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Test",
            IsTransitive = false,
            Mode = "snapshot",
            SnapshotDate = date
        };
        _rateServiceMock.Setup(s => s.GetSnapshotRateAsync(from, to, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetExchangeRate(from, to, "snapshot", date);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(response, okResult.Value);
    }

    [Fact]
    public async Task GetExchangeRate_NotFound_ReturnsNotFound()
    {
        // Arrange
        var from = "USD";
        var to = "EUR";
        var date = new DateOnly(2024, 1, 1);
        // Use snapshot mode to get 404 instead of 503
        _rateServiceMock.Setup(s => s.GetSnapshotRateAsync(from, to, date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        // Act
        var result = await _controller.GetExchangeRate(from, to, "snapshot", date);

        // Assert
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetExchangeRate_InvalidMode_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetExchangeRate("USD", "EUR", "invalid");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
