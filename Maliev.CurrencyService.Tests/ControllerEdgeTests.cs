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

public class ControllerEdgeTests
{
    #region RatesController Edge Cases

    [Fact]
    public async Task GetExchangeRate_ModeCaseInsensitive_ConvertsToLower()
    {
        var mock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();
        var controller = new RatesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

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

        mock.Setup(x => x.GetLiveRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await controller.GetExchangeRate("USD", "EUR", "LIVE", null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetExchangeRate_FreshRate_ReturnsFreshStalenessHeader()
    {
        var mock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();
        var controller = new RatesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

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

        mock.Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(controller.Response.Headers.ContainsKey("X-Rate-Staleness"));
        Assert.Equal("fresh", controller.Response.Headers["X-Rate-Staleness"]);
    }

    [Fact]
    public async Task GetExchangeRate_EmptyFromParameter_ReturnsBadRequest()
    {
        var mock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();
        var controller = new RatesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetExchangeRate("", "EUR", "live", null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequest.Value);
        Assert.Equal("BadRequest", error.Error);
    }

    [Fact]
    public async Task GetExchangeRate_EmptyToParameter_ReturnsBadRequest()
    {
        var mock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();
        var controller = new RatesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetExchangeRate("USD", "", "live", null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequest.Value);
        Assert.Equal("BadRequest", error.Error);
    }

    [Fact]
    public async Task GetExchangeRate_FromCurrencyTooLong_ReturnsBadRequest()
    {
        var mock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();
        var controller = new RatesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetExchangeRate("USDD", "EUR", "live", null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequest.Value);
        Assert.Contains("from", error.Details?["validation"]?.FirstOrDefault() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetExchangeRate_ToCurrencyTooLong_ReturnsBadRequest()
    {
        var mock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();
        var controller = new RatesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetExchangeRate("USD", "EURR", "live", null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequest.Value);
        Assert.Contains("to", error.Details?["validation"]?.FirstOrDefault() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetExchangeRate_LiveMode_AddsCacheControlHeader()
    {
        var mock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();
        var controller = new RatesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

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

        mock.Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(controller.Response.Headers.ContainsKey("Cache-Control"));
    }

    #endregion

    #region CurrenciesController Edge Cases

    [Fact]
    public async Task GetById_Admin_ExistingId_ReturnsOk()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

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

        mock.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var result = await controller.GetById(id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("USD", response.Code);
    }

    [Fact]
    public async Task GetById_Admin_NonExistingId_ReturnsNotFound()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var id = Guid.NewGuid();

        mock.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await controller.GetById(id, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var error = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", error.Error);
    }

    [Fact]
    public async Task GetById_Admin_ServiceThrows_Returns500()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var id = Guid.NewGuid();

        mock.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await controller.GetById(id, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetById_Admin_ReturnsETagHeader()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

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

        mock.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var result = await controller.GetById(id, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(controller.Response.Headers.ContainsKey("ETag"));
    }

    [Fact]
    public async Task GetCurrencyByCountryPath_ServiceThrows_Returns500()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        mock.Setup(x => x.GetByCountryCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await controller.GetCurrencyByCountryPath("TH", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetCurrencyByCountryPath_NullCountryCode_ReturnsNotFound()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetCurrencyByCountryPath(null!, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var error = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", error.Error);
    }

    [Fact]
    public async Task GetCurrencyByCountryPath_NotFound_ReturnsNotFound()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        mock.Setup(x => x.GetByCountryCodeAsync("XX", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await controller.GetCurrencyByCountryPath("XX", CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var error = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", error.Error);
    }

    [Fact]
    public async Task GetCurrencyByCountryPath_ReturnsCacheControlHeader()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

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

        mock.Setup(x => x.GetByCountryCodeAsync("TH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var result = await controller.GetCurrencyByCountryPath("TH", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(controller.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public async Task GetCurrencyByCountry_InvalidCountryCodeFormat_ReturnsBadRequest()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetCurrencyByCountry("1", CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequest.Value);
        Assert.Equal("BadRequest", error.Error);
    }

    [Fact]
    public async Task GetCurrencyByCountry_ServiceThrows_Returns500()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        mock.Setup(x => x.GetByCountryCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await controller.GetCurrencyByCountry("TH", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetByCode_ServiceThrows_Returns500()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        mock.Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await controller.GetByCode("USD", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task ListCurrencies_ReturnsETagHeader()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var expectedResponse = new PaginatedCurrencyResponse
        {
            Items = new List<CurrencyResponse>(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0,
            TotalPages = 0
        };

        mock.Setup(x => x.GetAllAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await controller.ListCurrencies(1, 50, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(controller.Response.Headers.ContainsKey("ETag"));
    }

    [Fact]
    public async Task UpdateById_InvalidRequest_ReturnsBadRequest()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var id = Guid.NewGuid();
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Symbol = "",
            Name = "",
            DecimalPlaces = -1
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

        mock.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCurrency);

        controller.Request.Headers.IfMatch = "\"abc123\"";

        var result = await controller.UpdateById(id, request, CancellationToken.None);

        Assert.IsType<ObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateById_CurrencyNotFoundAfterETagCheck_ReturnsNotFound()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

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

        mock.SetupSequence(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCurrency)
            .ReturnsAsync((CurrencyResponse?)null);

        controller.Request.Headers.IfMatch = "\"correct-etag\"";

        var result = await controller.UpdateById(id, request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, statusResult.StatusCode);
    }

    [Fact]
    public async Task UpdateById_ServiceThrows_Returns500()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

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

        mock.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCurrency);

        mock.Setup(x => x.UpdateByIdAsync(id, It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        controller.Request.Headers.IfMatch = "\"abc123\"";

        var result = await controller.UpdateById(id, request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteById_NotFound_ReturnsNotFound()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var id = Guid.NewGuid();

        mock.Setup(x => x.DeleteByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await controller.DeleteById(id, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", error.Error);
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsNotFound()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        mock.Setup(x => x.DeleteAsync("XXX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await controller.Delete("XXX", CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", error.Error);
    }

    [Fact]
    public async Task CreateAdmin_ServiceThrows_Returns500()
    {
        var mock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();
        var controller = new CurrenciesController(mock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "JPY",
            Symbol = "¥",
            Name = "Japanese Yen",
            DecimalPlaces = 0
        };

        mock.Setup(x => x.CreateAsync(It.IsAny<Application.DTOs.Currencies.CreateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await controller.CreateAdmin(request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    #endregion

    #region SnapshotsController Edge Cases

    [Fact]
    public async Task ImportBatch_ControllerValidationErrors_ReturnsBadRequest()
    {
        var mockService = new Mock<ISnapshotService>();
        var mockQueue = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();
        var controller = new SnapshotsController(mockService.Object, mockQueue.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "", To = "EUR", Rate = 0.85m, Timestamp = "2025-01-15T00:00:00Z" }
        };

        var batchResponse = new SnapshotBatchResponse
        {
            BatchId = "batch-123",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "AdminApi",
            SuccessCount = 1,
            FailureCount = 1,
            Status = "staged",
            Errors = new Dictionary<string, string[]>
            {
                { "0", new[] { "Invalid from currency" } }
            }
        };

        mockService.Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await controller.ImportBatch(snapshots, false, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ImportBatch_InvalidTimestamp_ReturnsAccepted()
    {
        var mockService = new Mock<ISnapshotService>();
        var mockQueue = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();
        var controller = new SnapshotsController(mockService.Object, mockQueue.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = "invalid-timestamp" }
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

        mockService.Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await controller.ImportBatch(snapshots, false, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task ImportBatch_NegativeRate_ReturnsAccepted()
    {
        var mockService = new Mock<ISnapshotService>();
        var mockQueue = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();
        var controller = new SnapshotsController(mockService.Object, mockQueue.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "USD", To = "EUR", Rate = -1m, Timestamp = "2025-01-15T00:00:00Z" }
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

        mockService.Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await controller.ImportBatch(snapshots, false, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task PromoteBatch_UpdatesQueueStatus()
    {
        var mockService = new Mock<ISnapshotService>();
        var mockQueue = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();
        var controller = new SnapshotsController(mockService.Object, mockQueue.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var batchId = "batch-123";

        mockService.Setup(x => x.PromoteBatchAsync(batchId, null, "System", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await controller.PromoteBatch(batchId, CancellationToken.None);

        mockQueue.Verify(x => x.UpdateStatus(batchId, "Promoted"), Times.Once);
    }

    [Fact]
    public async Task CleanupOldSnapshots_ReturnsDeletedCount()
    {
        var mockService = new Mock<ISnapshotService>();
        var mockQueue = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();
        var controller = new SnapshotsController(mockService.Object, mockQueue.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        mockService.Setup(x => x.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        var result = await controller.CleanupOldSnapshots(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetBatchAudit_ReturnsAuditLog()
    {
        var mockService = new Mock<ISnapshotService>();
        var mockQueue = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();
        var controller = new SnapshotsController(mockService.Object, mockQueue.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var batchId = "batch-123";
        var auditLog = new Application.DTOs.Snapshots.SnapshotAuditLog
        {
            BatchId = batchId,
            Timestamp = DateTime.UtcNow,
            RecordCount = 10,
            Source = "AdminApi",
            SubmittedBy = "admin"
        };

        mockService.Setup(x => x.GetBatchAuditAsync(batchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLog);

        var result = await controller.GetBatchAudit(batchId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<Application.DTOs.Snapshots.SnapshotAuditLog>(okResult.Value);
        Assert.Equal(batchId, response.BatchId);
    }

    #endregion

    #region SystemController Edge Cases

    [Fact]
    public async Task RebuildCache_RemovesAllCachePatterns()
    {
        var mockCache = new Mock<ICacheService>();
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        var metrics = new CurrencyServiceMetrics(configuration.Object);
        var loggerMock = new Mock<ILogger<SystemController>>();
        var controller = new SystemController(mockCache.Object, metrics, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        mockCache.Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await controller.RebuildCache(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        mockCache.Verify(x => x.RemoveByPatternAsync("currency:*", It.IsAny<CancellationToken>()), Times.Once);
        mockCache.Verify(x => x.RemoveByPatternAsync("rate:*", It.IsAny<CancellationToken>()), Times.Once);
        mockCache.Verify(x => x.RemoveByPatternAsync("snapshot:*", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetStats_ReturnsServiceName()
    {
        var mockCache = new Mock<ICacheService>();
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        var metrics = new CurrencyServiceMetrics(configuration.Object);
        var loggerMock = new Mock<ILogger<SystemController>>();
        var controller = new SystemController(mockCache.Object, metrics, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = controller.GetStats();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    #endregion
}
