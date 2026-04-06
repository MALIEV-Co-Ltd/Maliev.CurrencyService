using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Common;
using System.Linq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// Unit tests for API Models, Application DTOs, and remaining Infrastructure services.
/// </summary>
public class FinalTests
{
    #region API Models Tests

    #region UpdateCurrencyRequestTests

    [Fact]
    public void ApiModels_UpdateCurrencyRequest_ValidData_PassesValidation()
    {
        var request = new Api.Models.UpdateCurrencyRequest
        {
            ShortName = "USD",
            LongName = "United States Dollar"
        };

        Assert.Equal("USD", request.ShortName);
        Assert.Equal("United States Dollar", request.LongName);
    }

    [Fact]
    public void ApiModels_UpdateCurrencyRequest_SetProperties_SetsCorrectValues()
    {
        var request = new Api.Models.UpdateCurrencyRequest
        {
            ShortName = "EUR",
            LongName = "Euro"
        };

        Assert.Equal("EUR", request.ShortName);
        Assert.Equal("Euro", request.LongName);
    }

    #endregion

    #region RedisOptionsTests

    [Fact]
    public void ApiModels_RedisOptions_SetProperties_SetsCorrectValues()
    {
        var options = new Api.Models.RedisOptions
        {
            ConnectionString = "localhost:6379",
            InstanceName = "my-instance",
            Database = 1,
            ConnectTimeout = 10,
            SyncTimeout = 10000,
            AbortOnConnectFail = true
        };

        Assert.Equal("localhost:6379", options.ConnectionString);
        Assert.Equal("my-instance", options.InstanceName);
        Assert.Equal(1, options.Database);
        Assert.Equal(10, options.ConnectTimeout);
        Assert.Equal(10000, options.SyncTimeout);
        Assert.True(options.AbortOnConnectFail);
    }

    [Fact]
    public void ApiModels_RedisOptions_DefaultValues_AreCorrect()
    {
        var options = new Api.Models.RedisOptions
        {
            ConnectionString = "localhost:6379"
        };

        Assert.Equal(0, options.Database);
        Assert.Equal(5, options.ConnectTimeout);
        Assert.Equal(5000, options.SyncTimeout);
        Assert.False(options.AbortOnConnectFail);
        Assert.Equal("Redis", Api.Models.RedisOptions.SectionName);
    }

    #endregion

    #region ProviderMetricsTests

    [Fact]
    public void ApiModels_ProviderMetrics_DefaultValues_AreCorrect()
    {
        var metrics = new Api.Models.ProviderMetrics();

        Assert.Equal(string.Empty, metrics.ProviderName);
        Assert.Equal(0, metrics.TotalRequests);
        Assert.Equal(0, metrics.SuccessfulRequests);
        Assert.Equal(0, metrics.TotalResponseTimeMs);
    }

    [Fact]
    public void ApiModels_ProviderMetrics_SuccessRate_ReturnsZero_WhenNoRequests()
    {
        var metrics = new Api.Models.ProviderMetrics();

        Assert.Equal(0, metrics.SuccessRate);
    }

    [Fact]
    public void ApiModels_ProviderMetrics_SuccessRate_ReturnsCorrectValue()
    {
        var metrics = new Api.Models.ProviderMetrics
        {
            TotalRequests = 100,
            SuccessfulRequests = 80
        };

        Assert.Equal(0.8, metrics.SuccessRate);
    }

    [Fact]
    public void ApiModels_ProviderMetrics_AverageResponseTimeMs_ReturnsZero_WhenNoRequests()
    {
        var metrics = new Api.Models.ProviderMetrics();

        Assert.Equal(0, metrics.AverageResponseTimeMs);
    }

    [Fact]
    public void ApiModels_ProviderMetrics_AverageResponseTimeMs_ReturnsCorrectValue()
    {
        var metrics = new Api.Models.ProviderMetrics
        {
            TotalRequests = 10,
            TotalResponseTimeMs = 500
        };

        Assert.Equal(50, metrics.AverageResponseTimeMs);
    }

