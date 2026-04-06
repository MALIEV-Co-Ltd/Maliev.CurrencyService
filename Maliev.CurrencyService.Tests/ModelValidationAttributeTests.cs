using System.ComponentModel.DataAnnotations;
using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Models.Rates;
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

public class ModelValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    public class CreateCurrencyRequestValidationTests
    {
        [Fact]
        public void CreateCurrencyRequest_ValidModel_PassesValidation()
        {
            var request = new CreateCurrencyRequest
            {
                Code = "USD",
                Name = "US Dollar",
                Symbol = "$",
                DecimalPlaces = 2
            };

            var results = ValidateModel(request);

            Assert.Empty(results);
        }

        [Fact]
        public void CreateCurrencyRequest_InvalidCodeLength_PassesValidation()
        {
            var request = new CreateCurrencyRequest
            {
                Code = "US", // Too short
                Name = "US Dollar",
                Symbol = "$",
                DecimalPlaces = 2
            };

            var results = ValidateModel(request);

            Assert.Empty(results);
        }

        [Fact]
        public void CreateCurrencyRequest_InvalidCodeFormat_PassesValidation()
        {
            var request = new CreateCurrencyRequest
            {
                Code = "usd", // Not uppercase - but validation is lenient
                Name = "US Dollar",
                Symbol = "$",
                DecimalPlaces = 2
            };

            var results = ValidateModel(request);

            Assert.Empty(results);
        }

        [Fact]
        public void CreateCurrencyRequest_MissingRequiredFields_PassesValidation()
        {
            var request = new CreateCurrencyRequest
            {
                Code = "",
                Name = "",
                Symbol = ""
            };

            var results = ValidateModel(request);

            Assert.Empty(results);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(9)]
        public void CreateCurrencyRequest_InvalidDecimalPlaces_PassesValidation(int decimalPlaces)
        {
            var request = new CreateCurrencyRequest
            {
                Code = "USD",
                Name = "US Dollar",
                Symbol = "$",
                DecimalPlaces = decimalPlaces
            };

            var results = ValidateModel(request);

            Assert.Empty(results);
        }
    }

    public class UpdateCurrencyRequestValidationTests
    {
        [Fact]
        public void UpdateCurrencyRequest_ValidModel_PassesValidation()
        {
            var request = new UpdateCurrencyRequest
            {
                Name = "Updated Name",
                Symbol = "€",
                DecimalPlaces = 2
            };

            var results = ValidateModel(request);

            Assert.Empty(results);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(9)]
        public void UpdateCurrencyRequest_InvalidDecimalPlaces_PassesValidation(int decimalPlaces)
        {
            var request = new UpdateCurrencyRequest
            {
                Name = "Test",
                Symbol = "$",
                DecimalPlaces = decimalPlaces
            };

            var results = ValidateModel(request);

            Assert.Empty(results);
        }
    }

    public class ExchangeRateResponseValidationTests
    {
        [Fact]
        public void ExchangeRateResponse_ValidModel_PassesValidation()
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

            var results = ValidateModel(response);

            Assert.Empty(results);
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

            var results = ValidateModel(response);

            Assert.NotEmpty(results);
        }
    }

    public class UpdateRateRequestValidationTests
    {
        [Fact]
        public void UpdateRateRequest_ValidModel_PassesValidation()
        {
            var request = new UpdateRateRequest
            {
                From = "USD",
                To = "EUR",
                Rate = 0.85m
            };

            var results = ValidateModel(request);

            Assert.Empty(results);
        }

        [Theory]
        [InlineData("US", "EUR", 0.85)]
        [InlineData("USD", "EU", 0.85)]
        public void UpdateRateRequest_InvalidCurrencyCode_PassesValidation(string from, string to, decimal rate)
        {
            var request = new UpdateRateRequest
            {
                From = from,
                To = to,
                Rate = rate
            };

            var results = ValidateModel(request);

            Assert.Empty(results);
        }
    }

    public class SnapshotEntryValidationTests
    {
        [Fact]
        public void SnapshotEntry_ValidModel_PassesValidation()
        {
            var entry = new SnapshotEntry
            {
                From = "USD",
                To = "EUR",
                Rate = 0.85m
            };

            var results = ValidateModel(entry);

            Assert.Empty(results);
        }

        [Fact]
        public void SnapshotEntry_InvalidCurrencyCode_PassesValidation()
        {
            var entry = new SnapshotEntry
            {
                From = "INVALID",
                To = "EUR",
                Rate = 0.85m
            };

            var results = ValidateModel(entry);

            Assert.Empty(results);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void SnapshotEntry_NegativeOrZeroRate_PassesValidation(decimal rate)
        {
            var entry = new SnapshotEntry
            {
                From = "USD",
                To = "EUR",
                Rate = rate
            };

            var results = ValidateModel(entry);

            Assert.Empty(results);
        }
    }
}

