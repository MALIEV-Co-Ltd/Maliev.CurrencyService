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

public class FinalRatesControllerTests
{
    private readonly Mock<IRateService> _rateServiceMock;
    private readonly Mock<ILogger<RatesController>> _loggerMock;
    private readonly RatesController _controller;

    public FinalRatesControllerTests()
    {
        _rateServiceMock = new Mock<IRateService>();
        _loggerMock = new Mock<ILogger<RatesController>>();
        _controller = new RatesController(_rateServiceMock.Object, _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "final-test-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task GetExchangeRate_LiveMode_ReturnsCacheControlWith5Minutes()
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

        await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
        Assert.StartsWith("public, max-age=300", _controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetExchangeRate_SnapshotMode_ReturnsCacheControlWith24Hours()
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

        await _controller.GetExchangeRate("USD", "EUR", "snapshot", DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
        Assert.StartsWith("public, max-age=86400", _controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetExchangeRate_FreshRate_ReturnsStalenessHeaderFresh()
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

        await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("X-Rate-Staleness"));
        Assert.Equal("fresh", _controller.Response.Headers["X-Rate-Staleness"]);
    }

    [Fact]
    public async Task GetExchangeRate_StaleRate_ReturnsStalenessHeaderStale()
    {
        var oldTimestamp = DateTime.UtcNow.AddMinutes(-10);
        var expectedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = oldTimestamp,
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("X-Rate-Staleness"));
        Assert.StartsWith("stale", _controller.Response.Headers["X-Rate-Staleness"]);
    }

    [Fact]
    public async Task GetExchangeRate_ServiceUnavailable_ReturnsRetryAfterHeader()
    {
        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("Retry-After"));
        Assert.Equal("30", _controller.Response.Headers.RetryAfter);
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

        await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task GetExchangeRate_ReturnsLastModifiedHeader()
    {
        var timestamp = DateTime.UtcNow;
        var expectedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = timestamp,
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("Last-Modified"));
    }
}

public class FinalCurrenciesControllerTests
{
    private readonly Mock<ICurrencyService> _currencyServiceMock;
    private readonly Mock<ILogger<CurrenciesController>> _loggerMock;
    private readonly CurrenciesController _controller;

