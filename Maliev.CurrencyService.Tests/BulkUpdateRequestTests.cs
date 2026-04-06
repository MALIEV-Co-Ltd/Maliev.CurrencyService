using System.ComponentModel.DataAnnotations;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class ApiUncoveredTests
{
    #region BulkUpdateRatesRequest Tests

    [Fact]
    public void BulkUpdateRatesRequest_SetProperties_ValuesAreCorrectlySet()
    {
        var rates = new List<UpdateRateRequest>
        {
            new() { From = "USD", To = "EUR", Rate = 0.85m },
            new() { From = "USD", To = "GBP", Rate = 0.75m }
        };

        var request = new Api.Models.Rates.BulkUpdateRatesRequest
        {
            Rates = rates
        };

        Assert.Equal(2, request.Rates.Count);
        Assert.Equal("USD", request.Rates[0].From);
        Assert.Equal("EUR", request.Rates[0].To);
        Assert.Equal(0.85m, request.Rates[0].Rate);
    }

    [Fact]
    public void BulkUpdateRatesRequest_EmptyRates_ListIsEmpty()
    {
        var request = new Api.Models.Rates.BulkUpdateRatesRequest
        {
            Rates = new List<UpdateRateRequest>()
        };

        Assert.Empty(request.Rates);
    }

    #endregion

    #region SetRateSourceRequest Tests

    [Fact]
    public void SetRateSourceRequest_SetProperties_ValuesAreCorrectlySet()
    {
        var request = new Api.Models.Rates.SetRateSourceRequest
        {
            ProviderName = "Frankfurter"
        };

        Assert.Equal("Frankfurter", request.ProviderName);
    }

    [Fact]
    public void SetRateSourceRequest_SetEmptyProviderName_ValueIsEmpty()
    {
        var request = new Api.Models.Rates.SetRateSourceRequest
        {
            ProviderName = string.Empty
        };

        Assert.Equal(string.Empty, request.ProviderName);
    }

    #endregion

    #region ValidationReport Tests

    [Fact]
    public void ValidationReport_SetProperties_ValuesAreCorrectlySet()
    {
        var report = new Api.Models.Snapshots.ValidationReport
        {
            IsValid = true,
            ValidationErrors = new List<string> { "Error1", "Error2" },
            RecordCount = 100,
            IsDryRun = true
        };

        Assert.True(report.IsValid);
        Assert.Equal(2, report.ValidationErrors.Count);
        Assert.Equal("Error1", report.ValidationErrors[0]);
        Assert.Equal(100, report.RecordCount);
        Assert.True(report.IsDryRun);
    }

    [Fact]
    public void ValidationReport_DefaultValidationErrors_ListIsEmpty()
    {
        var report = new Api.Models.Snapshots.ValidationReport();

        Assert.NotNull(report.ValidationErrors);
        Assert.Empty(report.ValidationErrors);
    }

    [Fact]
    public void ValidationReport_IsValidFalse_HasValidationErrors()
    {
        var report = new Api.Models.Snapshots.ValidationReport
        {
            IsValid = false,
            ValidationErrors = new List<string> { "Invalid rate value", "Missing currency code" },
            RecordCount = 2,
            IsDryRun = false
        };

        Assert.False(report.IsValid);
        Assert.Equal(2, report.ValidationErrors.Count);
        Assert.False(report.IsDryRun);
    }

    #endregion

    #region SnapshotIngestionResult Tests

    [Fact]
    public void SnapshotIngestionResult_SetProperties_ValuesAreCorrectlySet()
    {
        var submittedAt = new DateTime(2024, 1, 15, 10, 30, 0);
        var result = new Api.Models.Snapshots.SnapshotIngestionResult
        {
            BatchId = "batch-123",
            Status = "Queued",
            RecordCount = 50,
            SubmittedAt = submittedAt
        };

        Assert.Equal("batch-123", result.BatchId);
        Assert.Equal("Queued", result.Status);
        Assert.Equal(50, result.RecordCount);
        Assert.Equal(submittedAt, result.SubmittedAt);
    }

    [Fact]
    public void SnapshotIngestionResult_CompletedStatus_HasCorrectStatus()
    {
        var result = new Api.Models.Snapshots.SnapshotIngestionResult
        {
            BatchId = "batch-456",
            Status = "Completed",
            RecordCount = 100,
            SubmittedAt = DateTime.UtcNow
        };

        Assert.Equal("Completed", result.Status);
    }

    [Fact]
    public void SnapshotIngestionResult_FailedStatus_HasCorrectStatus()
    {
        var result = new Api.Models.Snapshots.SnapshotIngestionResult
        {
            BatchId = "batch-789",
            Status = "Failed",
            RecordCount = 0,
            SubmittedAt = DateTime.UtcNow
        };

        Assert.Equal("Failed", result.Status);
    }

    #endregion

    #region RateQueryRequest Tests

    [Fact]
    public void RateQueryRequest_ValidRequest_PassesValidation()
    {
        var request = new Api.Models.Rates.RateQueryRequest
        {
            From = "USD",
            To = "EUR"
        };

        var validationResults = ValidateModel(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void RateQueryRequest_FromTooShort_FailsValidation()
    {
        var request = new Api.Models.Rates.RateQueryRequest
        {
            From = "US",
            To = "EUR"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, v => v.ErrorMessage!.Contains("From currency"));
    }

    [Fact]
    public void RateQueryRequest_ToTooShort_FailsValidation()
    {
        var request = new Api.Models.Rates.RateQueryRequest
        {
            From = "USD",
            To = "EU"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, v => v.ErrorMessage!.Contains("To currency"));
    }

    [Fact]
    public void RateQueryRequest_FromLowercase_FailsValidation()
    {
        var request = new Api.Models.Rates.RateQueryRequest
        {
            From = "usd",
            To = "EUR"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void RateQueryRequest_ToLowercase_FailsValidation()
    {
        var request = new Api.Models.Rates.RateQueryRequest
        {
            From = "USD",
            To = "eur"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void RateQueryRequest_MissingFrom_FailsValidation()
    {
        var request = new Api.Models.Rates.RateQueryRequest
        {
            From = null!,
            To = "EUR"
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void RateQueryRequest_MissingTo_FailsValidation()
    {
        var request = new Api.Models.Rates.RateQueryRequest
        {
            From = "USD",
            To = null!
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void RateQueryRequest_DefaultMode_IsLive()
    {
        var request = new Api.Models.Rates.RateQueryRequest
        {
            From = "USD",
            To = "EUR"
        };

        Assert.Equal("live", request.Mode);
    }

    [Fact]
    public void RateQueryRequest_SetSnapshotMode_ModeIsSnapshot()
    {
        var request = new Api.Models.Rates.RateQueryRequest
        {
            From = "USD",
            To = "EUR",
            Mode = "snapshot"
        };

        Assert.Equal("snapshot", request.Mode);
    }

    [Fact]
    public void RateQueryRequest_SetDate_DateIsSet()
    {
        var date = new DateOnly(2024, 1, 15);
        var request = new Api.Models.Rates.RateQueryRequest
        {
            From = "USD",
            To = "EUR",
            Date = date
        };

        Assert.Equal(date, request.Date);
    }

    [Fact]
    public void RateQueryRequest_NullDate_DateIsNull()
    {
        var request = new Api.Models.Rates.RateQueryRequest
        {
            From = "USD",
            To = "EUR",
            Date = null
        };

        Assert.Null(request.Date);
    }

    #endregion

    #region OpenRatesModel Tests

    [Fact]
    public void OpenRatesModel_SetProperties_ValuesAreCorrectlySet()
    {
        var model = new Api.Models.ApiResponses.OpenRatesModel
        {
            Date = "2024-01-15",
            Base = "USD",
            Rates = new Dictionary<string, decimal>
            {
                { "EUR", 0.85m },
                { "GBP", 0.75m }
            }
        };

        Assert.Equal("2024-01-15", model.Date);
        Assert.Equal("USD", model.Base);
        Assert.Equal(2, model.Rates.Count);
        Assert.Equal(0.85m, model.Rates["EUR"]);
    }

    [Fact]
    public void OpenRatesModel_DefaultRates_DictionaryIsEmpty()
    {
        var model = new Api.Models.ApiResponses.OpenRatesModel();

        Assert.NotNull(model.Rates);
        Assert.Empty(model.Rates);
    }

    [Fact]
    public void OpenRatesModel_NullDate_DateIsNull()
    {
        var model = new Api.Models.ApiResponses.OpenRatesModel
        {
            Date = null,
            Base = "USD"
        };

        Assert.Null(model.Date);
    }

    [Fact]
    public void OpenRatesModel_NullBase_BaseIsNull()
    {
        var model = new Api.Models.ApiResponses.OpenRatesModel
        {
            Date = "2024-01-15",
            Base = null
        };

        Assert.Null(model.Base);
    }

    [Fact]
    public void OpenRatesModel_AddRate_AddsSuccessfully()
    {
        var model = new Api.Models.ApiResponses.OpenRatesModel
        {
            Date = "2024-01-15",
            Base = "USD"
        };

        model.Rates["JPY"] = 150.5m;

        Assert.Single(model.Rates);
        Assert.Equal(150.5m, model.Rates["JPY"]);
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
