using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.CurrencyService.Tests;

public class AdditionalRatesControllerTests
{
    private readonly Mock<IRateService> _rateServiceMock;
    private readonly Mock<ILogger<RatesController>> _loggerMock;
    private readonly RatesController _controller;

    public AdditionalRatesControllerTests()
    {
        _rateServiceMock = new Mock<IRateService>();
        _loggerMock = new Mock<ILogger<RatesController>>();
        _controller = new RatesController(_rateServiceMock.Object, _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "additional-test-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task GetExchangeRate_IfNoneMatchMatches_Returns304NotModified()
    {
        var timestamp = DateTime.UtcNow;
        var rateResponse = new ExchangeRateResponse
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
            .ReturnsAsync(rateResponse);

        var expectedEtag = Maliev.CurrencyService.Api.Models.Common.ETagHelper.GenerateETag(rateResponse);
        _controller.Request.Headers.IfNoneMatch = $"\"{expectedEtag}\"";

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var statusResult = Assert.IsType<StatusCodeResult>(result.Result);
        Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetExchangeRate_IfModifiedSinceNotModified_Returns304()
    {
        var oldTimestamp = DateTime.UtcNow.AddHours(-2);
        var rateResponse = new ExchangeRateResponse
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
            .ReturnsAsync(rateResponse);

        _controller.Request.Headers.IfModifiedSince = DateTime.UtcNow.ToString("R");

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var statusResult = Assert.IsType<StatusCodeResult>(result.Result);
        Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
    }
}

public class AdditionalCurrenciesControllerTests
{
    private readonly Mock<ICurrencyService> _currencyServiceMock;
    private readonly Mock<ILogger<CurrenciesController>> _loggerMock;
    private readonly CurrenciesController _controller;

    public AdditionalCurrenciesControllerTests()
    {
        _currencyServiceMock = new Mock<ICurrencyService>();
        _loggerMock = new Mock<ILogger<CurrenciesController>>();
        _controller = new CurrenciesController(_currencyServiceMock.Object, _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "additional-test-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task GetCurrencyById_ServiceException_Returns500WithCorrelationId()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var result = await _controller.GetCurrencyById(id, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.NotNull(errorResponse.CorrelationId);
    }

    [Fact]
    public async Task GetByCode_ServiceException_Returns500WithCorrelationId()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var result = await _controller.GetByCode("USD", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.NotNull(errorResponse.CorrelationId);
    }

    [Fact]
    public async Task GetCurrencyByCountry_ServiceException_Returns500WithCorrelationId()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCountryCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var result = await _controller.GetCurrencyByCountry("TH", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.NotNull(errorResponse.CorrelationId);
    }

    [Fact]
    public async Task GetCurrencyByCountryPath_ServiceException_Returns500WithCorrelationId()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCountryCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var result = await _controller.GetCurrencyByCountryPath("TH", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.NotNull(errorResponse.CorrelationId);
    }

    [Fact]
    public async Task GetById_Admin_ServiceException_Returns500WithCorrelationId()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var result = await _controller.GetById(id, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.NotNull(errorResponse.CorrelationId);
    }

    [Fact]
    public async Task CreateAdmin_ServiceException_Returns500WithCorrelationId()
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
            .ThrowsAsync(new Exception("Database connection failed"));

        var result = await _controller.CreateAdmin(request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.NotNull(errorResponse.CorrelationId);
    }

    [Fact]
    public async Task Update_ServiceException_Returns500WithCorrelationId()
    {
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        _currencyServiceMock
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var result = await _controller.Update("USD", request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.NotNull(errorResponse.CorrelationId);
    }

    [Fact]
    public async Task UpdateById_UpdateServiceException_Returns500WithCorrelationId()
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
            .SetupSequence(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCurrency)
            .ReturnsAsync(existingCurrency);

        _currencyServiceMock
            .Setup(x => x.UpdateByIdAsync(id, It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var etag = Maliev.CurrencyService.Api.Models.Common.ETagHelper.GenerateETag(existingCurrency);
        _controller.Request.Headers.IfMatch = $"\"{etag}\"";

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.NotNull(errorResponse.CorrelationId);
    }

    [Fact]
    public async Task Delete_ServiceException_Returns500WithCorrelationId()
    {
        _currencyServiceMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var result = await _controller.Delete("USD", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.NotNull(errorResponse.CorrelationId);
    }

    [Fact]
    public async Task DeleteById_ServiceException_Returns500WithCorrelationId()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.DeleteByIdAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var result = await _controller.DeleteById(id, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.NotNull(errorResponse.CorrelationId);
    }
}

public class AdditionalSnapshotsControllerTests
{
    private readonly Mock<ISnapshotService> _snapshotServiceMock;
    private readonly Mock<ISnapshotQueue> _snapshotQueueMock;
    private readonly Mock<ILogger<SnapshotsController>> _loggerMock;
    private readonly SnapshotsController _controller;

    public AdditionalSnapshotsControllerTests()
    {
        _snapshotServiceMock = new Mock<ISnapshotService>();
        _snapshotQueueMock = new Mock<ISnapshotQueue>();
        _loggerMock = new Mock<ILogger<SnapshotsController>>();
        _controller = new SnapshotsController(
            _snapshotServiceMock.Object,
            _snapshotQueueMock.Object,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "additional-test-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task ImportBatch_ValidBatchWithDate_ReturnsAccepted()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = "2025-01-15T00:00:00Z" },
            new SnapshotEntryDto { From = "USD", To = "GBP", Rate = 0.75m, Timestamp = "2025-01-15T00:00:00Z" }
        };

        var batchResponse = new SnapshotBatchResponse
        {
            BatchId = "batch-456",
            SnapshotDate = DateOnly.FromDateTime(new DateTime(2025, 1, 15)),
            Source = "AdminApi",
            SuccessCount = 2,
            FailureCount = 0,
            Status = "staged"
        };

        _snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await _controller.ImportBatch(snapshots, false, CancellationToken.None);

        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        Assert.NotNull(acceptedResult);
    }

    [Fact]
    public async Task ImportBatch_DryRunWithNoErrors_ReturnsOkWithValidFlag()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = "2025-01-15T00:00:00Z" }
        };

        var batchResponse = new SnapshotBatchResponse
        {
            BatchId = "batch-789",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "AdminApi",
            SuccessCount = 1,
            FailureCount = 0,
            Status = "validated"
        };

        _snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await _controller.ImportBatch(snapshots, true, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ImportBatch_ValidationErrors_DryRun_ReturnsOkWithErrors()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "", To = "EUR", Rate = -1m, Timestamp = "invalid" }
        };

        var batchResponse = new SnapshotBatchResponse
        {
            BatchId = "batch-101",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "AdminApi",
            SuccessCount = 0,
            FailureCount = 1,
            Status = "failed",
            Errors = new Dictionary<string, string[]>
            {
                { "0", new[] { "Invalid from currency code", "Rate must be positive", "Invalid timestamp" } }
            }
        };

        _snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await _controller.ImportBatch(snapshots, true, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task PromoteBatch_BatchPromoted_ReturnsOkWithMessage()
    {
        var batchId = "batch-promote-123";

        _snapshotServiceMock
            .Setup(x => x.PromoteBatchAsync(batchId, null, "System", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.PromoteBatch(batchId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task CleanupOldSnapshots_ReturnsDeletedCountAndMessage()
    {
        _snapshotServiceMock
            .Setup(x => x.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(150);

        var result = await _controller.CleanupOldSnapshots(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void GetBatchStatus_ReturnsStatusAndError()
    {
        var batchId = "batch-status-123";

        _snapshotQueueMock
            .Setup(x => x.GetStatus(batchId))
            .Returns(("Failed", "Validation error occurred"));

        var result = _controller.GetBatchStatus(batchId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetBatchAudit_NotFound_ReturnsNotFound()
    {
        var batchId = "nonexistent-batch";

        _snapshotServiceMock
            .Setup(x => x.GetBatchAuditAsync(batchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Application.DTOs.Snapshots.SnapshotAuditLog?)null);

        var result = await _controller.GetBatchAudit(batchId, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }
}