    [Fact]
    public void ApiModels_ProviderMetrics_ErrorRequests_ReturnsCorrectValue()
    {
        var metrics = new Api.Models.ProviderMetrics
        {
            TotalRequests = 100,
            SuccessfulRequests = 70
        };

        Assert.Equal(30, metrics.ErrorRequests);
    }

    [Fact]
    public void ApiModels_ProviderMetrics_ErrorRate_ReturnsCorrectValue()
    {
        var metrics = new Api.Models.ProviderMetrics
        {
            TotalRequests = 100,
            SuccessfulRequests = 90
        };

        Assert.Equal(0.1, metrics.ErrorRate);
    }

    [Fact]
    public void ApiModels_ProviderMetrics_LastRequestAt_SetAndGet()
    {
        var now = DateTime.UtcNow;
        var metrics = new Api.Models.ProviderMetrics
        {
            LastRequestAt = now
        };

        Assert.Equal(now, metrics.LastRequestAt);
    }

    #endregion

    #region PagedResultTests

    [Fact]
    public void ApiModels_PagedResult_TotalPages_CalculatesCorrectly()
    {
        var result = new Api.Models.PagedResult<string>
        {
            TotalCount = 100,
            PageSize = 10,
            Page = 1
        };

        Assert.Equal(10, result.TotalPages);
    }