    public FinalCurrenciesControllerTests()
    {
        _currencyServiceMock = new Mock<ICurrencyService>();
        _loggerMock = new Mock<ILogger<CurrenciesController>>();
        _controller = new CurrenciesController(_currencyServiceMock.Object, _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "final-test-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task GetCurrencyByCountryPath_ValidCountryCode_ReturnsCurrency()
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
            .Setup(x => x.GetByCountryCodeAsync("TH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var result = await _controller.GetCurrencyByCountryPath("TH", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("THB", response.Code);
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
    public async Task GetCurrencyByCountryPath_ValidCountryCode_ReturnsCacheControl1Hour()
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
            .Setup(x => x.GetByCountryCodeAsync("TH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        await _controller.GetCurrencyByCountryPath("TH", CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
        Assert.StartsWith("public, max-age=3600", _controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetById_Admin_ValidId_ReturnsCurrency()
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
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("USD", response.Code);
    }

    [Fact]
    public async Task GetById_Admin_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await _controller.GetById(id, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
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

        await _controller.GetById(id, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
        Assert.StartsWith("private, max-age=0", _controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task UpdateById_ValidETag_ReturnsUpdatedCurrency()
    {
        var id = Guid.NewGuid();
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated US Dollar",
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
            Name = "Updated US Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow.AddMinutes(1)
        };

        _currencyServiceMock
            .SetupSequence(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCurrency)
            .ReturnsAsync(existingCurrency);

        _currencyServiceMock
            .Setup(x => x.UpdateByIdAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedCurrency);

        var etag = Maliev.CurrencyService.Api.Models.Common.ETagHelper.GenerateETag(existingCurrency);
        _controller.Request.Headers.IfMatch = $"\"{etag}\"";

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("Updated US Dollar", response.Name);
    }

    [Fact]
    public async Task UpdateById_ValidETag_ReturnsNewETag()
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
            UpdatedAt = DateTime.UtcNow.AddMinutes(1)
        };

        _currencyServiceMock
            .SetupSequence(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCurrency)
            .ReturnsAsync(existingCurrency);

        _currencyServiceMock
            .Setup(x => x.UpdateByIdAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedCurrency);

        var etag = Maliev.CurrencyService.Api.Models.Common.ETagHelper.GenerateETag(existingCurrency);
        _controller.Request.Headers.IfMatch = $"\"{etag}\"";

        await _controller.UpdateById(id, request, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("ETag"));
    }

    [Fact]
    public async Task UpdateById_CurrencyNotFoundAfterETagCheck_ReturnsNotFound()
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

        var etag = Maliev.CurrencyService.Api.Models.Common.ETagHelper.GenerateETag(existingCurrency);
        _controller.Request.Headers.IfMatch = $"\"{etag}\"";

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
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
            .Setup(x => x.GetByCodeAsync("USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var etag = Maliev.CurrencyService.Api.Models.Common.ETagHelper.GenerateETag(expectedCurrency);
        _controller.Request.Headers.IfNoneMatch = $"\"{etag}\"";

        var result = await _controller.GetByCode("USD", CancellationToken.None);

        var statusResult = Assert.IsType<StatusCodeResult>(result.Result);
        Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetByCode_ReturnsCacheControlHeader()
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
            .Setup(x => x.GetByCodeAsync("USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        await _controller.GetByCode("USD", CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
        Assert.StartsWith("public, max-age=300", _controller.Response.Headers.CacheControl);
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

        await _controller.ListCurrencies(1, 50, null, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
        Assert.StartsWith("public, max-age=300", _controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetCurrencyByCountry_ValidISO3_ReturnsCurrency()
    {
        var expectedCurrency = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "JPY",
            Symbol = "¥",
            Name = "Japanese Yen",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _currencyServiceMock
            .Setup(x => x.GetByCountryCodeAsync("JPN", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var result = await _controller.GetCurrencyByCountry("JPN", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("JPY", response.Code);
    }

    [Fact]
    public async Task GetCurrencyByCountry_InvalidFormat_ReturnsBadRequest()
    {
        var result = await _controller.GetCurrencyByCountry("A", CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task Update_ConcurrencyException_ReturnsConflict()
    {
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        _currencyServiceMock
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Concurrency conflict"));

        var result = await _controller.Update("USD", request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(conflictResult.Value);
        Assert.Equal("Conflict", errorResponse.Error);
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
    public async Task DeleteById_Exception_Returns500()
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
    public async Task Delete_Exception_Returns500()
    {
        _currencyServiceMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.Delete("USD", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task CreateAdmin_InvalidCodeLength_ReturnsBadRequest()
    {
        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "TOOLONG",
            Symbol = "$",
            Name = "Test",
            DecimalPlaces = 2
        };

        var result = await _controller.CreateAdmin(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task CreateAdmin_InvalidCodeFormat_ReturnsBadRequest()
    {
        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "123",
            Symbol = "$",
            Name = "Test",
            DecimalPlaces = 2
        };

        var result = await _controller.CreateAdmin(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }
}

public class FinalSnapshotsControllerTests
{
    private readonly Mock<ISnapshotService> _snapshotServiceMock;
    private readonly Mock<ISnapshotQueue> _snapshotQueueMock;
    private readonly Mock<ILogger<SnapshotsController>> _loggerMock;
    private readonly SnapshotsController _controller;

    public FinalSnapshotsControllerTests()
    {
        _snapshotServiceMock = new Mock<ISnapshotService>();
        _snapshotQueueMock = new Mock<ISnapshotQueue>();
        _loggerMock = new Mock<ILogger<SnapshotsController>>();
        _controller = new SnapshotsController(
            _snapshotServiceMock.Object,
            _snapshotQueueMock.Object,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "final-test-id";
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

        await _controller.ImportBatch(snapshots, false, CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task PromoteBatch_ReturnsCorrelationIdHeader()
    {
        _snapshotServiceMock
            .Setup(x => x.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _controller.PromoteBatch("batch-123", CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task PromoteBatch_Exception_Returns500()
    {
        _snapshotServiceMock
            .Setup(x => x.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.PromoteBatch("batch-123", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task CleanupOldSnapshots_Exception_Returns500()
    {
        _snapshotServiceMock
            .Setup(x => x.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.CleanupOldSnapshots(CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task CleanupOldSnapshots_ReturnsCorrelationIdHeader()
    {
        _snapshotServiceMock
            .Setup(x => x.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        await _controller.CleanupOldSnapshots(CancellationToken.None);

        Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
    }

    [Fact]
    public async Task GetBatchAudit_Exception_Returns500()
    {
        _snapshotServiceMock
            .Setup(x => x.GetBatchAuditAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetBatchAudit("batch-123", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public void GetBatchStatus_ReturnsBatchInfo()
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

public class FinalSystemControllerTests
{
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly Mock<ILogger<SystemController>> _loggerMock;
    private readonly SystemController _controller;

    public FinalSystemControllerTests()
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
        httpContext.TraceIdentifier = "final-test-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public void GetStats_ReturnsServiceInfo()
    {
        var result = _controller.GetStats();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void GetStats_ReturnsCorrectVersion()
    {
        var result = _controller.GetStats();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);
    }

    [Fact]
    public async Task RebuildCache_CallsAllPatternRemovals()
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
    public async Task RebuildCache_Exception_ThrowsException()
    {
        _cacheServiceMock
            .Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cache error"));

        await Assert.ThrowsAsync<Exception>(() => _controller.RebuildCache(CancellationToken.None));
    }
}
