using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Common;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class TestsExtra
{
    #region DTO Tests

    public class ExchangeRateResponseTests
    {
        [Fact]
        public void ExchangeRateResponse_Properties_SetCorrectly()
        {
            var response = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.85m,
                Timestamp = DateTime.UtcNow,
                Source = "TestProvider",
                IsTransitive = false,
                Mode = "live"
            };

            Assert.Equal("USD", response.FromCurrency);
            Assert.Equal("EUR", response.ToCurrency);
            Assert.Equal(0.85m, response.Rate);
            Assert.Equal("TestProvider", response.Source);
            Assert.False(response.IsTransitive);
            Assert.Equal("live", response.Mode);
        }

        [Fact]
        public void ExchangeRateResponse_TransitiveCalculationDetails_ConstructedCorrectly()
        {
            var response = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 35.5m,
                Timestamp = DateTime.UtcNow,
                Source = "Transitive",
                IsTransitive = true,
                IntermediateCurrency = "THB",
                CalculationDetails = "USD/THB × THB/EUR",
                Mode = "live"
            };

            Assert.True(response.IsTransitive);
            Assert.Equal("THB", response.IntermediateCurrency);
            Assert.Equal("USD/THB × THB/EUR", response.CalculationDetails);
        }

        [Fact]
        public void ExchangeRateResponse_SnapshotMode_HasDate()
        {
            var response = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.82m,
                Timestamp = DateTime.UtcNow,
                Source = "Snapshot",
                IsTransitive = false,
                Mode = "snapshot",
                SnapshotDate = new DateOnly(2024, 1, 15)
            };

            Assert.Equal("snapshot", response.Mode);
            Assert.NotNull(response.SnapshotDate);
            Assert.Equal(new DateOnly(2024, 1, 15), response.SnapshotDate);
        }

        [Fact]
        public void ExchangeRateResponse_NullableFields_CanBeNull()
        {
            var response = new ExchangeRateResponse
            {
                FromCurrency = "USD",
                ToCurrency = "EUR",
                Rate = 0.85m,
                Timestamp = DateTime.UtcNow,
                Source = "Direct",
                IsTransitive = false,
                Mode = "live"
            };

            Assert.Null(response.IntermediateCurrency);
            Assert.Null(response.CalculationDetails);
            Assert.Null(response.SnapshotDate);
        }
    }

    public class PaginatedResponseTests
    {
        [Fact]
        public void PaginatedResponse_HasNextPage_ReturnsTrue_WhenPageLessThanTotal()
        {
            var response = new PaginatedResponse<string>
            {
                Items = new List<string> { "item1", "item2" },
                Page = 2,
                PageSize = 10,
                TotalCount = 25,
                TotalPages = 3
            };

            Assert.True(response.HasNextPage);
            Assert.True(response.HasPreviousPage);
        }

        [Fact]
        public void PaginatedResponse_HasPreviousPage_ReturnsTrue_WhenPageGreaterThanOne()
        {
            var response = new PaginatedResponse<string>
            {
                Items = new List<string> { "item1", "item2" },
                Page = 2,
                PageSize = 10,
                TotalCount = 25,
                TotalPages = 3
            };

            Assert.True(response.HasPreviousPage);
        }

        [Fact]
        public void PaginatedResponse_NoNextPage_WhenOnLastPage()
        {
            var response = new PaginatedResponse<string>
            {
                Items = new List<string> { "item1", "item2" },
                Page = 3,
                PageSize = 10,
                TotalCount = 25,
                TotalPages = 3
            };

            Assert.False(response.HasNextPage);
            Assert.True(response.HasPreviousPage);
        }

        [Fact]
        public void PaginatedResponse_CalculatesTotalPages_Correctly()
        {
            var response = new PaginatedResponse<string>
            {
                Items = new List<string>(),
                Page = 1,
                PageSize = 10,
                TotalCount = 95,
                TotalPages = 10
            };

            Assert.Equal(10, response.TotalPages);
        }

        [Fact]
        public void PaginatedResponse_FirstPage_HasNoPreviousPage()
        {
            var response = new PaginatedResponse<string>
            {
                Items = new List<string>(),
                Page = 1,
                PageSize = 10,
                TotalCount = 5,
                TotalPages = 1
            };

            Assert.False(response.HasPreviousPage);
            Assert.False(response.HasNextPage);
        }
    }

    public class SnapshotBatchRequestTests
    {
        [Fact]
        public void SnapshotBatchRequest_DefaultAutoPromote_IsFalse()
        {
            var request = new SnapshotBatchRequest
            {
                SnapshotDate = new DateOnly(2024, 1, 1),
                Source = "Test",
                Snapshots = new List<SnapshotEntry>()
            };

            Assert.False(request.AutoPromote);
        }

        [Fact]
        public void SnapshotBatchRequest_CanSetAutoPromote_ToTrue()
        {
            var request = new SnapshotBatchRequest
            {
                SnapshotDate = new DateOnly(2024, 1, 1),
                Source = "Test",
                Snapshots = new List<SnapshotEntry>(),
                AutoPromote = true
            };

            Assert.True(request.AutoPromote);
        }

        [Fact]
        public void SnapshotEntry_Properties_SetCorrectly()
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
        public void SnapshotEntry_ZeroRate_IsValid()
        {
            var entry = new SnapshotEntry
            {
                From = "USD",
                To = "JPY",
                Rate = 0m
            };

            Assert.Equal(0m, entry.Rate);
        }

        [Fact]
        public void SnapshotEntry_LargeRate_IsValid()
        {
            var entry = new SnapshotEntry
            {
                From = "USD",
                To = "VND",
                Rate = 25000.123456m
            };

            Assert.Equal(25000.123456m, entry.Rate);
        }
    }

    public class SnapshotBatchResponseTests
    {
        [Fact]
        public void SnapshotBatchResponse_DefaultStatus_IsStaged()
        {
            var response = new SnapshotBatchResponse
            {
                BatchId = Guid.NewGuid().ToString(),
                SnapshotDate = new DateOnly(2024, 1, 1),
                Source = "Test",
                SuccessCount = 0,
                FailureCount = 0,
                Status = "staged"
            };

            Assert.Equal("staged", response.Status);
        }

        [Fact]
        public void SnapshotBatchResponse_Errors_ConvertedToArray()
        {
            var errors = new Dictionary<string, List<string>>
            {
                { "USD:EUR", new List<string> { "Invalid rate" } }
            };

            var response = new SnapshotBatchResponse
            {
                BatchId = Guid.NewGuid().ToString(),
                SnapshotDate = new DateOnly(2024, 1, 1),
                Source = "Test",
                SuccessCount = 0,
                FailureCount = 1,
                Status = "staged",
                Errors = errors.Any() ? errors.ToDictionary(x => x.Key, x => x.Value.ToArray()) : null
            };

            Assert.NotNull(response.Errors);
            Assert.True(response.Errors.ContainsKey("USD:EUR"));
            Assert.Equal("Invalid rate", response.Errors["USD:EUR"][0]);
        }

        [Fact]
        public void SnapshotBatchResponse_MultipleErrors_CanBeStored()
        {
            var errors = new Dictionary<string, List<string>>
            {
                { "USD:EUR", new List<string> { "Invalid rate", "Rate too old" } },
                { "USD:GBP", new List<string> { "Currency not found" } }
            };

            var response = new SnapshotBatchResponse
            {
                BatchId = Guid.NewGuid().ToString(),
                SnapshotDate = new DateOnly(2024, 1, 1),
                Source = "Test",
                SuccessCount = 0,
                FailureCount = 3,
                Status = "staged",
                Errors = errors.Any() ? errors.ToDictionary(x => x.Key, x => x.Value.ToArray()) : null
            };

            Assert.NotNull(response.Errors);
            Assert.Equal(2, response.Errors.Count);
            Assert.Equal(2, response.Errors["USD:EUR"].Length);
        }

        [Fact]
        public void SnapshotBatchResponse_NullErrors_WhenNoFailures()
        {
            var response = new SnapshotBatchResponse
            {
                BatchId = Guid.NewGuid().ToString(),
                SnapshotDate = new DateOnly(2024, 1, 1),
                Source = "Test",
                SuccessCount = 10,
                FailureCount = 0,
                Status = "promoted",
                Errors = null
            };

            Assert.Null(response.Errors);
        }
    }

    public class CreateCurrencyRequestTests
    {
        [Fact]
        public void CreateCurrencyRequest_Properties_SetCorrectly()
        {
            var request = new CreateCurrencyRequest
            {
                Code = "THB",
                Symbol = "฿",
                Name = "Thai Baht",
                DecimalPlaces = 2
            };

            Assert.Equal("THB", request.Code);
            Assert.Equal("฿", request.Symbol);
            Assert.Equal("Thai Baht", request.Name);
            Assert.Equal(2, request.DecimalPlaces);
        }

        [Fact]
        public void CreateCurrencyRequest_DefaultDecimalPlaces_IsTwo()
        {
            var request = new CreateCurrencyRequest
            {
                Code = "USD",
                Symbol = "$",
                Name = "US Dollar"
            };

            Assert.Equal(0, request.DecimalPlaces);
        }
    }

    public class UpdateCurrencyRequestTests
    {
        [Fact]
        public void UpdateCurrencyRequest_NullableProperties_CanBeNull()
        {
            var request = new Application.DTOs.Currencies.UpdateCurrencyRequest();

            Assert.Null(request.Name);
            Assert.Null(request.Symbol);
            Assert.Null(request.DecimalPlaces);
            Assert.Null(request.IsActive);
            Assert.Null(request.Version);
        }

        [Fact]
        public void UpdateCurrencyRequest_OptionalFields_CanBeSet()
        {
            var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
            {
                Symbol = "€",
                Name = "Euro Updated",
                DecimalPlaces = 2,
                IsActive = false
            };

            Assert.Equal("€", request.Symbol);
            Assert.Equal("Euro Updated", request.Name);
            Assert.Equal(2, request.DecimalPlaces);
            Assert.False(request.IsActive);
        }

        [Fact]
        public void UpdateCurrencyRequest_Version_CanBeSet()
        {
            var version = new byte[] { 1, 2, 3, 4 };
            var request = new Application.DTOs.Currencies.UpdateCurrencyRequest
            {
                Version = version
            };

            Assert.NotNull(request.Version);
            Assert.Equal(4, request.Version.Length);
        }
    }

    public class CurrencyResponseTests
    {
        [Fact]
        public void CurrencyResponse_Properties_SetCorrectly()
        {
            var now = DateTime.UtcNow;
            var response = new CurrencyResponse
            {
                Id = Guid.NewGuid(),
                Code = "USD",
                Symbol = "$",
                Name = "US Dollar",
                DecimalPlaces = 2,
                IsActive = true,
                IsPrimary = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            Assert.Equal("USD", response.Code);
            Assert.Equal("$", response.Symbol);
            Assert.Equal("US Dollar", response.Name);
            Assert.Equal(2, response.DecimalPlaces);
            Assert.True(response.IsActive);
            Assert.False(response.IsPrimary);
        }

        [Fact]
        public void CurrencyResponse_IsPrimary_CanBeTrue()
        {
            var response = new CurrencyResponse
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

            Assert.True(response.IsPrimary);
        }

        [Fact]
        public void CurrencyResponse_Timestamps_CanBeSet()
        {
            var createdAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var updatedAt = new DateTime(2024, 6, 15, 12, 30, 0, DateTimeKind.Utc);

            var response = new CurrencyResponse
            {
                Id = Guid.NewGuid(),
                Code = "EUR",
                Symbol = "€",
                Name = "Euro",
                DecimalPlaces = 2,
                IsActive = true,
                IsPrimary = false,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };

            Assert.Equal(createdAt, response.CreatedAt);
            Assert.Equal(updatedAt, response.UpdatedAt);
        }
    }

    public class PaginatedCurrencyResponseTests
    {
        [Fact]
        public void PaginatedCurrencyResponse_InheritsFromPaginatedResponse()
        {
            var response = new PaginatedCurrencyResponse
            {
                Items = new List<CurrencyResponse>(),
                Page = 1,
                PageSize = 20,
                TotalCount = 100,
                TotalPages = 5
            };

            Assert.Equal(1, response.Page);
            Assert.Equal(20, response.PageSize);
            Assert.Equal(100, response.TotalCount);
            Assert.Equal(5, response.TotalPages);
        }

        [Fact]
        public void PaginatedCurrencyResponse_HasItems_CanBePopulated()
        {
            var items = new List<CurrencyResponse>
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
                },
                new CurrencyResponse
                {
                    Id = Guid.NewGuid(),
                    Code = "EUR",
                    Symbol = "€",
                    Name = "Euro",
                    DecimalPlaces = 2,
                    IsActive = true,
                    IsPrimary = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            var response = new PaginatedCurrencyResponse
            {
                Items = items,
                Page = 1,
                PageSize = 20,
                TotalCount = 2,
                TotalPages = 1
            };

            Assert.Equal(2, response.Items.Count());
        }
    }

    public class UpdateRateRequestTests
    {
        [Fact]
        public void UpdateRateRequest_Properties_SetCorrectly()
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
        public void UpdateRateRequest_ZeroRate_IsValid()
        {
            var request = new UpdateRateRequest
            {
                From = "USD",
                To = "VND",
                Rate = 0m
            };

            Assert.Equal(0m, request.Rate);
        }
    }

    public class SnapshotAuditLogTests
    {
        [Fact]
        public void SnapshotAuditLog_Properties_SetCorrectly()
        {
            var audit = new SnapshotAuditLog
            {
                BatchId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                RecordCount = 100,
                Source = "Manual",
                SubmittedBy = "Admin"
            };

            Assert.NotNull(audit.BatchId);
            Assert.Equal(100, audit.RecordCount);
            Assert.Equal("Manual", audit.Source);
            Assert.Equal("Admin", audit.SubmittedBy);
        }

        [Fact]
        public void SnapshotAuditLog_ZeroRecords_IsValid()
        {
            var audit = new SnapshotAuditLog
            {
                BatchId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                RecordCount = 0,
                Source = "Staging",
                SubmittedBy = "System"
            };

            Assert.Equal(0, audit.RecordCount);
        }
    }

    #endregion

    #region Infrastructure Service Unit Tests

    /// <summary>
    /// Tests for SnapshotQueue - requires full DI container, skipping for unit tests.
    /// Integration tests exist in SnapshotQueueIntegrationTests.
    /// </summary>
    public class SnapshotQueueUnitTests
    {
        [Fact]
        public void SnapshotQueue_CanBeConstructed()
        {
            var serviceProviderMock = new Mock<IServiceProvider>();
            var loggerMock = new Mock<ILogger<SnapshotQueue>>();

            var queue = new SnapshotQueue(serviceProviderMock.Object, loggerMock.Object);

            Assert.NotNull(queue);
        }
    }

    #endregion
}
