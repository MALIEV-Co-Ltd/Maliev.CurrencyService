using Maliev.CurrencyService.Api.Controllers;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Models.Common;
using Maliev.CurrencyService.Api.Models.Rates;
using Maliev.CurrencyService.Api.Models.Snapshots;
using Maliev.CurrencyService.Application.Common;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using ErrorResponse = Maliev.CurrencyService.Api.Models.Common.ErrorResponse;
using SnapshotAuditLog = Maliev.CurrencyService.Application.DTOs.Snapshots.SnapshotAuditLog;
using ValidationReport = Maliev.CurrencyService.Api.Models.Snapshots.ValidationReport;

namespace Maliev.CurrencyService.Tests;

public class TestsExtra2
{
    #region Model Validation Tests

    public class ConvertCurrencyRequestValidationTests
    {
        [Fact]
        public void ConvertCurrencyRequest_ValidProperties_SetCorrectly()
        {
            var request = new ConvertCurrencyRequest
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
        public void ConvertCurrencyRequest_RequiredAttributes_AreDefined()
        {
            var type = typeof(ConvertCurrencyRequest);
            var fromProperty = type.GetProperty(nameof(ConvertCurrencyRequest.From));
            var toProperty = type.GetProperty(nameof(ConvertCurrencyRequest.To));
            var amountProperty = type.GetProperty(nameof(ConvertCurrencyRequest.Amount));

            Assert.NotNull(fromProperty);
            Assert.NotNull(toProperty);
            Assert.NotNull(amountProperty);

            var fromRequired = fromProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false);
            var toRequired = toProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false);
            var amountRequired = amountProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false);