    [Fact]
    public void ApiModels_PagedResult_TotalPages_ReturnsZero_WhenTotalCountIsZero()
    {
        var result = new Api.Models.PagedResult<string>
        {
            TotalCount = 0,
            PageSize = 10,
            Page = 1
        };

        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void ApiModels_PagedResult_HasNextPage_ReturnsTrue_WhenNotLastPage()
    {
        var result = new Api.Models.PagedResult<string>
        {
            TotalCount = 100,
            PageSize = 10,
            Page = 5
        };

        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void ApiModels_PagedResult_HasNextPage_ReturnsFalse_WhenLastPage()
    {
        var result = new Api.Models.PagedResult<string>
        {
            TotalCount = 100,
            PageSize = 10,
            Page = 10
        };

        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void ApiModels_PagedResult_HasPreviousPage_ReturnsTrue_WhenNotFirstPage()
    {
        var result = new Api.Models.PagedResult<string>
        {
            TotalCount = 100,
            PageSize = 10,
            Page = 5
        };

        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public void ApiModels_PagedResult_HasPreviousPage_ReturnsFalse_WhenFirstPage()
    {
        var result = new Api.Models.PagedResult<string>
        {
            TotalCount = 100,
            PageSize = 10,
            Page = 1
        };

        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void ApiModels_PagedResult_Items_CanBeSetAndRetrieved()
    {
        var items = new List<string> { "a", "b", "c" };
        var result = new Api.Models.PagedResult<string>
        {
            Items = items,
            TotalCount = 3,
            PageSize = 10,
            Page = 1
        };

        Assert.Equal(3, result.Items.Count());
    }

    #endregion

    #region GetExchangeRateRequestTests

    [Fact]
    public void ApiModels_GetExchangeRateRequest_SetProperties_SetsCorrectValues()
    {
        var request = new Api.Models.GetExchangeRateRequest
        {
            From = "USD",
            To = "EUR"
        };

        Assert.Equal("USD", request.From);
        Assert.Equal("EUR", request.To);
    }

    #endregion

    #region ExchangeRateOptionsTests

    [Fact]
    public void ApiModels_ExchangeRateOptions_DefaultValues_AreCorrect()
    {
        var options = new Api.Models.ExchangeRateOptions();

        Assert.Equal(30, options.CacheDurationMinutes);
        Assert.Equal(3, options.RetryAttempts);
        Assert.Equal(30, options.TimeoutSeconds);
        Assert.Equal("https://api.frankfurter.app/", options.FrankfurterApiUrl);
        Assert.Equal("ExchangeRate", Api.Models.ExchangeRateOptions.SectionName);
    }

    [Fact]
    public void ApiModels_ExchangeRateOptions_ProviderOrder_ContainsExpectedProviders()
    {
        var options = new Api.Models.ExchangeRateOptions();

        Assert.Contains("Frankfurter", options.ProviderOrder);
        Assert.Contains("Fawazahmed", options.ProviderOrder);
        Assert.Contains("ExchangeRateHost", options.ProviderOrder);
        Assert.Contains("ExchangeRateApi", options.ProviderOrder);
    }

    [Fact]
    public void ApiModels_ExchangeRateOptions_SetProperties_SetsCorrectValues()
    {
        var options = new Api.Models.ExchangeRateOptions
        {
            CacheDurationMinutes = 60,
            RetryAttempts = 5,
            TimeoutSeconds = 60,
            EnableDynamicPrioritization = true,
            MinRequestsForPrioritization = 20,
            ResponseTimeWeight = 0.5,
            SuccessRateWeight = 0.3,
            ErrorRateWeight = 0.1,
            RequestCountWeight = 0.1
        };

        Assert.Equal(60, options.CacheDurationMinutes);
        Assert.Equal(5, options.RetryAttempts);
        Assert.Equal(60, options.TimeoutSeconds);
        Assert.True(options.EnableDynamicPrioritization);
        Assert.Equal(20, options.MinRequestsForPrioritization);
        Assert.Equal(0.5, options.ResponseTimeWeight);
        Assert.Equal(0.3, options.SuccessRateWeight);
        Assert.Equal(0.1, options.ErrorRateWeight);
        Assert.Equal(0.1, options.RequestCountWeight);
    }

    #endregion

    #region ExchangeRateDtoTests

    [Fact]
    public void ApiModels_ExchangeRateDto_SetProperties_SetsCorrectValues()
    {
        var dto = new Api.Models.ExchangeRateDto
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            FetchedAt = DateTime.UtcNow,
            Source = "Frankfurter"
        };

        Assert.Equal("USD", dto.FromCurrency);
        Assert.Equal("EUR", dto.ToCurrency);
        Assert.Equal(0.85m, dto.Rate);
        Assert.Equal("Frankfurter", dto.Source);
    }

    #endregion

    #region CurrencyDtoTests

    [Fact]
    public void ApiModels_CurrencyDto_SetProperties_SetsCorrectValues()
    {
        var dto = new Api.Models.CurrencyDto
        {
            Id = 1,
            ShortName = "USD",
            LongName = "United States Dollar",
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow
        };

        Assert.Equal(1, dto.Id);
        Assert.Equal("USD", dto.ShortName);
        Assert.Equal("United States Dollar", dto.LongName);
    }

    #endregion

    #region CreateCurrencyRequestTests

    [Fact]
    public void ApiModels_CreateCurrencyRequest_SetProperties_SetsCorrectValues()
    {
        var request = new Api.Models.CreateCurrencyRequest
        {
            ShortName = "USD",
            LongName = "United States Dollar"
        };

        Assert.Equal("USD", request.ShortName);
        Assert.Equal("United States Dollar", request.LongName);
    }

    #endregion

    #region ConvertCurrencyResponseTests

    [Fact]
    public void ApiModels_ConvertCurrencyResponse_SetProperties_SetsCorrectValues()
    {
        var response = new Api.Models.ConvertCurrencyResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            OriginalAmount = 100m,
            ConvertedAmount = 85m,
            ExchangeRate = 0.85m,
            RateTimestamp = DateTime.UtcNow,
            Source = "Frankfurter"
        };

        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("EUR", response.ToCurrency);
        Assert.Equal(100m, response.OriginalAmount);
        Assert.Equal(85m, response.ConvertedAmount);
        Assert.Equal(0.85m, response.ExchangeRate);
        Assert.Equal("Frankfurter", response.Source);
    }

    #endregion

    #region ConvertCurrencyRequestTests

    [Fact]
    public void ApiModels_ConvertCurrencyRequest_SetProperties_SetsCorrectValues()
    {
        var request = new Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 100m
        };

        Assert.Equal("USD", request.From);
        Assert.Equal("EUR", request.To);
        Assert.Equal(100m, request.Amount);
    }

    #endregion

    #endregion

    #region Application DTOs Tests

    #region CurrencyResponseTests

    [Fact]
    public void Application_CurrencyResponse_SetProperties_SetsCorrectValues()
    {
        var now = DateTime.UtcNow;
        var response = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            Name = "United States Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        Assert.Equal("USD", response.Code);
        Assert.Equal("$", response.Symbol);
        Assert.Equal("United States Dollar", response.Name);
        Assert.Equal(2, response.DecimalPlaces);
        Assert.True(response.IsActive);
        Assert.True(response.IsPrimary);
    }

    #endregion

    #region Application CreateCurrencyRequestTests

    [Fact]
    public void Application_CreateCurrencyRequest_DefaultValues_AreCorrect()
    {
        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "USD",
            Name = "United States Dollar",
            Symbol = "$"
        };

        Assert.Equal(2, request.DecimalPlaces);
        Assert.True(request.IsActive);
    }

    [Fact]
    public void Application_CreateCurrencyRequest_SetProperties_SetsCorrectValues()
    {
        var request = new Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "THB",
            Name = "Thai Baht",
            Symbol = "฿",
            DecimalPlaces = 2,
            IsActive = true
        };

        Assert.Equal("THB", request.Code);
        Assert.Equal("Thai Baht", request.Name);
        Assert.Equal("฿", request.Symbol);
        Assert.Equal(2, request.DecimalPlaces);
        Assert.True(request.IsActive);
    }

