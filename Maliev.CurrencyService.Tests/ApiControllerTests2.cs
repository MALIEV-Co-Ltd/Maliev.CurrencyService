using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.CurrencyService.Tests;

public class RatesControllerTests2
{
    private readonly Mock<IRateService> _rateServiceMock;
    private readonly Mock<ILogger<RatesController>> _loggerMock;
    private readonly RatesController _controller;

    public RatesControllerTests2()
    {
        _rateServiceMock = new Mock<IRateService>();
        _loggerMock = new Mock<ILogger<RatesController>>();
        _controller = new RatesController(_rateServiceMock.Object, _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id-2";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task GetExchangeRate_LowercaseCurrencyCodes_ConvertsToUppercase()
    {
        var expectedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetExchangeRate("usd", "eur", "live", null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ExchangeRateResponse>(okResult.Value);
        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("EUR", response.ToCurrency);
    }

    [Fact]
    public async Task GetExchangeRate_IfNoneMatch_Returns304()
    {
        var expectedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("ETag"));
    }

    [Fact]
    public async Task GetExchangeRate_IfModifiedSince_Returns304()
    {
        var expectedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow.AddHours(-1),
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        _controller.Request.Headers.IfModifiedSince = DateTime.UtcNow.ToString("R");

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.IsType<StatusCodeResult>(result.Result);
    }

    [Fact]
    public async Task GetExchangeRate_FromCurrencyMissing_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("", "EUR", "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_ToCurrencyMissing_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("USD", "", "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_FromCurrencyTooShort_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("US", "EUR", "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_ToCurrencyTooShort_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("USD", "EU", "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_StaleRate_ReturnsStalenessHeader()
    {
        var expectedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow.AddMinutes(-10),
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("X-Rate-Staleness"));
    }

    [Fact]
    public async Task GetExchangeRate_SnapshotMode_ReturnsCacheHeader()
    {
        var expectedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Snapshot",
            IsTransitive = false,
            Mode = "snapshot",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        _rateServiceMock
            .Setup(x => x.GetSnapshotRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetExchangeRate("USD", "EUR", "snapshot", DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public async Task GetExchangeRate_ReturnsCorrelationIdHeader()
    {
        var expectedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task GetExchangeRate_ServiceThrows_Returns500()
    {
        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service unavailable"));

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.Equal("InternalServerError", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_RetryAfterHeader_SetOn503()
    {
        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("Retry-After"));
        Assert.Equal("30", _controller.Response.Headers.RetryAfter);
    }
}

public class CurrenciesControllerTests2
{
    private readonly Mock<ICurrencyService> _currencyServiceMock;
    private readonly Mock<ILogger<CurrenciesController>> _loggerMock;
    private readonly CurrenciesController _controller;

    public CurrenciesControllerTests2()
    {
        _currencyServiceMock = new Mock<ICurrencyService>();
        _loggerMock = new Mock<ILogger<CurrenciesController>>();
        _controller = new CurrenciesController(_currencyServiceMock.Object, _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id-2";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task ListCurrencies_ReturnsCorrelationIdHeader()
    {
        var expectedResponse = new PaginatedCurrencyResponse
        {
            Items = new List<CurrencyResponse>(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0,
            TotalPages = 0
        };

        _currencyServiceMock
            .Setup(x => x.GetAllAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.ListCurrencies(1, 50, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task ListCurrencies_ReturnsCacheControlHeader()
    {
        var expectedResponse = new PaginatedCurrencyResponse
        {
            Items = new List<CurrencyResponse>(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0,
            TotalPages = 0
        };

        _currencyServiceMock
            .Setup(x => x.GetAllAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.ListCurrencies(1, 50, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public async Task GetCurrencyById_ReturnsCorrelationIdHeader()
    {
        var expectedCurrency = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            Name = "US Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var result = await _controller.GetCurrencyById(expectedCurrency.Id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task GetCurrencyByCountryPath_EmptyCountryCode_ReturnsNotFound()
    {
        var result = await _controller.GetCurrencyByCountryPath("", CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task GetCurrencyByCountryPath_ValidCountry_ReturnsOk()
    {
        var expectedCurrency = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "THB",
            Symbol = "฿",
            Name = "Thai Baht",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _currencyServiceMock
            .Setup(x => x.GetByCountryCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var result = await _controller.GetCurrencyByCountryPath("TH", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("THB", response.Code);
    }

    [Fact]
    public async Task GetByCode_IfNoneMatch_Returns304()
    {
        var expectedCurrency = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            Name = "US Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _currencyServiceMock
            .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var result = await _controller.GetByCode("USD", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("ETag"));
    }

    [Fact]
    public async Task GetByCode_ReturnsCorrelationIdHeader()
    {
        var expectedCurrency = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            Name = "US Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _currencyServiceMock
            .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var result = await _controller.GetByCode("USD", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task GetById_Admin_ReturnsNoCacheHeader()
    {
        var id = Guid.NewGuid();
        var expectedCurrency = new CurrencyResponse
        {
            Id = id,
            Code = "USD",
            Symbol = "$",
            Name = "US Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var result = await _controller.GetById(id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public async Task CreateAdmin_CurrencyExists_ThrowsInvalidOperationException()
    {
        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "USD",
            Symbol = "$",
            Name = "US Dollar",
            DecimalPlaces = 2
        };

        _currencyServiceMock
            .Setup(x => x.CreateAsync(It.IsAny<Application.DTOs.Currencies.CreateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Currency USD already exists"));

        var result = await _controller.CreateAdmin(request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(conflictResult.Value);
        Assert.Equal("Conflict", errorResponse.Error);
    }

    [Fact]
    public async Task Update_ConcurrencyException_ReturnsConflict()
    {
        var code = "USD";
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        _currencyServiceMock
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException());

        var result = await _controller.Update(code, request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(conflictResult.Value);
        Assert.Equal("Conflict", errorResponse.Error);
    }

    [Fact]
    public async Task Update_ValidRequest_ReturnsOk()
    {
        var code = "USD";
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        var updatedCurrency = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            Name = "Updated",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _currencyServiceMock
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedCurrency);

        var result = await _controller.Update(code, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("Updated", response.Name);
    }

    [Fact]
    public async Task Update_InvalidRequest_ReturnsBadRequest()
    {
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Symbol = "",
            Name = "",
            DecimalPlaces = -1
        };

        var result = await _controller.Update("USD", request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task UpdateById_ValidETag_ReturnsOk()
    {
        var id = Guid.NewGuid();
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        var existingCurrency = new CurrencyResponse
        {
            Id = id,
            Code = "USD",
            Symbol = "$",
            Name = "US Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var updatedCurrency = new CurrencyResponse
        {
            Id = id,
            Code = "USD",
            Symbol = "$",
            Name = "Updated",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCurrency);

        _currencyServiceMock
            .Setup(x => x.UpdateByIdAsync(id, It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedCurrency);

        _controller.Request.Headers.IfMatch = "\"wrong-etag\"";

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, statusResult.StatusCode);
    }

    [Fact]
    public async Task DeleteById_ValidId_ReturnsNoContent()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.DeleteByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteById(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteById_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.DeleteByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DeleteById(id, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task DeleteById_GeneralError_Returns500()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.DeleteByIdAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.DeleteById(id, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task Delete_ValidCode_ReturnsNoContent()
    {
        _currencyServiceMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Delete("USD", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsNotFound()
    {
        _currencyServiceMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Delete("XXX", CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task Delete_GeneralError_Returns500()
    {
        _currencyServiceMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.Delete("USD", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task Update_GeneralError_Returns500()
    {
        var code = "USD";
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        _currencyServiceMock
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.Update(code, request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }
}

public class SnapshotsControllerTests2
{
    private readonly Mock<ISnapshotService> _snapshotServiceMock;
    private readonly Mock<ISnapshotQueue> _snapshotQueueMock;
    private readonly Mock<ILogger<SnapshotsController>> _loggerMock;
    private readonly SnapshotsController _controller;

    public SnapshotsControllerTests2()
    {
        _snapshotServiceMock = new Mock<ISnapshotService>();
        _snapshotQueueMock = new Mock<ISnapshotQueue>();
        _loggerMock = new Mock<ILogger<SnapshotsController>>();
        _controller = new SnapshotsController(
            _snapshotServiceMock.Object,
            _snapshotQueueMock.Object,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id-2";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task ImportBatch_NullSnapshots_ReturnsBadRequest()
    {
        var result = await _controller.ImportBatch(null!, false, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task ImportBatch_ReturnsCorrelationIdHeader()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = "2025-01-15T00:00:00Z" }
        };

        var batchResponse = new SnapshotBatchResponse
        {
            BatchId = "batch-123",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "AdminApi",
            SuccessCount = 1,
            FailureCount = 0,
            Status = "staged"
        };

        _snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await _controller.ImportBatch(snapshots, false, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task ImportBatch_GeneralException_Returns500()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = "2025-01-15T00:00:00Z" }
        };

        _snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.ImportBatch(snapshots, false, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task PromoteBatch_ReturnsCorrelationIdHeader()
    {
        var batchId = "batch-123";

        _snapshotServiceMock
            .Setup(x => x.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.PromoteBatch(batchId, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task PromoteBatch_GeneralException_Returns500()
    {
        var batchId = "batch-123";

        _snapshotServiceMock
            .Setup(x => x.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.PromoteBatch(batchId, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task CleanupOldSnapshots_ReturnsCorrelationIdHeader()
    {
        _snapshotServiceMock
            .Setup(x => x.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        var result = await _controller.CleanupOldSnapshots(CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task CleanupOldSnapshots_GeneralException_Returns500()
    {
        _snapshotServiceMock
            .Setup(x => x.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.CleanupOldSnapshots(CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetBatchAudit_GeneralException_Returns500()
    {
        var batchId = "batch-123";

        _snapshotServiceMock
            .Setup(x => x.GetBatchAuditAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetBatchAudit(batchId, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public void GetBatchStatus_ReturnsBatchStatus()
    {
        var batchId = "batch-123";

        _snapshotQueueMock
            .Setup(x => x.GetStatus(batchId))
            .Returns(("Processing", (string?)null));

        var result = _controller.GetBatchStatus(batchId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}

public class SystemControllerTests2
{
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly Mock<ILogger<SystemController>> _loggerMock;
    private readonly SystemController _controller;

    public SystemControllerTests2()
    {
        _cacheServiceMock = new Mock<ICacheService>();
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        _metrics = new CurrencyServiceMetrics(configuration.Object);
        _loggerMock = new Mock<ILogger<SystemController>>();
        _controller = new SystemController(
            _cacheServiceMock.Object,
            _metrics,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id-2";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task RebuildCache_AllPatterns_Called()
    {
        _cacheServiceMock
            .Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _controller.RebuildCache(CancellationToken.None);

        _cacheServiceMock.Verify(
            x => x.RemoveByPatternAsync("currency:*", It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheServiceMock.Verify(
            x => x.RemoveByPatternAsync("rate:*", It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheServiceMock.Verify(
            x => x.RemoveByPatternAsync("snapshot:*", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void GetStats_ReturnsServiceInfo()
    {
        var result = _controller.GetStats();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void GetStats_ReturnsExpectedProperties()
    {
        var result = _controller.GetStats();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);
    }
}