            Assert.NotEmpty(fromRequired);
            Assert.NotEmpty(toRequired);
            Assert.NotEmpty(amountRequired);
        }

        [Fact]
        public void ConvertCurrencyRequest_StringLengthValidation_AreDefined()
        {
            var type = typeof(ConvertCurrencyRequest);
            var fromProperty = type.GetProperty(nameof(ConvertCurrencyRequest.From));
            var toProperty = type.GetProperty(nameof(ConvertCurrencyRequest.To));

            Assert.NotNull(fromProperty);
            Assert.NotNull(toProperty);

            var fromLength = fromProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.StringLengthAttribute), false);
            var toLength = toProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.StringLengthAttribute), false);

            Assert.NotEmpty(fromLength);
            Assert.NotEmpty(toLength);
        }

        [Fact]
        public void ConvertCurrencyRequest_RangeValidation_IsDefined()
        {
            var type = typeof(ConvertCurrencyRequest);
            var amountProperty = type.GetProperty(nameof(ConvertCurrencyRequest.Amount));
            Assert.NotNull(amountProperty);

            var rangeAttr = amountProperty.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false);
            Assert.NotEmpty(rangeAttr);
        }
    }

    public class SnapshotEntryDtoValidationTests
    {
        [Fact]
        public void SnapshotEntryDto_ValidProperties_SetCorrectly()
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
            Assert.Equal("2025-01-15T00:00:00Z", dto.Timestamp);
        }

        // NOTE: Data annotations were removed from SnapshotEntryDto to support dry-run mode.
        // The controller handles validation manually to return detailed error reports.
    }

    public class BulkUpdateRatesRequestValidationTests
    {
        [Fact]
        public void BulkUpdateRatesRequest_RatesProperty_CanBeAccessed()
        {
            var type = typeof(BulkUpdateRatesRequest);
            var ratesProperty = type.GetProperty(nameof(BulkUpdateRatesRequest.Rates));
            Assert.NotNull(ratesProperty);
        }

        [Fact]
        public void BulkUpdateRatesRequest_CanCreateInstance()
        {
            var request = new BulkUpdateRatesRequest
            {
                Rates = new List<UpdateRateRequest>
                {
                    new() { From = "USD", To = "EUR", Rate = 0.85m }
                }
            };

            Assert.Single(request.Rates);
        }
    }

    public class SetRateSourceRequestValidationTests
    {
        [Fact]
        public void SetRateSourceRequest_ProviderNameProperty_CanBeAccessed()
        {
            var type = typeof(SetRateSourceRequest);
            var providerProperty = type.GetProperty(nameof(SetRateSourceRequest.ProviderName));
            Assert.NotNull(providerProperty);
        }

        [Fact]
        public void SetRateSourceRequest_CanCreateInstance()
        {
            var request = new SetRateSourceRequest
            {
                ProviderName = "Frankfurter"
            };

            Assert.Equal("Frankfurter", request.ProviderName);
        }
    }

    public class ConvertCurrencyResponseTests
    {
        [Fact]
        public void ConvertCurrencyResponse_Properties_SetCorrectly()
        {
            var response = new ConvertCurrencyResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                OriginalAmount = 100.00m,
                ConvertedAmount = 85.00m,
                ExchangeRate = 0.85m,
                RateTimestamp = DateTime.UtcNow,
                Source = "Frankfurter"
            };

            Assert.Equal("USD", response.FromCurrency);
            Assert.Equal("EUR", response.ToCurrency);
            Assert.Equal(100.00m, response.OriginalAmount);
            Assert.Equal(85.00m, response.ConvertedAmount);
            Assert.Equal(0.85m, response.ExchangeRate);
            Assert.Equal("Frankfurter", response.Source);
        }
    }

    public class ValidationReportTests
    {
        [Fact]
        public void ValidationReport_Properties_SetCorrectly()
        {
            var report = new ValidationReport
            {
                IsValid = true,
                ValidationErrors = new List<string> { "Error 1", "Error 2" },
                RecordCount = 100,
                IsDryRun = true
            };

            Assert.True(report.IsValid);
            Assert.Equal(2, report.ValidationErrors.Count);
            Assert.Equal(100, report.RecordCount);
            Assert.True(report.IsDryRun);
        }

        [Fact]
        public void ValidationReport_DefaultValues_AreSet()
        {
            var report = new ValidationReport();
            Assert.False(report.IsValid);
            Assert.False(report.IsDryRun);
            Assert.NotNull(report.ValidationErrors);
            Assert.Empty(report.ValidationErrors);
            Assert.Equal(0, report.RecordCount);
        }
    }

    public class SnapshotIngestionResultTests
    {
        [Fact]
        public void SnapshotIngestionResult_Properties_SetCorrectly()
        {
            var result = new SnapshotIngestionResult
            {
                BatchId = "batch-123",
                Status = "Queued",
                RecordCount = 50,
                SubmittedAt = DateTime.UtcNow
            };

            Assert.Equal("batch-123", result.BatchId);
            Assert.Equal("Queued", result.Status);
            Assert.Equal(50, result.RecordCount);
        }
    }

    #endregion

    #region ETag Helper Tests

    public class ETagHelperTests
    {
        [Fact]
        public void GenerateETag_ReturnsConsistentHash_ForSameObject()
        {
            var obj = new { Code = "USD", Rate = 0.85m };
            var etag1 = ETagHelper.GenerateETag(obj);
            var etag2 = ETagHelper.GenerateETag(obj);

            Assert.Equal(etag1, etag2);
        }

        [Fact]
        public void GenerateETag_ReturnsDifferentHash_ForDifferentObjects()
        {
            var obj1 = new { Code = "USD", Rate = 0.85m };
            var obj2 = new { Code = "EUR", Rate = 0.75m };

            var etag1 = ETagHelper.GenerateETag(obj1);
            var etag2 = ETagHelper.GenerateETag(obj2);

            Assert.NotEqual(etag1, etag2);
        }

        [Fact]
        public void GenerateETag_Returns16CharacterBase64String()
        {
            var obj = new { Code = "USD" };
            var etag = ETagHelper.GenerateETag(obj);

            Assert.Equal(16, etag.Length);
        }

        [Fact]
        public void GenerateETag_WorksWithCurrencyResponse()
        {
            var response = new CurrencyResponse
            {
                Id = Guid.NewGuid(),
                Code = "USD",
                Symbol = "$",
                Name = "US Dollar",
                DecimalPlaces = 2,
                IsActive = true,
                IsPrimary = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var etag = ETagHelper.GenerateETag(response);

            Assert.NotNull(etag);
            Assert.Equal(16, etag.Length);
        }

        [Fact]
        public void GenerateETag_WorksWithExchangeRateResponse()
        {
            var response = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.85m,
                Timestamp = DateTime.UtcNow,
                Source = "Test",
                IsTransitive = false,
                Mode = "live"
            };

            var etag = ETagHelper.GenerateETag(response);

            Assert.NotNull(etag);
            Assert.Equal(16, etag.Length);
        }
    }

    #endregion

    #region RatesController Edge Cases

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
        public async Task GetExchangeRate_WithIfNoneMatchMatching_Returns304()
        {
            var expectedResponse = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.85m,
                Timestamp = DateTime.UtcNow.AddMinutes(-10),
                Source = "Test",
                IsTransitive = false,
                Mode = "live"
            };

            _rateServiceMock
                .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var etag = ETagHelper.GenerateETag(expectedResponse);
            _controller.Request.Headers.IfNoneMatch = $"\"{etag}\"";

            var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

            Assert.IsType<StatusCodeResult>(result.Result);
            var statusResult = (StatusCodeResult)result.Result;
            Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetExchangeRate_WithIfNoneMatchNotMatching_ReturnsOk()
        {
            var expectedResponse = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.85m,
                Timestamp = DateTime.UtcNow,
                Source = "Test",
                IsTransitive = false,
                Mode = "live"
            };

            _rateServiceMock
                .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            _controller.Request.Headers.IfNoneMatch = "\"different-etag\"";

            var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.IsType<ExchangeRateResponse>(okResult.Value);
        }

        [Fact]
        public async Task GetExchangeRate_WithIfModifiedSinceNotModified_Returns304()
        {
            var oldTimestamp = DateTime.UtcNow.AddHours(-1);
            var expectedResponse = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.85m,
                Timestamp = oldTimestamp,
                Source = "Test",
                IsTransitive = false,
                Mode = "live"
            };

            _rateServiceMock
                .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            _controller.Request.Headers.IfModifiedSince = oldTimestamp.AddMinutes(1).ToString("R");

            var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

            Assert.IsType<StatusCodeResult>(result.Result);
            var statusResult = (StatusCodeResult)result.Result;
            Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetExchangeRate_StaleRate_IncludesStalenessHeader()
        {
            var oldTimestamp = DateTime.UtcNow.AddMinutes(-10);
            var expectedResponse = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.85m,
                Timestamp = oldTimestamp,
                Source = "Test",
                IsTransitive = false,
                Mode = "live"
            };

            _rateServiceMock
                .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.True(_controller.Response.Headers.ContainsKey("X-Rate-Staleness"));
            var staleness = _controller.Response.Headers["X-Rate-Staleness"].ToString();
            Assert.Contains("stale", staleness);
        }

        [Fact]
        public async Task GetExchangeRate_FreshRate_IncludesFreshStalenessHeader()
        {
            var recentTimestamp = DateTime.UtcNow;
            var expectedResponse = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.85m,
                Timestamp = recentTimestamp,
                Source = "Test",
                IsTransitive = false,
                Mode = "live"
            };

            _rateServiceMock
                .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.True(_controller.Response.Headers.ContainsKey("X-Rate-Staleness"));
            var staleness = _controller.Response.Headers["X-Rate-Staleness"].ToString();
            Assert.Equal("fresh", staleness);
        }

        [Fact]
        public async Task GetExchangeRate_InvalidFromCurrencyTooShort_ReturnsBadRequest()
        {
            var result = await _controller.GetExchangeRate("US", "EUR", "live", null, CancellationToken.None);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ErrorResponse>(badRequestResult.Value);
            Assert.Equal("BadRequest", errorResponse.Error);
        }

        [Fact]
        public async Task GetExchangeRate_InvalidFromCurrencyWithNumbers_ReturnsBadRequest()
        {
            var result = await _controller.GetExchangeRate("US1", "EUR", "live", null, CancellationToken.None);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ErrorResponse>(badRequestResult.Value);
            Assert.Equal("BadRequest", errorResponse.Error);
        }

        [Fact]
        public async Task GetExchangeRate_SameCurrency_ReturnsOk()
        {
            var expectedResponse = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "USD",
                Rate = 1.0m,
                Timestamp = DateTime.UtcNow,
                Source = "Direct",
                IsTransitive = false,
                Mode = "live"
            };

            _rateServiceMock
                .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var result = await _controller.GetExchangeRate("USD", "USD", "live", null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetExchangeRate_EmptyFromCurrency_ReturnsBadRequest()
        {
            var result = await _controller.GetExchangeRate("", "EUR", "live", null, CancellationToken.None);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ErrorResponse>(badRequestResult.Value);
            Assert.Equal("BadRequest", errorResponse.Error);
        }

        [Fact]
        public async Task GetExchangeRate_LowercaseCurrency_ConvertedToUppercase()
        {
            var expectedResponse = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.85m,
                Timestamp = DateTime.UtcNow,
                Source = "Test",
                IsTransitive = false,
                Mode = "live"
            };

            _rateServiceMock
                .Setup(x => x.GetLiveRateAsync("USD", "EUR", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse)
                .Verifiable();

            await _controller.GetExchangeRate("usd", "eur", "live", null, CancellationToken.None);

            _rateServiceMock.Verify();
        }

        [Fact]
        public async Task GetExchangeRate_ServiceUnavailable_ReturnsRetryAfterHeader()
        {
            _rateServiceMock
                .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ExchangeRateResponse?)null);

            var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
            Assert.True(_controller.Response.Headers.ContainsKey("Retry-After"));
            Assert.Equal("30", _controller.Response.Headers["Retry-After"].ToString());
        }

        [Fact]
        public async Task GetExchangeRate_SnapshotMode_CachesFor24Hours()
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
            Assert.Contains("86400", _controller.Response.Headers["Cache-Control"].ToString());
        }

        [Fact]
        public async Task GetExchangeRate_LiveMode_CachesFor5Minutes()
        {
            var expectedResponse = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.85m,
                Timestamp = DateTime.UtcNow,
                Source = "Live",
                IsTransitive = false,
                Mode = "live"
            };

            _rateServiceMock
                .Setup(x => x.GetLiveRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var result = await _controller.GetExchangeRate("USD", "EUR", "live", null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
            Assert.Contains("300", _controller.Response.Headers["Cache-Control"].ToString());
        }
    }

    #endregion

    #region CurrenciesController Edge Cases

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
        public async Task ListCurrencies_EmptyResult_ReturnsOk()
        {
            var emptyResponse = new PaginatedCurrencyResponse
            {
                Items = new List<CurrencyResponse>(),
                Page = 1,
                PageSize = 50,
                TotalCount = 0,
                TotalPages = 0
            };

            _currencyServiceMock
                .Setup(x => x.GetAllAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(emptyResponse);

            var result = await _controller.ListCurrencies(1, 50, null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<PaginatedCurrencyResponse>(okResult.Value);
            Assert.Empty(response.Items);
        }

        [Fact]
        public async Task ListCurrencies_IncludesETagHeader()
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
                        IsPrimary = false,
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
                .ReturnsAsync(response);

            var result = await _controller.ListCurrencies(1, 50, null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.True(_controller.Response.Headers.ContainsKey("ETag"));
        }

        [Fact]
        public async Task ListCurrencies_IncludesCacheControlHeader()
        {
            var response = new PaginatedCurrencyResponse
            {
                Items = new List<CurrencyResponse>(),
                Page = 1,
                PageSize = 50,
                TotalCount = 0,
                TotalPages = 0
            };

            _currencyServiceMock
                .Setup(x => x.GetAllAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var result = await _controller.ListCurrencies(1, 50, null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.True(_controller.Response.Headers.ContainsKey("Cache-Control"));
            Assert.Contains("max-age=300", _controller.Response.Headers["Cache-Control"].ToString());
        }

        [Fact]
        public async Task ListCurrencies_IncludesCorrelationIdHeader()
        {
            var response = new PaginatedCurrencyResponse
            {
                Items = new List<CurrencyResponse>(),
                Page = 1,
                PageSize = 50,
                TotalCount = 0,
                TotalPages = 0
            };

            _currencyServiceMock
                .Setup(x => x.GetAllAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var result = await _controller.ListCurrencies(1, 50, null, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            Assert.True(_controller.Response.Headers.ContainsKey("X-Correlation-ID"));
        }

        [Fact]
        public async Task GetByCode_NotFound_Returns404()
        {
            _currencyServiceMock
                .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CurrencyResponse?)null);

            var result = await _controller.GetByCode("XXX", CancellationToken.None);

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ErrorResponse>(notFoundResult.Value);
            Assert.Equal("NotFound", errorResponse.Error);
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
                IsPrimary = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ETag = "test-xmin-789"
            };

            _currencyServiceMock
                .Setup(x => x.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(currency);

            _controller.Request.Headers.IfNoneMatch = "\"test-xmin-789\"";

            var result = await _controller.GetByCode("USD", CancellationToken.None);

            Assert.IsType<StatusCodeResult>(result.Result);
            var statusResult = (StatusCodeResult)result.Result;
            Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
        }

        [Fact]
        public async Task GetCurrencyByCountry_EmptyCountryCode_ReturnsBadRequest()
        {
            var result = await _controller.GetCurrencyByCountry("", CancellationToken.None);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ErrorResponse>(badRequestResult.Value);
            Assert.Equal("BadRequest", errorResponse.Error);
        }

        [Fact]
        public async Task GetCurrencyByCountry_InvalidFormat_ReturnsBadRequest()
        {
            var result = await _controller.GetCurrencyByCountry("INVALID", CancellationToken.None);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ErrorResponse>(badRequestResult.Value);
            Assert.Equal("BadRequest", errorResponse.Error);
            Assert.Contains("ISO2", errorResponse.Message);
        }

        [Fact]
        public async Task GetCurrencyByCountryPath_EmptyCountryCode_ReturnsNotFound()
        {
            var result = await _controller.GetCurrencyByCountryPath("", CancellationToken.None);

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ErrorResponse>(notFoundResult.Value);
            Assert.Equal("NotFound", errorResponse.Error);
        }

        [Fact]
        public async Task Update_CurrencyNotFound_Returns404()
        {
            var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
            {
                Symbol = "$",
                Name = "US Dollar",
                DecimalPlaces = 2
            };

            _currencyServiceMock
                .Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Application.DTOs.Currencies.UpdateCurrencyRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CurrencyResponse?)null);

            var result = await _controller.Update("XXX", request, CancellationToken.None);

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ErrorResponse>(notFoundResult.Value);
            Assert.Equal("NotFound", errorResponse.Error);
        }

        [Fact]
        public async Task Update_InvalidRequest_ReturnsBadRequest()
        {
            var request = new Application.DTOs.Currencies.UpdateCurrencyRequest();

            var result = await _controller.Update("USD", request, CancellationToken.None);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ErrorResponse>(badRequestResult.Value);
            Assert.Equal("BadRequest", errorResponse.Error);
            Assert.NotNull(errorResponse.Details);
        }

        [Fact]
        public async Task Delete_CurrencyNotFound_Returns404()
        {
            _currencyServiceMock
                .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var result = await _controller.Delete("XXX", CancellationToken.None);

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(notFoundResult.Value);
            Assert.Equal("NotFound", errorResponse.Error);
        }

        [Fact]
        public async Task Delete_Success_Returns204()
        {
            _currencyServiceMock
                .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _controller.Delete("USD", CancellationToken.None);

            Assert.IsType<NoContentResult>(result);
        }
    }

    #endregion

    #region SnapshotsController Edge Cases

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
        public async Task ImportBatch_EmptySnapshots_ReturnsBadRequest()
        {
            var result = await _controller.ImportBatch(new List<SnapshotEntryDto>(), false, CancellationToken.None);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(badRequestResult.Value);
            Assert.Equal("BadRequest", errorResponse.Error);
            Assert.Contains("empty", errorResponse.Message);
        }

        [Fact]
        public async Task ImportBatch_NullSnapshots_ReturnsBadRequest()
        {
            var result = await _controller.ImportBatch(null!, false, CancellationToken.None);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(badRequestResult.Value);
            Assert.Equal("BadRequest", errorResponse.Error);
        }

        [Fact]
        public async Task ImportBatch_DryRun_ReturnsOkWithValidationReport()
        {
            var snapshots = new List<SnapshotEntryDto>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m, Timestamp = "2025-01-15T00:00:00Z" }
            };

            var batchResponse = new SnapshotBatchResponse
            {
                BatchId = "batch-123",
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
        public async Task ImportBatch_ValidationFailed_ReturnsBadRequest()
        {
            var snapshots = new List<SnapshotEntryDto>
            {
                new() { From = "INVALID", To = "EUR", Rate = 0.85m, Timestamp = "2025-01-15T00:00:00Z" }
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
                    { "INVALID:EUR", new[] { "Invalid currency code" } }
                }
            };

            _snapshotServiceMock
                .Setup(x => x.ImportBatchAsync(It.IsAny<SnapshotBatchRequest>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(batchResponse);

            var result = await _controller.ImportBatch(snapshots, false, CancellationToken.None);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(badRequestResult.Value);
            Assert.Equal("BadRequest", errorResponse.Error);
        }

        [Fact]
        public async Task PromoteBatch_NotFound_Returns404()
        {
            _snapshotServiceMock
                .Setup(x => x.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var result = await _controller.PromoteBatch("invalid-batch", CancellationToken.None);

            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(notFoundResult.Value);
            Assert.Equal("NotFound", errorResponse.Error);
        }

        [Fact]
        public async Task PromoteBatch_Success_ReturnsOk()
        {
            _snapshotServiceMock
                .Setup(x => x.PromoteBatchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _controller.PromoteBatch("batch-123", CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task CleanupOldSnapshots_ReturnsDeletedCount()
        {
            _snapshotServiceMock
                .Setup(x => x.CleanupOldSnapshotsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(100);

            var result = await _controller.CleanupOldSnapshots(CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public void GetBatchStatus_ReturnsStatus()
        {
            _snapshotQueueMock
                .Setup(x => x.GetStatus(It.IsAny<string>()))
                .Returns(("Completed", (string?)null));

            var result = _controller.GetBatchStatus("batch-123");

            var okResult = Assert.IsType<OkObjectResult>(result);
        }
    }

    #endregion

    #region ErrorResponse Model Tests

    public class ApiErrorResponseModelTests
    {
        [Fact]
        public void ErrorResponse_RequiredProperties_CanBeInitialized()
        {
            var errorResponse = new Maliev.CurrencyService.Api.Models.Common.ErrorResponse
            {
                Error = "BadRequest",
                Message = "Invalid request",
                Timestamp = DateTime.UtcNow
            };

            Assert.Equal("BadRequest", errorResponse.Error);
            Assert.Equal("Invalid request", errorResponse.Message);
            Assert.NotEqual(default, errorResponse.Timestamp);
        }

        [Fact]
        public void ErrorResponse_OptionalProperties_CanBeNull()
        {
            var errorResponse = new Maliev.CurrencyService.Api.Models.Common.ErrorResponse
            {
                Error = "BadRequest",
                Message = "Invalid request",
                Timestamp = DateTime.UtcNow
            };

            Assert.Null(errorResponse.CorrelationId);
            Assert.Null(errorResponse.Details);
        }

        [Fact]
        public void ErrorResponse_WithDetails_CanBeSet()
        {
            var errorResponse = new Maliev.CurrencyService.Api.Models.Common.ErrorResponse
            {
                Error = "BadRequest",
                Message = "Invalid request",
                Timestamp = DateTime.UtcNow,
                Details = new Dictionary<string, string[]>
                {
                    { "validation", new[] { "Error 1", "Error 2" } }
                }
            };

            Assert.NotNull(errorResponse.Details);
            Assert.Equal(2, errorResponse.Details["validation"].Length);
        }

        [Fact]
        public void ErrorResponse_WithCorrelationId_CanBeSet()
        {
            var errorResponse = new Maliev.CurrencyService.Api.Models.Common.ErrorResponse
            {
                Error = "BadRequest",
                Message = "Invalid request",
                Timestamp = DateTime.UtcNow,
                CorrelationId = "test-correlation-id"
            };

            Assert.Equal("test-correlation-id", errorResponse.CorrelationId);
        }
    }

    #endregion
}
