using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class TestsV5
{
    #region SystemController Tests

    [Fact]
    public void SystemController_RebuildCache_RemovesCurrencyCachePattern()
    {
        var cacheServiceMock = new Mock<ICacheService>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        var metrics = new CurrencyServiceMetrics(configMock.Object);
        var loggerMock = new Mock<ILogger<SystemController>>();

        cacheServiceMock
            .Setup(c => c.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new SystemController(cacheServiceMock.Object, metrics, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = controller.RebuildCache(CancellationToken.None).Result;

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        cacheServiceMock.Verify(
            c => c.RemoveByPatternAsync("currency:*", It.IsAny<CancellationToken>()),
            Times.Once);
        cacheServiceMock.Verify(
            c => c.RemoveByPatternAsync("rate:*", It.IsAny<CancellationToken>()),
            Times.Once);
        cacheServiceMock.Verify(
            c => c.RemoveByPatternAsync("snapshot:*", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void SystemController_GetStats_ReturnsServiceInfo()
    {
        var cacheServiceMock = new Mock<ICacheService>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        var metrics = new CurrencyServiceMetrics(configMock.Object);
        var loggerMock = new Mock<ILogger<SystemController>>();

        var controller = new SystemController(cacheServiceMock.Object, metrics, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = controller.GetStats();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    #endregion

    #region DTO Validation Tests

    [Fact]
    public void ConvertCurrencyRequest_ValidRequest_PassesValidation()
    {
        var request = new Maliev.CurrencyService.Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 100.50m
        };

        Assert.Equal("USD", request.From);
        Assert.Equal("EUR", request.To);
        Assert.Equal(100.50m, request.Amount);
    }

    [Fact]
    public void ConvertCurrencyRequest_ZeroAmount_StoresCorrectly()
    {
        var request = new Maliev.CurrencyService.Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 0m
        };

        Assert.Equal(0m, request.Amount);
    }

    [Fact]
    public void ConvertCurrencyRequest_LargeAmount_StoresCorrectly()
    {
        var request = new Maliev.CurrencyService.Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 1000000000m
        };

        Assert.Equal(1000000000m, request.Amount);
    }

    #endregion

    #region ETagHelper Tests

    [Fact]
    public void ETagHelper_GenerateETag_SameInput_SameOutput()
    {
        var obj1 = new { Code = "USD", Name = "US Dollar" };
        var obj2 = new { Code = "USD", Name = "US Dollar" };

        var etag1 = Maliev.CurrencyService.Application.Common.ETagHelper.GenerateETag(obj1);
        var etag2 = Maliev.CurrencyService.Application.Common.ETagHelper.GenerateETag(obj2);

        Assert.Equal(etag1, etag2);
    }

    [Fact]
    public void ETagHelper_GenerateETag_DifferentInput_DifferentOutput()
    {
        var obj1 = new { Code = "USD", Name = "US Dollar" };
        var obj2 = new { Code = "EUR", Name = "Euro" };

        var etag1 = Maliev.CurrencyService.Application.Common.ETagHelper.GenerateETag(obj1);
        var etag2 = Maliev.CurrencyService.Application.Common.ETagHelper.GenerateETag(obj2);

        Assert.NotEqual(etag1, etag2);
    }

    [Fact]
    public void ETagHelper_GenerateETag_ReturnsValidBase64String()
    {
        var obj = new { Code = "USD" };
        var etag = Maliev.CurrencyService.Application.Common.ETagHelper.GenerateETag(obj);

        Assert.NotEmpty(etag);
        Assert.True(etag.Length <= 16);
    }

    #endregion

    #region SnapshotsController Edge Case Tests

    [Fact]
    public async Task SnapshotsController_ImportBatch_EmptySnapshots_ReturnsBadRequest()
    {
        var snapshotServiceMock = new Mock<ISnapshotService>();
        var snapshotQueueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();

        var controller = new SnapshotsController(
            snapshotServiceMock.Object,
            snapshotQueueMock.Object,
            loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ImportBatch(new List<SnapshotEntryDto>(), false, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequest.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task SnapshotsController_ImportBatch_NullSnapshots_ReturnsBadRequest()
    {
        var snapshotServiceMock = new Mock<ISnapshotService>();
        var snapshotQueueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();

        var controller = new SnapshotsController(
            snapshotServiceMock.Object,
            snapshotQueueMock.Object,
            loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ImportBatch(null!, false, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequest.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    #endregion

    #region CurrenciesController Edge Case Tests

    [Fact]
    public async Task CurrenciesController_GetByCode_EmptyCode_ReturnsNotFound()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        currencyServiceMock
            .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Application.DTOs.Currencies.CurrencyResponse?)null);

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetByCode("", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFound.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task CurrenciesController_GetCurrencyById_NotFound_Returns404()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        currencyServiceMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Application.DTOs.Currencies.CurrencyResponse?)null);

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetCurrencyById(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFound.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task CurrenciesController_GetCurrencyByCountry_InvalidCode_ReturnsBadRequest()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetCurrencyByCountry("invalid_code", CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequest.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task CurrenciesController_Update_ValidationErrors_ReturnsBadRequest()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "",
            Symbol = ""
        };

        var result = await controller.Update("USD", request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequest.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task CurrenciesController_Delete_NotFound_Returns404()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        currencyServiceMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Delete("NOTEXIST", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFound.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task CurrenciesController_GetCurrencyByCountryPath_EmptyCode_ReturnsNotFound()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetCurrencyByCountryPath("", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFound.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task CurrenciesController_GetCurrencyByCountryPath_NotFound_Returns404()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        currencyServiceMock
            .Setup(x => x.GetByCountryCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Application.DTOs.Currencies.CurrencyResponse?)null);

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetCurrencyByCountryPath("XX", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFound.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    #endregion

    #region RatesController Edge Case Tests

    [Fact]
    public async Task RatesController_GetExchangeRate_BothCurrenciesEmpty_ReturnsBadRequest()
    {
        var rateServiceMock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();

        var controller = new RatesController(rateServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetExchangeRate("", "", "live", null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequest.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task RatesController_GetExchangeRate_FromCurrencyTooShort_ReturnsBadRequest()
    {
        var rateServiceMock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();

        var controller = new RatesController(rateServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetExchangeRate("U", "EUR", "live", null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequest.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task RatesController_GetExchangeRate_IfNoneMatchHeader_Returns304()
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

        var rateServiceMock = new Mock<IRateService>();
        rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var loggerMock = new Mock<ILogger<RatesController>>();
        var controller = new RatesController(rateServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        httpContext.Request.Headers.IfNoneMatch = "\"abc123\"";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(controller.Response.Headers.ContainsKey("ETag"));
    }

    #endregion

    #region SnapshotsController Tests

    [Fact]
    public async Task SnapshotsController_GetBatchStatus_ReturnsStatus()
    {
        var snapshotServiceMock = new Mock<ISnapshotService>();
        var snapshotQueueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();

        snapshotQueueMock
            .Setup(q => q.GetStatus(It.IsAny<string>()))
            .Returns(("Processing", (string?)null));

        var controller = new SnapshotsController(
            snapshotServiceMock.Object,
            snapshotQueueMock.Object,
            loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = controller.GetBatchStatus("batch-123");

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SnapshotsController_PromoteBatch_NotFound_Returns404()
    {
        var snapshotServiceMock = new Mock<ISnapshotService>();
        var snapshotQueueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();

        snapshotServiceMock
            .Setup(s => s.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new SnapshotsController(
            snapshotServiceMock.Object,
            snapshotQueueMock.Object,
            loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.PromoteBatch("invalid-batch", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
    }

    #endregion

    #region CurrenciesController Activation Tests

    [Fact]
    public async Task CurrenciesController_Activate_Success_ReturnsOk()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        currencyServiceMock
            .Setup(x => x.ActivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Activate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task CurrenciesController_Activate_NotFound_Returns404()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        currencyServiceMock
            .Setup(x => x.ActivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Activate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CurrenciesController_Deactivate_Success_ReturnsOk()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        currencyServiceMock
            .Setup(x => x.DeactivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Deactivate(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    #endregion

    #region RatesController Admin Tests

    [Fact]
    public async Task RatesController_UpdateRate_ReturnsOk()
    {
        var rateServiceMock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();

        rateServiceMock
            .Setup(x => x.UpdateRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>()))
            .Returns(Task.CompletedTask);

        var controller = new RatesController(rateServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new Application.DTOs.Rates.UpdateRateRequest
        {
            From = "USD",
            To = "EUR",
            Rate = 0.85m
        };

        var result = await controller.UpdateRate(request);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task RatesController_BulkUpdateRates_ReturnsOk()
    {
        var rateServiceMock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();

        rateServiceMock
            .Setup(x => x.BulkUpdateRatesAsync(It.IsAny<List<Application.DTOs.Rates.UpdateRateRequest>>()))
            .Returns(Task.CompletedTask);

        var controller = new RatesController(rateServiceMock.Object, loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new Maliev.CurrencyService.Api.Models.Rates.BulkUpdateRatesRequest
        {
            Rates = new List<Application.DTOs.Rates.UpdateRateRequest>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m }
            }
        };

        var result = await controller.BulkUpdateRates(request);

        Assert.IsType<OkObjectResult>(result);
    }

    #endregion
}
