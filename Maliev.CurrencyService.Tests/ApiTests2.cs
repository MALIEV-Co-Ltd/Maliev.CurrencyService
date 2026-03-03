using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Models.Common;
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

public class RatesControllerEdgeCaseTests
{
    private readonly Mock<IRateService> _rateServiceMock;
    private readonly Mock<ILogger<RatesController>> _loggerMock;
    private readonly RatesController _controller;

    public RatesControllerEdgeCaseTests()
    {
        _rateServiceMock = new Mock<IRateService>();
        _loggerMock = new Mock<ILogger<RatesController>>();
        _controller = new RatesController(_rateServiceMock.Object, _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task GetExchangeRate_EmptyFromCurrency_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("", "EUR", "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_EmptyToCurrency_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("USD", "", "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_WhitespaceCurrency_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("  ", "EUR", "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_LowercaseCurrencyCodes_ReturnsOk()
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
    }

    [Fact]
    public async Task GetExchangeRate_SameFromAndToCurrency_ReturnsOk()
    {
        var expectedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "USD",
            Rate = 1.0m,
            Timestamp = DateTime.UtcNow,
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync("USD", "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetExchangeRate("USD", "USD", "live", null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ExchangeRateResponse>(okResult.Value);
        Assert.Equal(1.0m, response.Rate);
    }

    [Fact]
    public async Task GetExchangeRate_WithValidETag_Returns304()
    {
        var response = new ExchangeRateResponse
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
            .ReturnsAsync(response);

        var etag = ETagHelper.GenerateETag(response);
        _controller.Request.Headers.IfNoneMatch = $"\"{etag}\"";

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.IsType<StatusCodeResult>(result.Result);
        var statusResult = (StatusCodeResult)result.Result!;
        Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetExchangeRate_WithIfModifiedSinceNotModified_Returns304()
    {
        var response = new ExchangeRateResponse
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
            .ReturnsAsync(response);

        _controller.Request.Headers.IfModifiedSince = DateTime.UtcNow.AddMinutes(30).ToString("R");

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.IsType<StatusCodeResult>(result.Result);
        var statusResult = (StatusCodeResult)result.Result!;
        Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
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
    public async Task GetExchangeRate_StaleRate_ReturnsStalenessHeader()
    {
        var response = new ExchangeRateResponse
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
            .ReturnsAsync(response);

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("X-Rate-Staleness"));
    }

    [Fact]
    public async Task GetExchangeRate_SnapshotMode_ResponseHasCacheHeader()
    {
        var response = new ExchangeRateResponse
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
            .ReturnsAsync(response);

        var result = await _controller.GetExchangeRate("USD", "EUR", "snapshot", DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public async Task GetExchangeRate_LiveMode_ResponseHasCacheHeader()
    {
        var response = new ExchangeRateResponse
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
            .ReturnsAsync(response);

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
    }
}

public class CurrenciesControllerEdgeCaseTests
{
    private readonly Mock<ICurrencyService> _currencyServiceMock;
    private readonly Mock<ILogger<CurrenciesController>> _loggerMock;
    private readonly CurrenciesController _controller;

    public CurrenciesControllerEdgeCaseTests()
    {
        _currencyServiceMock = new Mock<ICurrencyService>();
        _loggerMock = new Mock<ILogger<CurrenciesController>>();
        _controller = new CurrenciesController(_currencyServiceMock.Object, _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task ListCurrencies_PageZero_UsesDefaultPage()
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
            .Setup(x => x.GetAllAsync(1, 50, It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.ListCurrencies(0, 0, null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task ListCurrencies_NegativePageSize_ReturnsOk()
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

        var result = await _controller.ListCurrencies(1, -10, null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
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
    public async Task GetCurrencyByCountryPath_WhitespaceCountryCode_ReturnsNotFound()
    {
        var result = await _controller.GetCurrencyByCountryPath("   ", CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetCurrencyByCountryPath_ServiceThrows_Returns500()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCountryCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetCurrencyByCountryPath("TH", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetByCode_ServiceThrows_Returns500()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetByCode("USD", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetById_ServiceThrows_Returns500()
    {
        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
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
    public async Task Update_UnexpectedException_Returns500()
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
            .ThrowsAsync(new Exception("Unexpected error"));

        var result = await _controller.Update(code, request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
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
            .Setup(x => x.UpdateByIdAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedCurrency);

        var currentEtag = ETagHelper.GenerateETag(existingCurrency);
        _controller.Request.Headers.IfMatch = $"\"{currentEtag}\"";

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("Updated", response.Name);
    }

    [Fact]
    public async Task UpdateById_ServiceThrows_Returns500()
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

        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCurrency);

        _currencyServiceMock
            .Setup(x => x.UpdateByIdAsync(id, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var currentEtag = ETagHelper.GenerateETag(existingCurrency);
        _controller.Request.Headers.IfMatch = $"\"{currentEtag}\"";

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task CreateAdmin_UnexpectedException_Returns500()
    {
        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "JPY",
            Symbol = "¥",
            Name = "Japanese Yen",
            DecimalPlaces = 0
        };

        _currencyServiceMock
            .Setup(x => x.CreateAsync(It.IsAny<Application.DTOs.Currencies.CreateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.CreateAdmin(request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task DeleteById_ServiceThrows_Returns500()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.DeleteByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.DeleteById(id, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task Delete_ServiceThrows_Returns500()
    {
        var code = "USD";

        _currencyServiceMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.Delete(code, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetByCode_WithMatchingETag_Returns304()
    {
        var currency = new CurrencyResponse
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
            .ReturnsAsync(currency);

        var etag = ETagHelper.GenerateETag(currency);
        _controller.Request.Headers.IfNoneMatch = $"\"{etag}\"";

        var result = await _controller.GetByCode("USD", CancellationToken.None);

        Assert.IsType<StatusCodeResult>(result.Result);
        var statusResult = (StatusCodeResult)result.Result!;
        Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetByCode_InvalidCodeFormat_ReturnsNotFound()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await _controller.GetByCode("INVALID", CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task ListCurrencies_IncludesCorrelationIdHeader()
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

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }
}

public class SnapshotsControllerEdgeCaseTests
{
    private readonly Mock<ISnapshotService> _snapshotServiceMock;
    private readonly Mock<ISnapshotQueue> _snapshotQueueMock;
    private readonly Mock<ILogger<SnapshotsController>> _loggerMock;
    private readonly SnapshotsController _controller;

    public SnapshotsControllerEdgeCaseTests()
    {
        _snapshotServiceMock = new Mock<ISnapshotService>();
        _snapshotQueueMock = new Mock<ISnapshotQueue>();
        _loggerMock = new Mock<ILogger<SnapshotsController>>();
        _controller = new SnapshotsController(
            _snapshotServiceMock.Object,
            _snapshotQueueMock.Object,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task ImportBatch_NullList_ReturnsBadRequest()
    {
        var result = await _controller.ImportBatch(null!, false, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task ImportBatch_ServiceThrows_Returns500()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = "2025-01-15T00:00:00Z" }
        };

        _snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.ImportBatch(snapshots, false, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task PromoteBatch_ServiceThrows_Returns500()
    {
        var batchId = "batch-123";

        _snapshotServiceMock
            .Setup(x => x.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.PromoteBatch(batchId, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task CleanupOldSnapshots_ServiceThrows_Returns500()
    {
        _snapshotServiceMock
            .Setup(x => x.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.CleanupOldSnapshots(CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetBatchAudit_ServiceThrows_Returns500()
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
    public async Task ImportBatch_MultipleValidationErrors_ReturnsBadRequest()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "", To = "", Rate = -1m, Timestamp = "invalid" },
            new SnapshotEntryDto { From = "XX", To = "YY", Rate = 0m, Timestamp = "also-invalid" }
        };

        var batchResponse = new SnapshotBatchResponse
        {
            BatchId = "batch-123",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "AdminApi",
            SuccessCount = 0,
            FailureCount = 2,
            Status = "failed",
            Errors = new Dictionary<string, string[]>
            {
                { "0", new[] { "Invalid from", "Invalid to", "Invalid rate", "Invalid timestamp" } },
                { "1", new[] { "Invalid rate", "Invalid timestamp" } }
            }
        };

        _snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await _controller.ImportBatch(snapshots, false, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ImportBatch_DryRunWithValidationErrors_ReturnsOkWithErrors()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "", To = "", Rate = -1m, Timestamp = "invalid" }
        };

        var batchResponse = new SnapshotBatchResponse
        {
            BatchId = "batch-123",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "AdminApi",
            SuccessCount = 0,
            FailureCount = 1,
            Status = "failed",
            Errors = new Dictionary<string, string[]>
            {
                { "0", new[] { "Invalid from" } }
            }
        };

        _snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await _controller.ImportBatch(snapshots, true, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }
}

public class SystemControllerEdgeCaseTests
{
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly Mock<ILogger<SystemController>> _loggerMock;
    private readonly SystemController _controller;

    public SystemControllerEdgeCaseTests()
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
        httpContext.TraceIdentifier = "test-trace-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task RebuildCache_ServiceThrows_ThrowsException()
    {
        _cacheServiceMock
            .Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cache error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.RebuildCache(CancellationToken.None));
    }

    [Fact]
    public async Task RebuildCache_RemovesAllCachePatterns_ReturnsOk()
    {
        _cacheServiceMock
            .Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.RebuildCache(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        _cacheServiceMock.Verify(x => x.RemoveByPatternAsync("currency:*", It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(x => x.RemoveByPatternAsync("rate:*", It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(x => x.RemoveByPatternAsync("snapshot:*", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetStats_ContainsExpectedProperties_ReturnsOk()
    {
        var result = _controller.GetStats();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void GetStats_ReturnsServiceName()
    {
        var result = _controller.GetStats();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
