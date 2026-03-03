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

        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "test-trace-id";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task GetExchangeRate_ValidRequest_ReturnsOk()
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
        var response = Assert.IsType<ExchangeRateResponse>(okResult.Value);
        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("EUR", response.ToCurrency);
    }

    [Fact]
    public async Task GetExchangeRate_InvalidFromCurrency_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("INVALID", "EUR", "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_InvalidToCurrency_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("USD", "XX", "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_InvalidMode_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("USD", "EUR", "invalid", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_SnapshotModeWithoutDate_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("USD", "EUR", "snapshot", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_SnapshotNotFound_ReturnsNotFound()
    {
        _rateServiceMock
            .Setup(x => x.GetSnapshotRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var result = await _controller.GetExchangeRate("USD", "EUR", "snapshot", DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_LiveRateUnavailable_Returns503()
    {
        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(statusResult.Value);
        Assert.Equal("ServiceUnavailable", errorResponse.Error);
    }

    [Fact]
    public async Task GetExchangeRate_ValidSnapshotRequest_ReturnsOk()
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
        var response = Assert.IsType<ExchangeRateResponse>(okResult.Value);
        Assert.Equal("snapshot", response.Mode);
    }

    [Fact]
    public async Task UpdateRate_ValidRequest_ReturnsOk()
    {
        var request = new UpdateRateRequest
        {
            From = "USD",
            To = "EUR",
            Rate = 0.9m
        };

        _rateServiceMock
            .Setup(x => x.UpdateRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.UpdateRate(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task BulkUpdateRates_ValidRequest_ReturnsOk()
    {
        var request = new Maliev.CurrencyService.Api.Models.Rates.BulkUpdateRatesRequest
        {
            Rates = new List<UpdateRateRequest>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m }
            }
        };

        _rateServiceMock
            .Setup(x => x.BulkUpdateRatesAsync(It.IsAny<List<UpdateRateRequest>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.BulkUpdateRates(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SetRateSource_ValidRequest_ReturnsOk()
    {
        var request = new Maliev.CurrencyService.Api.Models.Rates.SetRateSourceRequest
        {
            ProviderName = "Fawazahmed"
        };

        var result = await _controller.SetRateSource(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task RefreshRatesFromProvider_ReturnsAccepted()
    {
        var result = await _controller.RefreshRatesFromProvider();

        Assert.IsType<AcceptedResult>(result);
    }
}

public class CurrenciesControllerTests
{
    private readonly Mock<ICurrencyService> _currencyServiceMock;
    private readonly Mock<ILogger<CurrenciesController>> _loggerMock;
    private readonly CurrenciesController _controller;

    public CurrenciesControllerTests()
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
    public async Task ListCurrencies_ValidRequest_ReturnsOk()
    {
        var expectedResponse = new PaginatedCurrencyResponse
        {
            Items = new List<CurrencyResponse>
            {
                new CurrencyResponse
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
                }
            },
            Page = 1,
            PageSize = 50,
            TotalCount = 1,
            TotalPages = 1
        };

        _currencyServiceMock
            .Setup(x => x.GetAllAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.ListCurrencies(1, 50, null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PaginatedCurrencyResponse>(okResult.Value);
        Assert.Single(response.Items);
    }

    [Fact]
    public async Task ListCurrencies_ServiceThrows_Returns500()
    {
        _currencyServiceMock
            .Setup(x => x.GetAllAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.ListCurrencies(1, 50, null, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetCurrencyById_ExistingId_ReturnsOk()
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
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("USD", response.Code);
    }

    [Fact]
    public async Task GetCurrencyById_NonExistingId_ReturnsNotFound()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await _controller.GetCurrencyById(id, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task GetByCode_ExistingCode_ReturnsOk()
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
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("USD", response.Code);
    }

    [Fact]
    public async Task GetByCode_NonExistingCode_ReturnsNotFound()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await _controller.GetByCode("XXX", CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task GetCurrencyByCountry_ValidCountryCode_ReturnsOk()
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

        var result = await _controller.GetCurrencyByCountry("TH", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("THB", response.Code);
    }

    [Fact]
    public async Task GetCurrencyByCountry_InvalidCountryCode_ReturnsBadRequest()
    {
        var result = await _controller.GetCurrencyByCountry("INVALID", CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task GetCurrencyByCountry_NotFound_ReturnsNotFound()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCountryCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await _controller.GetCurrencyByCountry("XX", CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task CreateAdmin_ValidRequest_ReturnsCreated()
    {
        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "JPY",
            Symbol = "¥",
            Name = "Japanese Yen",
            DecimalPlaces = 0
        };

        var createdCurrency = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "JPY",
            Symbol = "¥",
            Name = "Japanese Yen",
            DecimalPlaces = 0,
            IsActive = true,
            IsPrimary = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _currencyServiceMock
            .Setup(x => x.CreateAsync(It.IsAny<Application.DTOs.Currencies.CreateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdCurrency);

        var result = await _controller.CreateAdmin(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<CurrencyResponse>(createdResult.Value);
        Assert.Equal("JPY", response.Code);
    }

    [Fact]
    public async Task CreateAdmin_InvalidRequest_ReturnsBadRequest()
    {
        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "INVALID",
            Symbol = "",
            Name = "",
            DecimalPlaces = -1
        };

        var result = await _controller.CreateAdmin(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task CreateAdmin_CurrencyExists_ReturnsConflict()
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
    public async Task Update_ValidRequest_ReturnsOk()
    {
        var code = "USD";
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "US Dollar Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        var updatedCurrency = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            Name = "US Dollar Updated",
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
        Assert.Equal("US Dollar Updated", response.Name);
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
    public async Task Update_CurrencyNotFound_ReturnsNotFound()
    {
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        _currencyServiceMock
            .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await _controller.Update("XXX", request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
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
    public async Task Delete_CurrencyNotFound_ReturnsNotFound()
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
    public async Task DeleteById_ValidId_ReturnsNoContent()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.DeleteByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DeleteById(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteById_CannotDeleteDueToDependencies_ReturnsConflict()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.DeleteByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot delete currency due to country mappings"));

        var result = await _controller.DeleteById(id, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(conflictResult.Value);
        Assert.Equal("Conflict", errorResponse.Error);
    }

    [Fact]
    public async Task Activate_ValidId_ReturnsOk()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.ActivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Activate(id, CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Activate_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.ActivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Activate(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Deactivate_ValidId_ReturnsOk()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.DeactivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.Deactivate(id, CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Deactivate_NotFound_ReturnsNotFound()
    {
        var id = Guid.NewGuid();

        _currencyServiceMock
            .Setup(x => x.DeactivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.Deactivate(id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateById_MissingIfMatchHeader_Returns412()
    {
        var id = Guid.NewGuid();
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        _controller.Request.Headers.Remove("If-Match");

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, statusResult.StatusCode);
    }

    [Fact]
    public async Task UpdateById_ETagMismatch_Returns412()
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

        _controller.Request.Headers.IfMatch = "\"wrong-etag\"";

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, statusResult.StatusCode);
    }
}

public class SnapshotsControllerTests
{
    private readonly Mock<ISnapshotService> _snapshotServiceMock;
    private readonly Mock<ISnapshotQueue> _snapshotQueueMock;
    private readonly Mock<ILogger<SnapshotsController>> _loggerMock;
    private readonly SnapshotsController _controller;

    public SnapshotsControllerTests()
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
    public async Task ImportBatch_ValidRequest_ReturnsAccepted()
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
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await _controller.ImportBatch(snapshots, false, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task ImportBatch_EmptyArray_ReturnsBadRequest()
    {
        var result = await _controller.ImportBatch(new List<SnapshotEntryDto>(), false, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task ImportBatch_ValidDryRun_ReturnsOk()
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
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await _controller.ImportBatch(snapshots, true, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ImportBatch_ValidationFailed_ReturnsBadRequest()
    {
        var snapshots = new List<SnapshotEntryDto>
        {
            new SnapshotEntryDto { From = "", To = "EUR", Rate = -1m, Timestamp = "invalid" }
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
                { "0", new[] { "Invalid rate" } }
            }
        };

        _snapshotServiceMock
            .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(batchResponse);

        var result = await _controller.ImportBatch(snapshots, false, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PromoteBatch_ValidBatchId_ReturnsOk()
    {
        var batchId = "batch-123";

        _snapshotServiceMock
            .Setup(x => x.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.PromoteBatch(batchId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task PromoteBatch_BatchNotFound_ReturnsNotFound()
    {
        var batchId = "nonexistent-batch";

        _snapshotServiceMock
            .Setup(x => x.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.PromoteBatch(batchId, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task CleanupOldSnapshots_ReturnsOk()
    {
        _snapshotServiceMock
            .Setup(x => x.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        var result = await _controller.CleanupOldSnapshots(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetBatchStatus_ValidBatchId_ReturnsOk()
    {
        var batchId = "batch-123";

        _snapshotQueueMock
            .Setup(x => x.GetStatus(batchId))
            .Returns(("Completed", (string?)null));

        var result = _controller.GetBatchStatus(batchId);

        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetBatchAudit_ValidBatchId_ReturnsOk()
    {
        var batchId = "batch-123";

        var auditLog = new Application.DTOs.Snapshots.SnapshotAuditLog
        {
            BatchId = batchId,
            Timestamp = DateTime.UtcNow,
            RecordCount = 10,
            Source = "AdminApi",
            SubmittedBy = "admin"
        };

        _snapshotServiceMock
            .Setup(x => x.GetBatchAuditAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLog);

        var result = await _controller.GetBatchAudit(batchId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetBatchAudit_BatchNotFound_ReturnsNotFound()
    {
        var batchId = "nonexistent-batch";

        _snapshotServiceMock
            .Setup(x => x.GetBatchAuditAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Application.DTOs.Snapshots.SnapshotAuditLog?)null);

        var result = await _controller.GetBatchAudit(batchId, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }
}

public class SystemControllerTests
{
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly CurrencyServiceMetrics _metrics;
    private readonly Mock<ILogger<SystemController>> _loggerMock;
    private readonly SystemController _controller;

    public SystemControllerTests()
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
    public async Task RebuildCache_ReturnsOk()
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
    public void GetStats_ReturnsOk()
    {
        var result = _controller.GetStats();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
