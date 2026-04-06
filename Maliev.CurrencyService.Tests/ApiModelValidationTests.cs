using System.ComponentModel.DataAnnotations;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class UnitTests
{
    #region API Model Validation Tests

    #region CreateCurrencyRequest (API Models) Tests

    [Fact]
    public void ApiModels_CreateCurrencyRequest_ValidRequest_PassesValidation()
    {
        var request = new Api.Models.CreateCurrencyRequest
        {
            ShortName = "USD",
            LongName = "United States Dollar"
        };

        var validationResults = ValidateModel(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void ApiModels_CreateCurrencyRequest_ShortNameTooShort_FailsValidation()
    {
        var request = new Api.Models.CreateCurrencyRequest
        {
            ShortName = "US",
            LongName = "United States Dollar"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, v => v.ErrorMessage!.Contains("3 uppercase letters"));
    }

    [Fact]
    public void ApiModels_CreateCurrencyRequest_ShortNameLowercase_FailsValidation()
    {
        var request = new Api.Models.CreateCurrencyRequest
        {
            ShortName = "usd",
            LongName = "United States Dollar"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, v => v.ErrorMessage!.Contains("uppercase"));
    }

    [Fact]
    public void ApiModels_CreateCurrencyRequest_MissingShortName_FailsValidation()
    {
        var request = new Api.Models.CreateCurrencyRequest
        {
            ShortName = null!,
            LongName = "United States Dollar"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ApiModels_CreateCurrencyRequest_MissingLongName_FailsValidation()
    {
        var request = new Api.Models.CreateCurrencyRequest
        {
            ShortName = "USD",
            LongName = null!
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    #endregion

    #region UpdateCurrencyRequest (API Models) Tests

    [Fact]
    public void ApiModels_UpdateCurrencyRequest_ValidRequest_PassesValidation()
    {
        var request = new Api.Models.UpdateCurrencyRequest
        {
            ShortName = "EUR",
            LongName = "Euro"
        };

        var validationResults = ValidateModel(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void ApiModels_UpdateCurrencyRequest_InvalidShortName_FailsValidation()
    {
        var request = new Api.Models.UpdateCurrencyRequest
        {
            ShortName = "euro",
            LongName = "Euro"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    #endregion

    #region ConvertCurrencyRequest Tests

    [Fact]
    public void ConvertCurrencyRequest_ValidRequest_PassesValidation()
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
    public void ConvertCurrencyRequest_ZeroAmount_FailsValidation()
    {
        var request = new Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 0.00m
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, v => v.ErrorMessage!.Contains("greater than 0"));
    }

    [Fact]
    public void ConvertCurrencyRequest_NegativeAmount_FailsValidation()
    {
        var request = new Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = -50.00m
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ConvertCurrencyRequest_MaxAmount_PassesValidation()
    {
        var request = new Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 1000000000000.00m
        };

        var validationResults = ValidateModel(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void ConvertCurrencyRequest_MissingFrom_FailsValidation()
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
    public void ConvertCurrencyRequest_MissingTo_FailsValidation()
    {
        var request = new Api.Models.ConvertCurrencyRequest
        {
            From = "USD",
            To = null!,
            Amount = 100.00m
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    #endregion

    #endregion

    #region API Model DTO Tests

    [Fact]
    public void CurrencyDto_SetProperties_ValuesAreCorrectlySet()
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
        Assert.Equal(new DateTime(2024, 1, 1), dto.CreatedDate);
        Assert.Equal(new DateTime(2024, 1, 2), dto.ModifiedDate);
    }

    [Fact]
    public void ExchangeRateDto_SetProperties_ValuesAreCorrectlySet()
    {
        var dto = new Api.Models.ExchangeRateDto
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            FetchedAt = new DateTime(2024, 1, 1, 12, 0, 0),
            Source = "Frankfurter"
        };

        Assert.Equal("USD", dto.FromCurrency);
        Assert.Equal("EUR", dto.ToCurrency);
        Assert.Equal(0.85m, dto.Rate);
        Assert.Equal(new DateTime(2024, 1, 1, 12, 0, 0), dto.FetchedAt);
        Assert.Equal("Frankfurter", dto.Source);
    }

    [Fact]
    public void ConvertCurrencyResponse_SetProperties_ValuesAreCorrectlySet()
    {
        var response = new Api.Models.ConvertCurrencyResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            OriginalAmount = 100.00m,
            ConvertedAmount = 85.00m,
            ExchangeRate = 0.85m,
            RateTimestamp = new DateTime(2024, 1, 1),
            Source = "Frankfurter"
        };

        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("EUR", response.ToCurrency);
        Assert.Equal(100.00m, response.OriginalAmount);
        Assert.Equal(85.00m, response.ConvertedAmount);
        Assert.Equal(0.85m, response.ExchangeRate);
        Assert.Equal(new DateTime(2024, 1, 1), response.RateTimestamp);
        Assert.Equal("Frankfurter", response.Source);
    }

    #endregion

    #region Application DTO Tests

    [Fact]
    public void ApplicationDto_CurrencyResponse_SetProperties_ValuesAreCorrectlySet()
    {
        var response = new CurrencyResponse
        {
            Id = Guid.NewGuid(),
            Code = "USD",
            Symbol = "$",
            Name = "United States Dollar",
            DecimalPlaces = 2,
            IsActive = true,
            IsPrimary = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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
    public void ApplicationDto_ExchangeRateResponse_SetProperties_ValuesAreCorrectlySet()
    {
        var timestamp = DateTime.UtcNow;
        var response = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "EUR",
            Rate = 0.85m,
            Timestamp = timestamp,
            Source = "Frankfurter",
            IsTransitive = false,
            IntermediateCurrency = null,
            CalculationDetails = null,
            Mode = "live"
        };

        Assert.Equal("USD", response.FromCurrency);
        Assert.Equal("EUR", response.ToCurrency);
        Assert.Equal(0.85m, response.Rate);
        Assert.Equal(timestamp, response.Timestamp);
        Assert.Equal("Frankfurter", response.Source);
        Assert.False(response.IsTransitive);
        Assert.Null(response.IntermediateCurrency);
        Assert.Null(response.CalculationDetails);
        Assert.Equal("live", response.Mode);
    }

    [Fact]
    public void ApplicationDto_ExchangeRateResponse_TransitiveRate_HasCalculationDetails()
    {
        var response = new ExchangeRateResponse
        {
            FromCurrency = "USD",
            ToCurrency = "JPY",
            Rate = 150.0m,
            Timestamp = DateTime.UtcNow,
            Source = "Calculated",
            IsTransitive = true,
            IntermediateCurrency = "EUR",
            CalculationDetails = "USD/EUR × EUR/JPY",
            Mode = "live"
        };

        Assert.True(response.IsTransitive);
        Assert.Equal("EUR", response.IntermediateCurrency);
        Assert.NotNull(response.CalculationDetails);
        Assert.Equal("USD/EUR × EUR/JPY", response.CalculationDetails);
    }

    [Fact]
    public void ApplicationDto_PaginatedCurrencyResponse_SetProperties_ValuesAreCorrectlySet()
    {
        var items = new List<CurrencyResponse>
        {
            new() { Id = Guid.NewGuid(), Code = "USD", Name = "US Dollar", Symbol = "$", DecimalPlaces = 2, IsActive = true, IsPrimary = true },
            new() { Id = Guid.NewGuid(), Code = "EUR", Name = "Euro", Symbol = "€", DecimalPlaces = 2, IsActive = true, IsPrimary = false }
        };

        var response = new PaginatedCurrencyResponse
        {
            Items = items,
            Page = 1,
            PageSize = 50,
            TotalCount = 2,
            TotalPages = 1
        };

        Assert.Equal(2, response.Items.Count());
        Assert.Equal(1, response.Page);
        Assert.Equal(50, response.PageSize);
        Assert.Equal(2, response.TotalCount);
        Assert.Equal(1, response.TotalPages);
    }

    [Fact]
    public void ApplicationDto_PaginatedCurrencyResponse_EmptyItems_HasZeroTotalCount()
    {
        var response = new PaginatedCurrencyResponse
        {
            Items = Enumerable.Empty<CurrencyResponse>(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0,
            TotalPages = 0
        };

        Assert.Empty(response.Items);
        Assert.Equal(0, response.TotalCount);
        Assert.Equal(0, response.TotalPages);
    }

    [Fact]
    public void ApplicationDto_ExchangeRateResponse_SnapshotMode_HasSnapshotDate()
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
