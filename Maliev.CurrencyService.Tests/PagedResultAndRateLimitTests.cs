using System.ComponentModel.DataAnnotations;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.CurrencyService.Tests;

public class TestsFinal
{
    #region PagedResult Tests

    [Fact]
    public void PagedResult_TotalPages_CalculatesCorrectly()
    {
        var result = new Api.Models.PagedResult<string>
        {
            Items = new[] { "a", "b", "c" },
            TotalCount = 25,
            Page = 1,
            PageSize = 10
        };

        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void PagedResult_TotalPages_ZeroItems_ReturnsZero()
    {
        var result = new Api.Models.PagedResult<string>
        {
            Items = Array.Empty<string>(),
            TotalCount = 0,
            Page = 1,
            PageSize = 10
        };

        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void PagedResult_HasNextPage_WhenNotOnLastPage()
    {
        var result = new Api.Models.PagedResult<string>
        {
            Items = new[] { "a", "b" },
            TotalCount = 25,
            Page = 2,
            PageSize = 10
        };

        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void PagedResult_NoNextPage_WhenOnLastPage()
    {
        var result = new Api.Models.PagedResult<string>
        {
            Items = new[] { "a", "b", "c", "d", "e" },
            TotalCount = 25,
            Page = 3,
            PageSize = 10
        };

        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void PagedResult_HasPreviousPage_WhenNotOnFirstPage()
    {
        var result = new Api.Models.PagedResult<string>
        {
            Items = new[] { "a", "b" },
            TotalCount = 25,
            Page = 2,
            PageSize = 10
        };

        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public void PagedResult_NoPreviousPage_WhenOnFirstPage()
    {
        var result = new Api.Models.PagedResult<string>
        {
            Items = new[] { "a", "b" },
            TotalCount = 25,
            Page = 1,
            PageSize = 10
        };

        Assert.False(result.HasPreviousPage);
    }

    #endregion

    #region ConvertCurrencyRequest Validation Tests

    [Fact]
    public void ConvertCurrencyRequest_ValidRequest_PassesValidation()
    {
        var request = new ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 100.00m
        };

        var validationResults = ValidateModel(request);

        Assert.Empty(validationResults);
    }

    [Fact]
    public void ConvertCurrencyRequest_MissingFrom_FailsValidation()
    {
        var request = new ConvertCurrencyRequest
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
        var request = new ConvertCurrencyRequest
        {
            From = "USD",
            To = null!,
            Amount = 100.00m
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ConvertCurrencyRequest_InvalidFromLength_FailsValidation()
    {
        var request = new ConvertCurrencyRequest
        {
            From = "US",
            To = "EUR",
            Amount = 100.00m
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ConvertCurrencyRequest_NegativeAmount_FailsValidation()
    {
        var request = new ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = -10.00m
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void ConvertCurrencyRequest_ZeroAmount_FailsValidation()
    {
        var request = new ConvertCurrencyRequest
        {
            From = "USD",
            To = "EUR",
            Amount = 0m
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    #endregion

    #region SnapshotQueue Tests

    [Fact]
    public void SnapshotQueue_QueueBackgroundWorkItemAsync_EmptyBatchId_ThrowsArgumentNullException()
    {
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<SnapshotQueue>>();

        var queue = new SnapshotQueue(mockServiceProvider.Object, mockLogger.Object);

        Assert.Throws<ArgumentNullException>(() => queue.QueueBackgroundWorkItemAsync("").AsTask().GetAwaiter().GetResult());
    }

    [Fact]
    public void SnapshotQueue_QueueBackgroundWorkItemAsync_NullBatchId_ThrowsArgumentNullException()
    {
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockLogger = new Mock<ILogger<SnapshotQueue>>();

        var queue = new SnapshotQueue(mockServiceProvider.Object, mockLogger.Object);

        Assert.Throws<ArgumentNullException>(() => queue.QueueBackgroundWorkItemAsync(null!).AsTask().GetAwaiter().GetResult());
    }

    #endregion

    #region Helper Methods

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    #endregion
}
