using System.ComponentModel.DataAnnotations;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Common;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class TestsV4
{
    #region API Models - ExchangeRateDto Tests

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

    [Fact]
    public void ApiModels_ExchangeRateDto_DefaultValues_AreDefault()
    {
        var dto = new Api.Models.ExchangeRateDto
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 1.0m,
            Source = "Test"
        };

        Assert.NotEqual(default, dto.FromCurrency);
        Assert.NotEqual(default, dto.ToCurrency);
    }

    #endregion

    #region API Models - CurrencyDto Tests

    [Fact]
    public void ApiModels_CurrencyDto_SetProperties_SetsCorrectValues()
    {
        var dto = new Api.Models.CurrencyDto
        {
            Id = 1,
            ShortName = "USD",
            LongName = "United States Dollar",
            CreatedDate = new DateTime(2024, 1, 1),
            ModifiedDate = new DateTime(2024, 1, 2)
        };

        Assert.Equal(1, dto.Id);
        Assert.Equal("USD", dto.ShortName);
        Assert.Equal("United States Dollar", dto.LongName);
    }

    [Fact]
    public void ApiModels_CurrencyDto_DefaultId_IsZero()
    {
        var dto = new Api.Models.CurrencyDto
        {
            ShortName = "USD",
            LongName = "United States Dollar"
        };

        Assert.Equal(0, dto.Id);
    }

    #endregion

    #region API Models - ConvertCurrencyRequest Tests

    [Fact]
    public void ApiModels_ConvertCurrencyRequest_ValidRequest_PassesValidation()
    {
        var request = new Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 100.00m
        };

        var validationResults = ValidateModel(request);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void ApiModels_ConvertCurrencyRequest_FromTooShort_FailsValidation()
    {
        var request = new Api.Models.ConvertCurrencyRequest
        {
            From = "US",
            To = "EUR",
            Amount = 100.00m
        };

        var validationResults = ValidateModel(request);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ApiModels_ConvertCurrencyRequest_AmountTooSmall_FailsValidation()
    {
        var request = new Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 0.00m
        };

        var validationResults = ValidateModel(request);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ApiModels_ConvertCurrencyRequest_MissingFrom_FailsValidation()
    {
        var request = new Api.Models.ConvertCurrencyRequest
        {
            From = null!,
            To = "EUR",
            Amount = 100.00m
        };

        var validationResults = ValidateModel(request);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ApiModels_ConvertCurrencyRequest_MissingAmount_FailsValidation()
    {
        var request = new Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR"
        };

        var validationResults = ValidateModel(request);
        Assert.NotEmpty(validationResults);
    }

    #endregion

    #region API Models - ConvertCurrencyResponse Tests

    [Fact]
    public void ApiModels_ConvertCurrencyResponse_SetProperties_SetsCorrectValues()
    {
        var response = new Api.Models.ConvertCurrencyResponse
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

    #endregion

    #region API Models - GetExchangeRateRequest Tests

    [Fact]
    public void ApiModels_GetExchangeRateRequest_ValidRequest_PassesValidation()
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
    public void ApiModels_GetExchangeRateRequest_FromTooShort_FailsValidation()
    {
        var request = new Api.Models.GetExchangeRateRequest
        {
            From = "US",
            To = "EUR"
        };

        var validationResults = ValidateModel(request);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ApiModels_GetExchangeRateRequest_MissingFrom_FailsValidation()
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
    public void ApiModels_GetExchangeRateRequest_MissingTo_FailsValidation()
    {
        var request = new Api.Models.GetExchangeRateRequest
        {
            From = "USD",
            To = null!
        };

        var validationResults = ValidateModel(request);
        Assert.NotEmpty(validationResults);
    }

    #endregion

    #region Application DTOs - CurrencyResponse Tests

    [Fact]
    public void ApplicationDtos_CurrencyResponse_SetProperties_SetsCorrectValues()
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

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("USD", response.Code);
        Assert.Equal("$", response.Symbol);
        Assert.Equal("United States Dollar", response.Name);
        Assert.Equal(2, response.DecimalPlaces);
        Assert.True(response.IsActive);
        Assert.True(response.IsPrimary);
    }

    [Fact]
    public void ApplicationDtos_CurrencyResponse_Validation_RequiresCode()
    {
        var response = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = null!,
            Symbol = "$",
            Name = "United States Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var validationResults = ValidateModel(response);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ApplicationDtos_CurrencyResponse_Validation_InvalidCodeFormat()
    {
        var response = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "usd",
            Symbol = "$",
            Name = "United States Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var validationResults = ValidateModel(response);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ApplicationDtos_CurrencyResponse_Validation_DecimalPlacesOutOfRange()
    {
        var response = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            Name = "United States Dollar",
            DecimalPlaces = 10,
            IsActive = true,
            IsPrimary = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var validationResults = ValidateModel(response);
        Assert.NotEmpty(validationResults);
    }

    #endregion

    #region Application DTOs - UpdateRateRequest Tests

    [Fact]
    public void ApplicationDtos_UpdateRateRequest_SetProperties_SetsCorrectValues()
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

    [Fact]
    public void ApplicationDtos_UpdateRateRequest_DefaultValues_AreDefault()
    {
        var request = new UpdateRateRequest
        {
            From = "USD",
            To = "EUR",
            Rate = 1.0m
        };

        Assert.NotNull(request.From);
        Assert.NotNull(request.To);
        Assert.Equal(1.0m, request.Rate);
    }

    #endregion

    #region Application DTOs - ExchangeRateResponse Tests

    [Fact]
    public void ApplicationDtos_ExchangeRateResponse_SetProperties_SetsCorrectValues()
    {
        var now = DateTime.UtcNow;
        var response = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = now,
            Source = "Frankfurter",
            IsTransitive = false,
            IntermediateCurrency = null,
            CalculationDetails = null,
            Mode = "live",
            SnapshotDate = null
        };

        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("EUR", response.ToCurrency);
        Assert.Equal(0.85m, response.Rate);
        Assert.Equal("Frankfurter", response.Source);
        Assert.False(response.IsTransitive);
        Assert.Equal("live", response.Mode);
    }

    [Fact]
    public void ApplicationDtos_ExchangeRateResponse_Validation_RequiresFromCurrency()
    {
        var response = new ExchangeRateResponse
        {
            FromCurrency = null!,
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Frankfurter",
            IsTransitive = false,
            Mode = "live"
        };

        var validationResults = ValidateModel(response);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ApplicationDtos_ExchangeRateResponse_Validation_InvalidFromCurrencyFormat()
    {
        var response = new ExchangeRateResponse
        {
            FromCurrency = "usd",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = DateTime.UtcNow,
            Source = "Frankfurter",
            IsTransitive = false,
            Mode = "live"
        };

        var validationResults = ValidateModel(response);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ApplicationDtos_ExchangeRateResponse_WithTransitive_CalculationDetails()
    {
        var response = new ExchangeRateResponse
        {
            FromCurrency = "THB",
            ToCurrency = "EUR",
            Rate = 0.025m,
            Timestamp = DateTime.UtcNow,
            Source = "Transitive",
            IsTransitive = true,
            IntermediateCurrency = "USD",
            CalculationDetails = "USD/THB × THB/EUR",
            Mode = "live"
        };

        Assert.True(response.IsTransitive);
        Assert.Equal("USD", response.IntermediateCurrency);
        Assert.NotNull(response.CalculationDetails);
    }

    [Fact]
    public void ApplicationDtos_ExchangeRateResponse_SnapshotMode_HasSnapshotDate()
    {
        var snapshotDate = new DateOnly(2024, 1, 15);
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

    #region Application DTOs - PaginatedResponse Tests

    [Fact]
    public void ApplicationDtos_PaginatedResponse_SetProperties_SetsCorrectValues()
    {
        var items = new List<string> { "item1", "item2", "item3" };
        var response = new PaginatedResponse<string>
        {
            Items = items,
            Page = 1,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };

        Assert.Equal(3, response.Items.Count());
        Assert.Equal(1, response.Page);
        Assert.Equal(10, response.PageSize);
        Assert.Equal(100, response.TotalCount);
    }

    [Fact]
    public void ApplicationDtos_PaginatedResponse_HasNextPage_ReturnsTrue_WhenNotLastPage()
    {
        var response = new PaginatedResponse<string>
        {
            Items = new List<string>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };

        Assert.True(response.HasNextPage);
    }

    [Fact]
    public void ApplicationDtos_PaginatedResponse_HasNextPage_ReturnsFalse_WhenLastPage()
    {
        var response = new PaginatedResponse<string>
        {
            Items = new List<string>(),
            Page = 10,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };

        Assert.False(response.HasNextPage);
    }

    [Fact]
    public void ApplicationDtos_PaginatedResponse_HasPreviousPage_ReturnsTrue_WhenNotFirstPage()
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

    [Fact]
    public void ApplicationDtos_PaginatedResponse_HasPreviousPage_ReturnsFalse_WhenFirstPage()
    {
        var response = new PaginatedResponse<string>
        {
            Items = new List<string>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 100,
            TotalPages = 10
        };

        Assert.False(response.HasPreviousPage);
    }

    [Fact]
    public void ApplicationDtos_PaginatedResponse_TotalPages_ZeroItems()
    {
        var response = new PaginatedResponse<string>
        {
            Items = new List<string>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 0
        };

        Assert.False(response.HasNextPage);
        Assert.False(response.HasPreviousPage);
    }

    #endregion

    #region Snapshot DTOs Tests

    [Fact]
    public void SnapshotDtos_SnapshotBatchRequest_SetProperties()
    {
        var request = new SnapshotBatchRequest
        {
            SnapshotDate = new DateOnly(2024, 1, 15),
            Source = "Frankfurter",
            AutoPromote = true,
            Snapshots = new List<SnapshotEntry>
            {
                new() { From = "USD", To = "EUR", Rate = 0.85m },
                new() { From = "USD", To = "GBP", Rate = 0.75m }
            }
        };

        Assert.Equal(new DateOnly(2024, 1, 15), request.SnapshotDate);
        Assert.Equal("Frankfurter", request.Source);
        Assert.True(request.AutoPromote);
        Assert.Equal(2, request.Snapshots.Count);
    }

    [Fact]
    public void SnapshotDtos_SnapshotBatchResponse_SetProperties()
    {
        var response = new SnapshotBatchResponse
        {
            BatchId = Guid.NewGuid().ToString(),
            SnapshotDate = new DateOnly(2024, 1, 15),
            Source = "Frankfurter",
            SuccessCount = 10,
            FailureCount = 2,
            Status = "promoted",
            ProcessedAt = DateTime.UtcNow
        };

        Assert.NotNull(response.BatchId);
        Assert.Equal(10, response.SuccessCount);
        Assert.Equal(2, response.FailureCount);
        Assert.Equal("promoted", response.Status);
    }

    [Fact]
    public void SnapshotDtos_SnapshotAuditLog_SetProperties()
    {
        var auditLog = new SnapshotAuditLog
        {
            BatchId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            RecordCount = 50,
            Source = "Frankfurter",
            SubmittedBy = "Admin"
        };

        Assert.NotNull(auditLog.BatchId);
        Assert.Equal(50, auditLog.RecordCount);
        Assert.Equal("Frankfurter", auditLog.Source);
        Assert.Equal("Admin", auditLog.SubmittedBy);
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
