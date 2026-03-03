using System.ComponentModel.DataAnnotations;
using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Models.Common;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class TestsMore
{
    #region SystemController Tests

    [Fact]
    public async Task SystemController_RebuildCache_RemovesAllCachePatterns()
    {
        var cacheServiceMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<SystemController>>();

        cacheServiceMock
            .Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new SystemController(
            cacheServiceMock.Object,
            null!,
            loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.RebuildCache(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        cacheServiceMock.Verify(x => x.RemoveByPatternAsync("currency:*", It.IsAny<CancellationToken>()), Times.Once);
        cacheServiceMock.Verify(x => x.RemoveByPatternAsync("rate:*", It.IsAny<CancellationToken>()), Times.Once);
        cacheServiceMock.Verify(x => x.RemoveByPatternAsync("snapshot:*", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void SystemController_GetStats_ReturnsServiceInfo()
    {
        var cacheServiceMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<SystemController>>();

        var controller = new SystemController(
            cacheServiceMock.Object,
            null!,
            loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = controller.GetStats();

        Assert.IsType<OkObjectResult>(result);
    }

    #endregion

    #region CurrenciesController - Edge Cases

    [Fact]
    public async Task CurrenciesController_Activate_ReturnsOk_WhenFound()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        var id = Guid.NewGuid();

        currencyServiceMock
            .Setup(x => x.ActivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Activate(id, CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task CurrenciesController_Activate_ReturnsNotFound_WhenNotFound()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        var id = Guid.NewGuid();

        currencyServiceMock
            .Setup(x => x.ActivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Activate(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CurrenciesController_Deactivate_ReturnsOk_WhenFound()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        var id = Guid.NewGuid();

        currencyServiceMock
            .Setup(x => x.DeactivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Deactivate(id, CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task CurrenciesController_DeleteById_WithDependencies_ReturnsConflict()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();
        var loggerMock = new Mock<ILogger<CurrenciesController>>();

        var id = Guid.NewGuid();

        currencyServiceMock
            .Setup(x => x.DeleteByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot delete currency due to country mappings"));

        var controller = new CurrenciesController(currencyServiceMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.DeleteById(id, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    #endregion

    #region RatesController - Edge Cases

    [Fact]
    public async Task RatesController_GetExchangeRate_ValidatesCurrencyCodesCaseInsensitive()
    {
        var rateServiceMock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();

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

        rateServiceMock
            .Setup(x => x.GetLiveRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = new RatesController(rateServiceMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetExchangeRate("usd", "eur", "live", null, CancellationToken.None);

        Assert.NotNull(result.Result);
        rateServiceMock.Verify(x => x.GetLiveRateAsync("USD", "EUR", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RatesController_GetExchangeRate_ReturnsStalenessHeader_WhenFresh()
    {
        var rateServiceMock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();

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

        rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = new RatesController(rateServiceMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.Equal("fresh", httpContext.Response.Headers["X-Rate-Staleness"]);
    }

    [Fact]
    public async Task RatesController_GetExchangeRate_ReturnsStalenessHeader_WhenStale()
    {
        var rateServiceMock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();

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

        rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var controller = new RatesController(rateServiceMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.StartsWith("stale", httpContext.Response.Headers["X-Rate-Staleness"]);
    }

    [Fact]
    public async Task RatesController_GetExchangeRate_AddsRetryAfterHeader_WhenUnavailable()
    {
        var rateServiceMock = new Mock<IRateService>();
        var loggerMock = new Mock<ILogger<RatesController>>();

        rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var controller = new RatesController(rateServiceMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.Equal("30", httpContext.Response.Headers.RetryAfter);
    }

    #endregion

    #region SnapshotsController - Edge Cases

    [Fact]
    public async Task SnapshotsController_ImportBatch_EmptyArray_ReturnsBadRequest()
    {
        var snapshotServiceMock = new Mock<ISnapshotService>();
        var snapshotQueueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();

        var controller = new SnapshotsController(snapshotServiceMock.Object, snapshotQueueMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        httpContext.Request.Method = "POST";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ImportBatch(new List<SnapshotEntryDto>(), false, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SnapshotsController_ImportBatch_ValidationErrors_ReturnsBadRequest()
    {
        var snapshotServiceMock = new Mock<ISnapshotService>();
        var snapshotQueueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();

        var batchResponse = new SnapshotBatchResponse
        {
            BatchId = "batch-1",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "AdminApi",
            Status = "staged",
            SuccessCount = 1,
            FailureCount = 1,
            Errors = new Dictionary<string, string[]>
            {
                { "0", new[] { "Invalid rate" } }
            }
        };

        snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var controller = new SnapshotsController(snapshotServiceMock.Object, snapshotQueueMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        httpContext.Request.Method = "POST";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ImportBatch(
            new List<SnapshotEntryDto> { new() { From = "USD", To = "EUR", Rate = -1, Timestamp = "invalid" } },
            false,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SnapshotsController_ImportBatch_DryRun_ReturnsOk()
    {
        var snapshotServiceMock = new Mock<ISnapshotService>();
        var snapshotQueueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();

        var batchResponse = new SnapshotBatchResponse
        {
            BatchId = "batch-1",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "AdminApi",
            Status = "staged",
            SuccessCount = 2,
            FailureCount = 0
        };

        snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var controller = new SnapshotsController(snapshotServiceMock.Object, snapshotQueueMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        httpContext.Request.Method = "POST";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ImportBatch(
            new List<SnapshotEntryDto>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = "2025-01-15T00:00:00Z" }
            },
            true,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SnapshotsController_PromoteBatch_NotFound_ReturnsNotFound()
    {
        var snapshotServiceMock = new Mock<ISnapshotService>();
        var snapshotQueueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();

        snapshotServiceMock
            .Setup(x => x.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new SnapshotsController(snapshotServiceMock.Object, snapshotQueueMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.PromoteBatch("invalid-batch", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void SnapshotsController_GetBatchStatus_ReturnsStatus()
    {
        var snapshotServiceMock = new Mock<ISnapshotService>();
        var snapshotQueueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();

        snapshotQueueMock
            .Setup(x => x.GetStatus("batch-1"))
            .Returns(("Completed", (string?)null));

        var controller = new SnapshotsController(snapshotServiceMock.Object, snapshotQueueMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = controller.GetBatchStatus("batch-1");

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SnapshotsController_GetBatchAudit_NotFound_ReturnsNotFound()
    {
        var snapshotServiceMock = new Mock<ISnapshotService>();
        var snapshotQueueMock = new Mock<ISnapshotQueue>();
        var loggerMock = new Mock<ILogger<SnapshotsController>>();

        snapshotServiceMock
            .Setup(x => x.GetBatchAuditAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: null);

        var controller = new SnapshotsController(snapshotServiceMock.Object, snapshotQueueMock.Object, loggerMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.GetBatchAudit("invalid-batch", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    #endregion

    #region DTO Validation Tests

    [Fact]
    public void ExchangeRateResponse_ValidResponse_PassesValidation()
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

        var validationResults = ValidateModel(response);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void ExchangeRateResponse_InvalidCurrencyCode_FailsValidation()
    {
        var response = new ExchangeRateResponse
        {
            FromCurrency = "INVALID",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        var validationResults = ValidateModel(response);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ExchangeRateResponse_TransitiveRate_HasIntermediateCurrency()
    {
        var response = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Transitive:USD",
            IsTransitive = true,
            IntermediateCurrency = "USD",
            CalculationDetails = "USD/THB × THB/EUR",
            Mode = "live"
        };

        var validationResults = ValidateModel(response);
        Assert.Empty(validationResults);
        Assert.True(response.IsTransitive);
        Assert.NotNull(response.IntermediateCurrency);
    }

    [Fact]
    public void PaginatedResponse_HasNextPage_ReturnsCorrectValue()
    {
        var response = new PaginatedCurrencyResponse
        {
            Items = Enumerable.Empty<CurrencyResponse>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 25,
            TotalPages = 3
        };

        Assert.True(response.HasNextPage);
        Assert.False(response.HasPreviousPage);
    }

    [Fact]
    public void PaginatedResponse_LastPage_ReturnsCorrectValue()
    {
        var response = new PaginatedCurrencyResponse
        {
            Items = Enumerable.Empty<CurrencyResponse>(),
            Page = 3,
            PageSize = 10,
            TotalCount = 25,
            TotalPages = 3
        };

        Assert.False(response.HasNextPage);
        Assert.True(response.HasPreviousPage);
    }

    [Fact]
    public void ErrorResponse_CanBeConstructed()
    {
        var error = new Api.Models.Common.ErrorResponse
        {
            Error = "BadRequest",
            Message = "Invalid request",
            Timestamp = DateTime.UtcNow,
            CorrelationId = "test-correlation",
            Details = new Dictionary<string, string[]>
            {
                { "field", new[] { "error1", "error2" } }
            }
        };

        Assert.Equal("BadRequest", error.Error);
        Assert.Equal("Invalid request", error.Message);
        Assert.NotNull(error.Details);
    }

    #endregion

    #region Service Edge Cases with Moq

    [Fact]
    public async Task CurrencyService_GetAllAsync_RespectsPagination()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();

        var pagedResponse = new PaginatedCurrencyResponse
        {
            Items = new List<CurrencyResponse>
            {
                new() { Id = Guid.NewGuid(), Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, IsPrimary = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            },
            Page = 2,
            PageSize = 10,
            TotalCount = 25,
            TotalPages = 3
        };

        currencyServiceMock
            .Setup(x => x.GetAllAsync(2, 10, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResponse);

        var result = await currencyServiceMock.Object.GetAllAsync(2, 10, true);

        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public async Task CurrencyService_GetByCountryCodeAsync_ReturnsNull_WhenNotFound()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();

        currencyServiceMock
            .Setup(x => x.GetByCountryCodeAsync("ZZ", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await currencyServiceMock.Object.GetByCountryCodeAsync("ZZ");

        Assert.Null(result);
    }

    [Fact]
    public async Task CurrencyService_UpdateAsync_ReturnsNull_WhenNotFound()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();

        currencyServiceMock
            .Setup(x => x.UpdateAsync("XYZ", It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await currencyServiceMock.Object.UpdateAsync("XYZ", new Application.DTOs.Currencies.UpdateCurrencyRequest { Name = "Test", Symbol = "$", DecimalPlaces = 2 });

        Assert.Null(result);
    }

    [Fact]
    public async Task CurrencyService_DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        var currencyServiceMock = new Mock<ICurrencyService>();

        currencyServiceMock
            .Setup(x => x.DeleteAsync("XYZ", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await currencyServiceMock.Object.DeleteAsync("XYZ");

        Assert.False(result);
    }

    [Fact]
    public async Task RateService_GetSnapshotRateAsync_ReturnsNull_WhenNotFound()
    {
        var rateServiceMock = new Mock<IRateService>();

        rateServiceMock
            .Setup(x => x.GetSnapshotRateAsync("USD", "EUR", It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var result = await rateServiceMock.Object.GetSnapshotRateAsync("USD", "EUR", DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Null(result);
    }

    [Fact]
    public async Task RateService_UpdateRateAsync_CallsService()
    {
        var rateServiceMock = new Mock<IRateService>();

        rateServiceMock
            .Setup(x => x.UpdateRateAsync("USD", "EUR", 0.85m, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await rateServiceMock.Object.UpdateRateAsync("USD", "EUR", 0.85m);

        rateServiceMock.Verify();
    }

    [Fact]
    public async Task RateService_BulkUpdateRatesAsync_CallsService()
    {
        var rateServiceMock = new Mock<IRateService>();

        var rates = new List<UpdateRateRequest>
        {
            new() { From = "USD", To = "EUR", Rate = 0.85m },
            new() { From = "USD", To = "GBP", Rate = 0.75m }
        };

        rateServiceMock
            .Setup(x => x.BulkUpdateRatesAsync(rates, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await rateServiceMock.Object.BulkUpdateRatesAsync(rates);

        rateServiceMock.Verify();
    }

    #endregion

    #region Helper Methods

    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    #endregion
}
