using System.ComponentModel.DataAnnotations;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.DTOs.Common;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class AppTests
{
    #region Application DTOs - CreateCurrencyRequest

    [Fact]
    public void CreateCurrencyRequest_ValidRequest_PassesValidation()
    {
        var request = new Maliev.CurrencyService.Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "USD",
            Name = "United States Dollar",
            Symbol = "$",
            DecimalPlaces = 2,
            IsActive = true
        };

        var validationResults = ValidateModel(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void CreateCurrencyRequest_CodeTooShort_FailsValidation()
    {
        var request = new Maliev.CurrencyService.Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "US",
            Name = "United States Dollar",
            Symbol = "$"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void CreateCurrencyRequest_CodeLowercase_FailsValidation()
    {
        var request = new Maliev.CurrencyService.Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "usd",
            Name = "United States Dollar",
            Symbol = "$"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, v => v.ErrorMessage!.Contains("uppercase"));
    }

    [Fact]
    public void CreateCurrencyRequest_DefaultDecimalPlaces_IsTwo()
    {
        var request = new Maliev.CurrencyService.Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "USD",
            Name = "United States Dollar",
            Symbol = "$"
        };

        Assert.Equal(2, request.DecimalPlaces);
    }

    [Fact]
    public void CreateCurrencyRequest_DefaultIsActive_IsTrue()
    {
        var request = new Maliev.CurrencyService.Application.DTOs.Currencies.CreateCurrencyRequest
        {
            Code = "USD",
            Name = "United States Dollar",
            Symbol = "$"
        };

        Assert.True(request.IsActive);
    }

    #endregion

    #region Application DTOs - UpdateCurrencyRequest

    [Fact]
    public void UpdateCurrencyRequest_ValidRequest_PassesValidation()
    {
        var request = new Maliev.CurrencyService.Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            Name = "Euro",
            Symbol = "€",
            DecimalPlaces = 2,
            IsActive = true
        };

        var validationResults = ValidateModel(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void UpdateCurrencyRequest_AllPropertiesOptional_PassesValidation()
    {
        var request = new Maliev.CurrencyService.Application.DTOs.Currencies.UpdateCurrencyRequest();

        var validationResults = ValidateModel(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void UpdateCurrencyRequest_DecimalPlacesOutOfRange_FailsValidation()
    {
        var request = new Maliev.CurrencyService.Application.DTOs.Currencies.UpdateCurrencyRequest
        {
            DecimalPlaces = 10
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    #endregion

    #region Application DTOs - PaginatedResponse

    [Fact]
    public void PaginatedResponse_SetProperties_ValuesAreCorrectlySet()
    {
        var items = new List<string> { "item1", "item2" };
        var response = new PaginatedResponse<string>
        {
            Items = items,
            Page = 2,
            PageSize = 10,
            TotalCount = 25,
            TotalPages = 3
        };

        Assert.Equal(2, response.Items.Count());
        Assert.Equal(2, response.Page);
        Assert.Equal(10, response.PageSize);
        Assert.Equal(25, response.TotalCount);
        Assert.Equal(3, response.TotalPages);
    }

    [Fact]
    public void PaginatedResponse_HasNextPage_WhenPageLessThanTotalPages()
    {
        var response = new PaginatedResponse<string>
        {
            Items = Enumerable.Empty<string>(),
            Page = 2,
            PageSize = 10,
            TotalCount = 25,
            TotalPages = 3
        };

        Assert.True(response.HasNextPage);
        Assert.True(response.HasPreviousPage);
    }

    [Fact]
    public void PaginatedResponse_HasPreviousPage_WhenPageGreaterThanOne()
    {
        var response = new PaginatedResponse<string>
        {
            Items = Enumerable.Empty<string>(),
            Page = 3,
            PageSize = 10,
            TotalCount = 25,
            TotalPages = 3
        };

        Assert.False(response.HasNextPage);
        Assert.True(response.HasPreviousPage);
    }

    [Fact]
    public void PaginatedResponse_NoNextPage_OnLastPage()
    {
        var response = new PaginatedResponse<string>
        {
            Items = Enumerable.Empty<string>(),
            Page = 3,
            PageSize = 10,
            TotalCount = 25,
            TotalPages = 3
        };

        Assert.False(response.HasNextPage);
    }

    #endregion

    #region Application DTOs - UpdateRateRequest

    [Fact]
    public void UpdateRateRequest_SetProperties_ValuesAreCorrectlySet()
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

    #region Application DTOs - SnapshotAuditLog

    [Fact]
    public void SnapshotAuditLog_SetProperties_ValuesAreCorrectlySet()
    {
        var timestamp = DateTime.UtcNow;
        var log = new SnapshotAuditLog
        {
            BatchId = "batch-123",
            Timestamp = timestamp,
            RecordCount = 100,
            Source = "ECB",
            SubmittedBy = "admin"
        };

        Assert.Equal("batch-123", log.BatchId);
        Assert.Equal(timestamp, log.Timestamp);
        Assert.Equal(100, log.RecordCount);
        Assert.Equal("ECB", log.Source);
        Assert.Equal("admin", log.SubmittedBy);
    }

    [Fact]
    public void SnapshotAuditLog_DefaultValues_AreEmptyOrZero()
    {
        var log = new SnapshotAuditLog();

        Assert.Equal(string.Empty, log.BatchId);
        Assert.Equal(string.Empty, log.Source);
        Assert.Equal(string.Empty, log.SubmittedBy);
        Assert.Equal(0, log.RecordCount);
    }

    #endregion

    #region Application DTOs - SnapshotBatchRequest

    [Fact]
    public void SnapshotBatchRequest_SetProperties_ValuesAreCorrectlySet()
    {
        var snapshotDate = new DateOnly(2024, 1, 15);
        var snapshots = new List<SnapshotEntry>
        {
            new() { From = "USD", To = "EUR", Rate = 0.85m },
            new() { From = "USD", To = "GBP", Rate = 0.75m }
        };

        var request = new SnapshotBatchRequest
        {
            SnapshotDate = snapshotDate,
            Source = "ECB",
            Snapshots = snapshots,
            AutoPromote = true
        };

        Assert.Equal(snapshotDate, request.SnapshotDate);
        Assert.Equal("ECB", request.Source);
        Assert.Equal(2, request.Snapshots.Count);
        Assert.True(request.AutoPromote);
    }

    [Fact]
    public void SnapshotBatchRequest_DefaultAutoPromote_IsFalse()
    {
        var request = new SnapshotBatchRequest
        {
            SnapshotDate = new DateOnly(2024, 1, 15),
            Source = "ECB",
            Snapshots = new List<SnapshotEntry>()
        };

        Assert.False(request.AutoPromote);
    }

    #endregion

    #region Application DTOs - SnapshotEntry

    [Fact]
    public void SnapshotEntry_SetProperties_ValuesAreCorrectlySet()
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

    #endregion

    #region Application DTOs - SnapshotBatchResponse

    [Fact]
    public void SnapshotBatchResponse_SetProperties_ValuesAreCorrectlySet()
    {
        var response = new SnapshotBatchResponse
        {
            BatchId = "batch-123",
            SnapshotDate = new DateOnly(2024, 1, 15),
            Source = "ECB",
            SuccessCount = 95,
            FailureCount = 5,
            Status = "partial",
            ProcessedAt = DateTime.UtcNow
        };

        Assert.Equal("batch-123", response.BatchId);
        Assert.Equal(new DateOnly(2024, 1, 15), response.SnapshotDate);
        Assert.Equal("ECB", response.Source);
        Assert.Equal(95, response.SuccessCount);
        Assert.Equal(5, response.FailureCount);
        Assert.Equal("partial", response.Status);
    }

    [Fact]
    public void SnapshotBatchResponse_WithErrors_HasErrorDictionary()
    {
        var response = new SnapshotBatchResponse
        {
            BatchId = "batch-123",
            SnapshotDate = new DateOnly(2024, 1, 15),
            Source = "ECB",
            SuccessCount = 95,
            FailureCount = 5,
            Status = "partial",
            Errors = new Dictionary<string, string[]>
            {
                { "USD/EUR", new[] { "Invalid rate format" } }
            }
        };

        Assert.NotNull(response.Errors);
        Assert.Contains("USD/EUR", response.Errors.Keys);
    }

    #endregion

    #region API Models - GetExchangeRateRequest

    [Fact]
    public void GetExchangeRateRequest_ValidRequest_PassesValidation()
    {
        var request = new Api.Models.GetExchangeRateRequest
        {
            From = "USD",
            To = "EUR"
        };

        var validationResults = ValidateModel(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void GetExchangeRateRequest_MissingFrom_FailsValidation()
    {
        var request = new Api.Models.GetExchangeRateRequest
        {
            From = null!,
            To = "EUR"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void GetExchangeRateRequest_MissingTo_FailsValidation()
    {
        var request = new Api.Models.GetExchangeRateRequest
        {
            From = "USD",
            To = null!
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void GetExchangeRateRequest_FromTooShort_FailsValidation()
    {
        var request = new Api.Models.GetExchangeRateRequest
        {
            From = "US",
            To = "EUR"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    #endregion

    #region API Models - PagedResult

    [Fact]
    public void PagedResult_SetProperties_ValuesAreCorrectlySet()
    {
        var items = new List<string> { "item1", "item2" };
        var result = new Maliev.CurrencyService.Api.Models.PagedResult<string>
        {
            Items = items,
            TotalCount = 25,
            Page = 2,
            PageSize = 10
        };

        Assert.Equal(2, result.Items.Count());
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public void PagedResult_TotalPages_CalculatesCorrectly()
    {
        var result = new Maliev.CurrencyService.Api.Models.PagedResult<string>
        {
            Items = Enumerable.Empty<string>(),
            TotalCount = 25,
            Page = 1,
            PageSize = 10
        };

        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void PagedResult_HasNextPage_WhenPageLessThanTotalPages()
    {
        var result = new Maliev.CurrencyService.Api.Models.PagedResult<string>
        {
            Items = Enumerable.Empty<string>(),
            TotalCount = 25,
            Page = 2,
            PageSize = 10
        };

        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public void PagedResult_ZeroTotalCount_HasZeroTotalPages()
    {
        var result = new Maliev.CurrencyService.Api.Models.PagedResult<string>
        {
            Items = Enumerable.Empty<string>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 10
        };

        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    #endregion

    #region API Models - RedisOptions

    [Fact]
    public void RedisOptions_ValidOptions_PassesValidation()
    {
        var options = new Api.Models.RedisOptions
        {
            ConnectionString = "localhost:6379",
            InstanceName = "currency-service",
            Database = 1,
            ConnectTimeout = 10,
            SyncTimeout = 5000,
            AbortOnConnectFail = true
        };

        var validationResults = ValidateModel(options);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void RedisOptions_MissingConnectionString_FailsValidation()
    {
        var options = new Api.Models.RedisOptions
        {
            ConnectionString = null!
        };

        var validationResults = ValidateModel(options);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void RedisOptions_DefaultValues_AreCorrect()
    {
        var options = new Api.Models.RedisOptions
        {
            ConnectionString = "localhost:6379"
        };

        Assert.Equal(0, options.Database);
        Assert.Equal(5, options.ConnectTimeout);
        Assert.Equal(5000, options.SyncTimeout);
        Assert.False(options.AbortOnConnectFail);
    }

    [Fact]
    public void RedisOptions_SectionName_IsRedis()
    {
        Assert.Equal("Redis", Api.Models.RedisOptions.SectionName);
    }

    #endregion

    #region API Models - ProviderMetrics

    [Fact]
    public void ProviderMetrics_SetProperties_ValuesAreCorrectlySet()
    {
        var metrics = new Api.Models.ProviderMetrics
        {
            ProviderName = "Frankfurter",
            TotalRequests = 100,
            SuccessfulRequests = 95,
            TotalResponseTimeMs = 5000,
            LastRequestAt = DateTime.UtcNow
        };

        Assert.Equal("Frankfurter", metrics.ProviderName);
        Assert.Equal(100, metrics.TotalRequests);
        Assert.Equal(95, metrics.SuccessfulRequests);
        Assert.Equal(5000, metrics.TotalResponseTimeMs);
    }

    [Fact]
    public void ProviderMetrics_SuccessRate_CalculatesCorrectly()
    {
        var metrics = new Api.Models.ProviderMetrics
        {
            TotalRequests = 100,
            SuccessfulRequests = 95
        };

        Assert.Equal(0.95, metrics.SuccessRate);
    }

    [Fact]
    public void ProviderMetrics_ZeroRequests_SuccessRateIsZero()
    {
        var metrics = new Api.Models.ProviderMetrics
        {
            TotalRequests = 0,
            SuccessfulRequests = 0
        };

        Assert.Equal(0, metrics.SuccessRate);
    }

    [Fact]
    public void ProviderMetrics_AverageResponseTime_CalculatesCorrectly()
    {
        var metrics = new Api.Models.ProviderMetrics
        {
            TotalRequests = 10,
            TotalResponseTimeMs = 500
        };

        Assert.Equal(50, metrics.AverageResponseTimeMs);
    }

    [Fact]
    public void ProviderMetrics_ErrorRequests_CalculatesCorrectly()
    {
        var metrics = new Api.Models.ProviderMetrics
        {
            TotalRequests = 100,
            SuccessfulRequests = 85
        };

        Assert.Equal(15, metrics.ErrorRequests);
    }

    [Fact]
    public void ProviderMetrics_ErrorRate_CalculatesCorrectly()
    {
        var metrics = new Api.Models.ProviderMetrics
        {
            TotalRequests = 100,
            SuccessfulRequests = 90
        };

        Assert.Equal(0.10, metrics.ErrorRate);
    }

    #endregion

    #region API Models - ExchangeRateOptions

    [Fact]
    public void ExchangeRateOptions_ValidOptions_PassesValidation()
    {
        var options = new Api.Models.ExchangeRateOptions
        {
            CacheDurationMinutes = 60,
            RetryAttempts = 5,
            TimeoutSeconds = 60,
            FrankfurterApiUrl = "https://api.frankfurter.app/",
            EnableDynamicPrioritization = true,
            MinRequestsForPrioritization = 20,
            ResponseTimeWeight = 0.5,
            SuccessRateWeight = 0.3,
            ErrorRateWeight = 0.1,
            RequestCountWeight = 0.1
        };

        var validationResults = ValidateModel(options);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void ExchangeRateOptions_MissingFrankfurterApiUrl_PassesValidation()
    {
        var options = new Api.Models.ExchangeRateOptions
        {
            CacheDurationMinutes = 30,
            RetryAttempts = 3,
            TimeoutSeconds = 30
        };

        var validationResults = ValidateModel(options);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void ExchangeRateOptions_DefaultValues_AreCorrect()
    {
        var options = new Api.Models.ExchangeRateOptions();

        Assert.Equal(30, options.CacheDurationMinutes);
        Assert.Equal(3, options.RetryAttempts);
        Assert.Equal(30, options.TimeoutSeconds);
        Assert.Equal("https://api.frankfurter.app/", options.FrankfurterApiUrl);
        Assert.False(options.EnableDynamicPrioritization);
        Assert.Equal(10, options.MinRequestsForPrioritization);
        Assert.Equal(0.4, options.ResponseTimeWeight);
        Assert.Equal(0.3, options.SuccessRateWeight);
        Assert.Equal(0.2, options.ErrorRateWeight);
        Assert.Equal(0.1, options.RequestCountWeight);
    }

    [Fact]
    public void ExchangeRateOptions_ProviderOrder_DefaultContainsFourProviders()
    {
        var options = new Api.Models.ExchangeRateOptions();

        Assert.Equal(4, options.ProviderOrder.Count);
        Assert.Contains("Frankfurter", options.ProviderOrder);
        Assert.Contains("Fawazahmed", options.ProviderOrder);
    }

    [Fact]
    public void ExchangeRateOptions_SectionName_IsExchangeRate()
    {
        Assert.Equal("ExchangeRate", Api.Models.ExchangeRateOptions.SectionName);
    }

    [Fact]
    public void ExchangeRateOptions_CacheDurationOutOfRange_FailsValidation()
    {
        var options = new Api.Models.ExchangeRateOptions
        {
            CacheDurationMinutes = 2000
        };

        var validationResults = ValidateModel(options);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ExchangeRateOptions_RetryAttemptsOutOfRange_FailsValidation()
    {
        var options = new Api.Models.ExchangeRateOptions
        {
            RetryAttempts = 100
        };

        var validationResults = ValidateModel(options);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ExchangeRateOptions_TimeoutOutOfRange_FailsValidation()
    {
        var options = new Api.Models.ExchangeRateOptions
        {
            TimeoutSeconds = 500
        };

        var validationResults = ValidateModel(options);

        Assert.NotEmpty(validationResults);
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