    #endregion

    #region Application UpdateCurrencyRequestTests

    [Fact]
    public void Application_UpdateCurrencyRequest_DefaultValues_AreNull()
    {
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest();

        Assert.Null(request.Name);
        Assert.Null(request.Symbol);
        Assert.Null(request.DecimalPlaces);
        Assert.Null(request.IsActive);
    }

    [Fact]
    public void Application_UpdateCurrencyRequest_SetProperties_SetsCorrectValues()
    {
        var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Updated Name",
            Symbol = "U",
            DecimalPlaces = 3,
            IsActive = false
        };

        Assert.Equal("Updated Name", request.Name);
        Assert.Equal("U", request.Symbol);
        Assert.Equal(3, request.DecimalPlaces);
        Assert.False(request.IsActive);
    }

    #endregion

    #region SnapshotBatchRequestTests

    [Fact]
    public void Application_SnapshotBatchRequest_SetProperties_SetsCorrectValues()
    {
        var request = new SnapshotBatchRequest
        {
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "Frankfurter",
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m }
            }
        };

        Assert.Equal("Frankfurter", request.Source);
        Assert.Single(request.Snapshots);
    }

    #endregion

    #region SnapshotBatchResponseTests

    [Fact]
    public void Application_SnapshotBatchResponse_SetProperties_SetsCorrectValues()
    {
        var response = new SnapshotBatchResponse
        {
            BatchId = "test-batch-id",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "Frankfurter",
            SuccessCount = 10,
            FailureCount = 2,
            Status = "completed",
            ProcessedAt = DateTime.UtcNow
        };

        Assert.Equal("test-batch-id", response.BatchId);
        Assert.Equal(10, response.SuccessCount);
        Assert.Equal(2, response.FailureCount);
        Assert.Equal("completed", response.Status);
    }

    [Fact]
    public void Application_SnapshotBatchResponse_Errors_CanBeSet()
    {
        var errors = new Dictionary<string, string[]>
        {
            { "USD:EUR", new[] { "Invalid rate" } }
        };

        var response = new SnapshotBatchResponse
        {
            BatchId = "test-batch-id",
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "Frankfurter",
            SuccessCount = 0,
            FailureCount = 1,
            Status = "failed",
            Errors = errors,
            ProcessedAt = DateTime.UtcNow
        };

        Assert.NotNull(response.Errors);
        Assert.True(response.Errors.ContainsKey("USD:EUR"));
    }

    #endregion

    #region ExchangeRateResponseTests

    [Fact]
    public void Application_ExchangeRateResponse_SetProperties_SetsCorrectValues()
    {
        var response = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Frankfurter",
            IsTransitive = false,
            Mode = "live"
        };

        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("EUR", response.ToCurrency);
        Assert.Equal(0.85m, response.Rate);
        Assert.Equal("Frankfurter", response.Source);
        Assert.Equal("live", response.Mode);
    }

    [Fact]
    public void Application_ExchangeRateResponse_TransitiveCalculationDetails_CanBeSet()
    {
        var response = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "JPY",
            Rate = 110m,
            Timestamp = DateTime.UtcNow,
            Source = "Frankfurter",
            IsTransitive = true,
            IntermediateCurrency = "EUR",
            CalculationDetails = "USD/EUR × EUR/JPY",
            Mode = "live"
        };

        Assert.NotNull(response.CalculationDetails);
        Assert.Equal("USD/EUR × EUR/JPY", response.CalculationDetails);
    }

    [Fact]
    public void Application_ExchangeRateResponse_SnapshotMode_IncludesSnapshotDate()
    {
        var snapshotDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Snapshot",
            IsTransitive = false,
            Mode = "snapshot",
            SnapshotDate = snapshotDate
        };

        Assert.Equal("snapshot", response.Mode);
        Assert.Equal(snapshotDate, response.SnapshotDate);
    }

    #endregion

    #region UpdateRateRequestTests

    [Fact]
    public void Application_UpdateRateRequest_SetProperties_SetsCorrectValues()
    {
        var request = new UpdateRateRequest
        {
            From = "USD",
            To = "EUR",
            Rate = 0.85m
        };

        Assert.Equal("USD", request.From);
        Assert.Equal("EUR", request.To);
        Assert.Equal(0.85m, request.Rate);
    }

    #endregion

    #region PaginatedResponseTests

    [Fact]
    public void Application_PaginatedResponse_SetProperties_SetsCorrectValues()
    {
        var response = new PaginatedResponse<string>
        {
            Items = new List<string> { "a", "b", "c" },
            Page = 1,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };

        Assert.Equal(3, response.Items.Count());
        Assert.Equal(1, response.Page);
        Assert.Equal(10, response.PageSize);
        Assert.Equal(100, response.TotalCount);
        Assert.Equal(10, response.TotalPages);
    }

    [Fact]
    public void Application_PaginatedResponse_HasNextPage_ReturnsTrue()
    {
        var response = new PaginatedResponse<string>
        {
            Items = new List<string>(),
            Page = 5,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };

        Assert.True(response.HasNextPage);
    }

    [Fact]
    public void Application_PaginatedResponse_HasPreviousPage_ReturnsTrue()
    {
        var response = new PaginatedResponse<string>
        {
            Items = new List<string>(),
            Page = 5,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };

        Assert.True(response.HasPreviousPage);
    }

    #endregion

    #region SnapshotAuditLogTests

    [Fact]
    public void Application_SnapshotAuditLog_SetProperties_SetsCorrectValues()
    {
        var now = DateTime.UtcNow;
        var auditLog = new SnapshotAuditLog
        {
            BatchId = "batch-123",
            Timestamp = now,
            RecordCount = 50,
            Source = "Frankfurter",
            SubmittedBy = "Admin"
        };

        Assert.Equal("batch-123", auditLog.BatchId);
        Assert.Equal(now, auditLog.Timestamp);
        Assert.Equal(50, auditLog.RecordCount);
        Assert.Equal("Frankfurter", auditLog.Source);
        Assert.Equal("Admin", auditLog.SubmittedBy);
    }

    #endregion

    #endregion

    #region Edge Cases and Additional Tests

    [Fact]
    public void ApiModels_PagedResult_TotalPages_HandlesRounding()
    {
        var result = new Api.Models.PagedResult<string>
        {
            TotalCount = 95,
            PageSize = 10,
            Page = 1
        };

        Assert.Equal(10, result.TotalPages);
    }

    [Fact]
    public void ApiModels_PagedResult_TotalPages_HandlesSingleItem()
    {
        var result = new Api.Models.PagedResult<string>
        {
            TotalCount = 1,
            PageSize = 10,
            Page = 1
        };

        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public void ApiModels_ProviderMetrics_AllProperties_AreAccessible()
    {
        var now = DateTime.UtcNow;
        var metrics = new Api.Models.ProviderMetrics
        {
            ProviderName = "Frankfurter",
            TotalRequests = 1000,
            SuccessfulRequests = 950,
            TotalResponseTimeMs = 50000,
            LastRequestAt = now
        };

        Assert.Equal("Frankfurter", metrics.ProviderName);
        Assert.Equal(1000, metrics.TotalRequests);
        Assert.Equal(950, metrics.SuccessfulRequests);
        Assert.Equal(50000, metrics.TotalResponseTimeMs);
        Assert.Equal(now, metrics.LastRequestAt);
        Assert.Equal(0.95, metrics.SuccessRate);
        Assert.Equal(50, metrics.AverageResponseTimeMs);
        Assert.Equal(50, metrics.ErrorRequests);
        Assert.Equal(0.05, metrics.ErrorRate);
    }

    [Fact]
    public void ApiModels_ExchangeRateOptions_WeightsSumToOne()
    {
        var options = new Api.Models.ExchangeRateOptions
        {
            ResponseTimeWeight = 0.4,
            SuccessRateWeight = 0.3,
            ErrorRateWeight = 0.2,
            RequestCountWeight = 0.1
        };

        var sum = options.ResponseTimeWeight + options.SuccessRateWeight +
                  options.ErrorRateWeight + options.RequestCountWeight;
        Assert.Equal(1.0, sum, 10);
    }

    [Fact]
    public void ApiModels_ConvertCurrencyRequest_ValidAmounts_CanBeSet()
    {
        var request = new Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 1000.50m
        };

        Assert.Equal(1000.50m, request.Amount);
    }

    [Fact]
    public void ApiModels_UpdateCurrencyRequest_RequiredProperties_CanBeSet()
    {
        var request = new Api.Models.UpdateCurrencyRequest
        {
            ShortName = "GBP",
            LongName = "British Pound"
        };

        Assert.Equal("GBP", request.ShortName);
        Assert.Equal("British Pound", request.LongName);
    }

    [Fact]
    public void Application_SnapshotEntry_SetProperties_SetsCorrectValues()
    {
        var entry = new SnapshotEntry
        {
            From = "USD",
            To = "EUR",
            Rate = 0.85m
        };

        Assert.Equal("USD", entry.From);
        Assert.Equal("EUR", entry.To);
        Assert.Equal(0.85m, entry.Rate);
    }

    [Fact]
    public void Application_SnapshotBatchRequest_AutoPromote_DefaultIsFalse()
    {
        var request = new SnapshotBatchRequest
        {
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "Frankfurter",
            Snapshots = new List<SnapshotEntry>()
        };

        Assert.False(request.AutoPromote);
    }

    [Fact]
    public void Application_SnapshotBatchRequest_AutoPromote_CanBeSet()
    {
        var request = new SnapshotBatchRequest
        {
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Source = "Frankfurter",
            Snapshots = new List<SnapshotEntry>(),
            AutoPromote = true
        };

        Assert.True(request.AutoPromote);
    }

    #endregion
}