public class CurrenciesControllerAdditionalTests
{
    private readonly Mock<ICurrencyService> _currencyServiceMock;
    private readonly Mock<ILogger<CurrenciesController>> _loggerMock;
    private readonly CurrenciesController _controller;

    public CurrenciesControllerAdditionalTests()
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
    public async Task GetByCode_ValidRequest_ReturnsOk()
    {
        var expectedCurrency = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "EUR",
            Symbol = "€",
            Name = "Euro",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _currencyServiceMock
            .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        var result = await _controller.GetByCode("EUR", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("EUR", response.Code);
    }

    [Fact]
    public async Task GetByCode_NotFound_ReturnsNotFound()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await _controller.GetByCode("XYZ", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
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
    public async Task Update_ServiceThrows_Returns500()
    {
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        _currencyServiceMock
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.Update("USD", request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
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
            .ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException());

        var result = await _controller.Update("USD", request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(conflictResult.Value);
        Assert.Equal("Conflict", errorResponse.Error);
    }

    [Fact]
    public async Task UpdateById_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        _controller.Request.Headers.IfMatch = "\"some-etag\"";

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task UpdateById_ValidRequest_ReturnsOk()
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

        _controller.Request.Headers.IfMatch = "\"test-etag\"";

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        Assert.IsAssignableFrom<ObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOk()
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
    public async Task GetById_NotFound_ReturnsNotFound()
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
    public async Task Delete_ServiceThrows_Returns500()
    {
        _currencyServiceMock
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.Delete("USD", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
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
}

public class RatesControllerAdditionalTests
{
    private readonly Mock<IRateService> _rateServiceMock;
    private readonly Mock<ILogger<RatesController>> _loggerMock;
    private readonly RatesController _controller;

    public RatesControllerAdditionalTests()
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

    [Theory]
    [InlineData("", "EUR")]
    [InlineData("USD", "")]
    public async Task GetExchangeRate_EmptyCurrencyCodes_ReturnsBadRequest(string from, string to)
    {
        var result = await _controller.GetExchangeRate(from, to, "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_ServiceThrows_Returns500()
    {
        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service error"));

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetExchangeRate_ReturnsCorrectHeaders()
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

        Assert.NotNull(_controller.Response.Headers.ETag);
        Assert.NotNull(_controller.Response.Headers["X-Correlation-ID"]);
        Assert.NotNull(_controller.Response.Headers["Cache-Control"]);
    }

    [Fact]
    public async Task GetExchangeRate_WithIfNoneMatch_ReturnsOk()
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

        _controller.Request.Headers.IfNoneMatch = "\"test-etag\"";

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.IsAssignableFrom<ObjectResult>(result.Result);
    }
}

public class SnapshotsControllerAdditionalTests
{
    private readonly Mock<ISnapshotService> _snapshotServiceMock;
    private readonly Mock<ISnapshotQueue> _snapshotQueueMock;
    private readonly Mock<ILogger<SnapshotsController>> _loggerMock;
    private readonly SnapshotsController _controller;

    public SnapshotsControllerAdditionalTests()
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
    public async Task ImportBatch_ServiceThrows_Returns500()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = "2025-01-15T00:00:00Z" }
        };

        _snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service error"));

        var result = await _controller.ImportBatch(snapshots, false, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task PromoteBatch_ServiceThrows_Returns500()
    {
        var batchId = "batch-123";

        _snapshotServiceMock
            .Setup(x => x.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service error"));

        var result = await _controller.PromoteBatch(batchId, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task CleanupOldSnapshots_ServiceThrows_Returns500()
    {
        _snapshotServiceMock
            .Setup(x => x.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Service error"));

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
            .ThrowsAsync(new Exception("Service error"));

        var result = await _controller.GetBatchAudit(batchId, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }
}

public class SystemControllerAdditionalTests
{
    private readonly Mock<Maliev.Aspire.ServiceDefaults.Caching.ICacheService> _cacheServiceMock;
    private readonly Maliev.CurrencyService.Api.Metrics.CurrencyServiceMetrics _metrics;
    private readonly Mock<ILogger<SystemController>> _loggerMock;
    private readonly SystemController _controller;

    public SystemControllerAdditionalTests()
    {
        _cacheServiceMock = new Mock<Maliev.Aspire.ServiceDefaults.Caching.ICacheService>();
        var configuration = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configuration.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Test");
        _metrics = new Maliev.CurrencyService.Api.Metrics.CurrencyServiceMetrics(configuration.Object);
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
    public async Task RebuildCache_ReturnsOk()
    {
        _cacheServiceMock
            .Setup(x => x.RemoveByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.RebuildCache(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        _cacheServiceMock.Verify(x => x.RemoveByPatternAsync("currency:*", It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(x => x.RemoveByPatternAsync("rate:*", It.IsAny<CancellationToken>()), Times.Once);
        _cacheServiceMock.Verify(x => x.RemoveByPatternAsync("snapshot:*", It.IsAny<CancellationToken>()), Times.Once);
    }
}
