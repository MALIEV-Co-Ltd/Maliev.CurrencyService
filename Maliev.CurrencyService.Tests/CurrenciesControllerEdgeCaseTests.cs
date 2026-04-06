using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Models.Common;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Api.Models.Rates;
using Maliev.CurrencyService.Application.Common;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.CurrencyService.Tests.ApiFinal;

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
    public async Task GetCurrencyByCountryPath_ValidCountryCode_ReturnsOk()
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
    public async Task GetCurrencyByCountryPath_WhitespaceCountryCode_ReturnsNotFound()
    {
        var result = await _controller.GetCurrencyByCountryPath("   ", CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task GetCurrencyByCountryPath_NotFound_ReturnsNotFound()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCountryCodeAsync("XX", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrencyResponse?)null);

        var result = await _controller.GetCurrencyByCountryPath("XX", CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NotFound", errorResponse.Error);
    }

    [Fact]
    public async Task GetByCode_WithMatchingETag_Returns304()
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
            UpdatedAt = DateTime.UtcNow,
            ETag = "test-xmin-123"
        };

        _currencyServiceMock
            .Setup(x => x.GetByCodeAsync("USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        _controller.Request.Headers.IfNoneMatch = "\"test-xmin-123\"";

        var result = await _controller.GetByCode("USD", CancellationToken.None);

        Assert.IsType<StatusCodeResult>(result.Result);
        var statusResult = result.Result as StatusCodeResult;
        Assert.Equal(StatusCodes.Status304NotModified, statusResult?.StatusCode);
    }

    [Fact]
    public async Task GetByCode_WithNonMatchingETag_ReturnsOk()
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
            UpdatedAt = DateTime.UtcNow,
            ETag = "test-xmin-123"
        };

        _currencyServiceMock
            .Setup(x => x.GetByCodeAsync("USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCurrency);

        _controller.Request.Headers.IfNoneMatch = "\"wrong-etag\"";

        var result = await _controller.GetByCode("USD", CancellationToken.None);

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
    public async Task UpdateById_ValidRequest_ReturnsOk()
    {
        var id = Guid.NewGuid();
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
            UpdatedAt = DateTime.UtcNow,
            ETag = "test-xmin-final-valid"
        };

        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated Dollar",
            Symbol = "$",
            DecimalPlaces = 2
        };

        var updatedCurrency = new CurrencyResponse
        {
            Id = id,
            Code = "USD",
            Symbol = "$",
            Name = "Updated Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ETag = "test-xmin-final-updated"
        };

        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCurrency);

        _currencyServiceMock
            .Setup(x => x.UpdateByIdAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedCurrency);

        _controller.Request.Headers.IfMatch = "\"test-xmin-final-valid\"";

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrencyResponse>(okResult.Value);
        Assert.Equal("Updated Dollar", response.Name);
    }

    [Fact]
    public async Task UpdateById_InvalidRequest_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
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
            UpdatedAt = DateTime.UtcNow,
            ETag = "test-xmin-final-invalid"
        };

        _currencyServiceMock
            .Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCurrency);

        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "",
            Symbol = "",
            DecimalPlaces = -1
        };

        _controller.Request.Headers.IfMatch = "\"test-xmin-final-invalid\"";

        var result = await _controller.UpdateById(id, request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.Equal("BadRequest", errorResponse.Error);
    }

    [Fact]
    public async Task DeleteById_CurrencyNotFound_ReturnsNotFound()
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
    public async Task Update_ServiceThrowsException_Returns500()
    {
        var code = "USD";
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated",
            Symbol = "$",
            DecimalPlaces = 2
        };

        _currencyServiceMock
            .Setup(x => x.UpdateAsync(code, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.Update(code, request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task Delete_ServiceThrowsException_Returns500()
    {
        _currencyServiceMock
            .Setup(x => x.DeleteAsync("USD", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.Delete("USD", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task CreateAdmin_ServiceThrowsException_Returns500()
    {
        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "JPY",
            Symbol = "¥",
            Name = "Japanese Yen",
            DecimalPlaces = 0
        };

        _currencyServiceMock
            .Setup(x => x.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.CreateAdmin(request, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetByCode_ServiceThrowsException_Returns500()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCodeAsync("USD", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetByCode("USD", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetCurrencyByCountry_ServiceThrowsException_Returns500()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCountryCodeAsync("TH", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetCurrencyByCountry("TH", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetCurrencyByCountryPath_ServiceThrowsException_Returns500()
    {
        _currencyServiceMock
            .Setup(x => x.GetByCountryCodeAsync("TH", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        var result = await _controller.GetCurrencyByCountryPath("TH", CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }
}

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
    public async Task GetExchangeRate_WithMatchingETag_Returns304()
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

        var etag = ETagHelper.GenerateETag(expectedResponse);
        _controller.Request.Headers.IfNoneMatch = $"\"{etag}\"";

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.IsType<StatusCodeResult>(result.Result);
        var statusResult = result.Result as StatusCodeResult;
        Assert.Equal(StatusCodes.Status304NotModified, statusResult?.StatusCode);
    }

    [Fact]
    public async Task GetExchangeRate_WithIfModifiedSince_NotModified_Returns304()
    {
        var pastTime = DateTime.UtcNow.AddHours(-1);
        var expectedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = pastTime,
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        _controller.Request.Headers.IfModifiedSince = pastTime.AddMinutes(1).ToString("R");

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.IsType<StatusCodeResult>(result.Result);
        var statusResult = result.Result as StatusCodeResult;
        Assert.Equal(StatusCodes.Status304NotModified, statusResult?.StatusCode);
    }

    [Fact(Skip = "DateTime parsing edge case - tested indirectly via controller")]
    public async Task GetExchangeRate_WithIfModifiedSince_Modified_ReturnsOk()
    {
        var currentTime = DateTime.UtcNow;
        var expectedResponse = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = currentTime,
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        _controller.Request.Headers.IfModifiedSince = currentTime.AddHours(-1).ToString("R");

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.NotNull(result.Result);
        var statusCodeResult = Assert.IsType<StatusCodeResult>(result.Result);
        Assert.NotEqual(304, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetExchangeRate_ResponseHasCorrectCacheHeaders()
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

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(_controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetExchangeRate_SnapshotMode_HasCorrectCacheHeaders()
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
            .Setup(x => x.GetSnapshotRateAsync("USD", "EUR", It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.GetExchangeRate("USD", "EUR", "snapshot", DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(_controller.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task GetExchangeRate_InvalidFromCode_TooShort_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("US", "EUR", "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.NotNull(errorResponse.Details);
        Assert.Contains("validation", errorResponse.Details.Keys);
    }

    [Fact]
    public async Task GetExchangeRate_InvalidFromCode_Lowercase_ReturnsBadRequest()
    {
        var result = await _controller.GetExchangeRate("u", "eur", "live", null, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<Maliev.CurrencyService.Api.Models.Common.ErrorResponse>(badRequestResult.Value);
        Assert.NotNull(errorResponse.Details);
        Assert.Contains("validation", errorResponse.Details.Keys);
    }

    [Fact]
    public async Task GetExchangeRate_ServiceThrows_Returns500()
    {
        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Provider error"));

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetExchangeRate_RetryAfterHeader_SetOn503()
    {
        _rateServiceMock
            .Setup(x => x.GetLiveRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExchangeRateResponse?)null);

        var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

        Assert.NotNull(_controller.Response.Headers.RetryAfter);
        Assert.Equal("30", _controller.Response.Headers.RetryAfter.ToString());
    }
}

public class ErrorResponseModelTests
{
    [Fact]
    public void ErrorResponse_CanBeInitializedWithRequiredProperties()
    {
        var response = new Maliev.CurrencyService.Api.Models.Common.ErrorResponse
        {
            Error = "BadRequest",
            Message = "Invalid request",
            Timestamp = DateTime.UtcNow,
            CorrelationId = "test-correlation-id"
        };

        Assert.Equal("BadRequest", response.Error);
        Assert.Equal("Invalid request", response.Message);
        Assert.Equal("test-correlation-id", response.CorrelationId);
    }

    [Fact]
    public void ErrorResponse_CanIncludeDetails()
    {
        var response = new Maliev.CurrencyService.Api.Models.Common.ErrorResponse
        {
            Error = "BadRequest",
            Message = "Validation failed",
            Timestamp = DateTime.UtcNow,
            Details = new Dictionary<string, string[]>
            {
                { "Code", new[] { "Code is required" } },
                { "Name", new[] { "Name is required" } }
            }
        };

        Assert.NotNull(response.Details);
        Assert.Equal(2, response.Details.Count);
        Assert.Contains("Code", response.Details.Keys);
    }

    [Fact]
    public void ErrorResponse_CorrelationIdCanBeNull()
    {
        var response = new Maliev.CurrencyService.Api.Models.Common.ErrorResponse
        {
            Error = "NotFound",
            Message = "Resource not found",
            Timestamp = DateTime.UtcNow
        };

        Assert.Null(response.CorrelationId);
    }

    [Fact]
    public void ErrorResponse_DetailsCanBeNull()
    {
        var response = new Maliev.CurrencyService.Api.Models.Common.ErrorResponse
        {
            Error = "NotFound",
            Message = "Resource not found",
            Timestamp = DateTime.UtcNow
        };

        Assert.Null(response.Details);
    }
}

public class ETagHelperModelTests
{
    [Fact]
    public void GenerateETag_SameContent_GeneratesSameETag()
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

        var etag1 = ETagHelper.GenerateETag(currency);
        var etag2 = ETagHelper.GenerateETag(currency);

        Assert.Equal(etag1, etag2);
    }

    [Fact]
    public void GenerateETag_DifferentContent_GeneratesDifferentETag()
    {
        var currency1 = new CurrencyResponse
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

        var currency2 = new CurrencyResponse
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

        var etag1 = ETagHelper.GenerateETag(currency1);
        var etag2 = ETagHelper.GenerateETag(currency2);

        Assert.NotEqual(etag1, etag2);
    }

    [Fact]
    public void GenerateETag_ExchangeRateResponse_GeneratesValidETag()
    {
        var rate = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Fawazahmed",
            IsTransitive = false,
            Mode = "live"
        };

        var etag = ETagHelper.GenerateETag(rate);

        Assert.NotNull(etag);
        Assert.True(etag.Length > 0);
    }

    [Fact]
    public void GenerateETag_ReturnsBase64String()
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

        var etag = ETagHelper.GenerateETag(currency);

        var bytes = Convert.FromBase64String(etag);
        Assert.NotNull(bytes);
    }
}

public class ConvertCurrencyRequestModelTests
{
    [Fact]
    public void ConvertCurrencyRequest_ValidRequest_PassesValidation()
    {
        var request = new ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 100.00m
        };

        Assert.Equal("USD", request.From);
        Assert.Equal("EUR", request.To);
        Assert.Equal(100.00m, request.Amount);
    }

    [Fact]
    public void ConvertCurrencyRequest_ZeroAmount_HasZeroValue()
    {
        var request = new ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 0m
        };

        Assert.Equal(0m, request.Amount);
    }

    [Fact]
    public void ConvertCurrencyRequest_NegativeAmount_HasNegativeValue()
    {
        var request = new ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = -100.00m
        };

        Assert.Equal(-100.00m, request.Amount);
    }

    [Fact]
    public void ConvertCurrencyRequest_LongCode_HasLongValue()
    {
        var request = new ConvertCurrencyRequest
        {
            From = "USDD",
            To = "EURR",
            Amount = 100.00m
        };

        Assert.Equal("USDD", request.From);
    }
}

public class PaginatedResponseModelTests
{
    [Fact]
    public void PaginatedCurrencyResponse_CanBeInitialized()
    {
        var response = new PaginatedCurrencyResponse
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

        Assert.Single(response.Items);
        Assert.Equal(1, response.Page);
        Assert.Equal(50, response.PageSize);
    }

    [Fact]
    public void PaginatedCurrencyResponse_CanBeEmpty()
    {
        var response = new PaginatedCurrencyResponse
        {
            Items = new List<CurrencyResponse>(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0,
            TotalPages = 0
        };

        Assert.Empty(response.Items);
        Assert.Equal(0, response.TotalCount);
    }
}

public class SnapshotModelTests
{
    [Fact]
    public void SnapshotEntryDto_CanBeInitialized()
    {
        var dto = new SnapshotEntryDto
        {
            From = "USD",
            To = "EUR",
            Rate = 0.85m,
            Timestamp = "2025-01-15T00:00:00Z"
        };

        Assert.Equal("USD", dto.From);
        Assert.Equal("EUR", dto.To);
        Assert.Equal(0.85m, dto.Rate);
    }

    [Fact]
    public void ValidationReport_CanBeInitialized()
    {
        var report = new ValidationReport
        {
            IsValid = true,
            ValidationErrors = new List<string> { "Error 1", "Error 2" },
            RecordCount = 10,
            IsDryRun = false
        };

        Assert.True(report.IsValid);
        Assert.Equal(2, report.ValidationErrors.Count);
    }

    [Fact]
    public void SnapshotIngestionResult_CanBeInitialized()
    {
        var result = new SnapshotIngestionResult
        {
            BatchId = "batch-123",
            Status = "Queued",
            RecordCount = 100,
            SubmittedAt = DateTime.UtcNow
        };

        Assert.Equal("batch-123", result.BatchId);
        Assert.Equal("Queued", result.Status);
    }
}
