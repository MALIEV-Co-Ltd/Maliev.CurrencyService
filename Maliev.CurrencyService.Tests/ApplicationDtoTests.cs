using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Application.DTOs.Rates;
using Maliev.CurrencyService.Application.DTOs.Snapshots;
using Maliev.CurrencyService.Api.Models;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class MoreUnitTests
{
    #region Application DTO Tests

    public class CurrencyResponseTests
    {
        [Fact]
        public void CurrencyResponse_RequiredProperties_ShouldWork()
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

            Assert.Equal("USD", response.Code);
            Assert.Equal("$", response.Symbol);
            Assert.Equal("United States Dollar", response.Name);
            Assert.Equal(2, response.DecimalPlaces);
            Assert.True(response.IsActive);
            Assert.True(response.IsPrimary);
        }
    }

    public class ExchangeRateResponseTests
    {
        [Fact]
        public void ExchangeRateResponse_RequiredProperties_ShouldWork()
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
        public void ExchangeRateResponse_SnapshotMode_ShouldWork()
        {
            var response = new ExchangeRateResponse
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

            Assert.Equal("snapshot", response.Mode);
            Assert.NotNull(response.SnapshotDate);
        }

        [Fact]
        public void ExchangeRateResponse_TransitiveCalculation_ShouldWork()
        {
            var response = new ExchangeRateResponse
            {
                FromCurrency = "EUR",
                ToCurrency = "THB",
                Rate = 38.5m,
                Timestamp = DateTime.UtcNow,
                Source = "Transitive",
                IsTransitive = true,
                IntermediateCurrency = "USD",
                CalculationDetails = "EUR/USD × USD/THB",
                Mode = "live"
            };

            Assert.True(response.IsTransitive);
            Assert.Equal("USD", response.IntermediateCurrency);
            Assert.Equal("EUR/USD × USD/THB", response.CalculationDetails);
        }
    }

    public class SnapshotBatchRequestTests
    {
        [Fact]
        public void SnapshotBatchRequest_RequiredProperties_ShouldWork()
        {
            var request = new SnapshotBatchRequest
            {
                SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Source = "TestProvider",
                AutoPromote = true,
                Snapshots = new List<SnapshotEntry>
                {
                    new() { From = "USD", To = "EUR", Rate = 0.85m }
                }
            };

            Assert.Equal("TestProvider", request.Source);
            Assert.True(request.AutoPromote);
            Assert.Single(request.Snapshots);
        }

        [Fact]
        public void SnapshotEntry_RequiredProperties_ShouldWork()
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
    }

    public class UpdateRateRequestTests
    {
        [Fact]
        public void UpdateRateRequest_RequiredProperties_ShouldWork()
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
    }

    public class SnapshotBatchResponseTests
    {
        [Fact]
        public void SnapshotBatchResponse_RequiredProperties_ShouldWork()
        {
            var response = new SnapshotBatchResponse
            {
                BatchId = "test-batch-id",
                SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Source = "TestProvider",
                SuccessCount = 10,
                FailureCount = 2,
                Status = "completed",
                ProcessedAt = DateTime.UtcNow
            };

            Assert.Equal("test-batch-id", response.BatchId);
            Assert.Equal("TestProvider", response.Source);
            Assert.Equal(10, response.SuccessCount);
            Assert.Equal(2, response.FailureCount);
            Assert.Equal("completed", response.Status);
        }

        [Fact]
        public void SnapshotBatchResponse_WithErrors_ShouldWork()
        {
            var response = new SnapshotBatchResponse
            {
                BatchId = "test-batch-id",
                SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Source = "TestProvider",
                SuccessCount = 8,
                FailureCount = 2,
                Status = "partial",
                Errors = new Dictionary<string, string[]>
                {
                    { "USD:EUR", new[] { "Invalid rate" } }
                },
                ProcessedAt = DateTime.UtcNow
            };

            Assert.NotNull(response.Errors);
            Assert.True(response.Errors.ContainsKey("USD:EUR"));
        }
    }

    public class SnapshotAuditLogTests
    {
        [Fact]
        public void SnapshotAuditLog_RequiredProperties_ShouldWork()
        {
            var auditLog = new SnapshotAuditLog
            {
                BatchId = "test-batch-id",
                Timestamp = DateTime.UtcNow,
                RecordCount = 100,
                Source = "TestProvider",
                SubmittedBy = "Admin"
            };

            Assert.Equal("test-batch-id", auditLog.BatchId);
            Assert.Equal(100, auditLog.RecordCount);
            Assert.Equal("TestProvider", auditLog.Source);
            Assert.Equal("Admin", auditLog.SubmittedBy);
        }
    }

    #endregion

    #region API Models Tests

    public class ApiModelTests
    {
        [Fact]
        public void GetExchangeRequest_SetProperties_ShouldWorkCorrectly()
        {
            var request = new GetExchangeRateRequest
            {
                From = "USD",
                To = "EUR"
            };

            Assert.Equal("USD", request.From);
            Assert.Equal("EUR", request.To);
        }

        [Fact]
        public void RedisOptions_SetProperties_ShouldWorkCorrectly()
        {
            var options = new RedisOptions
            {
                ConnectionString = "localhost:6379",
                InstanceName = "CurrencyService",
                Database = 1,
                ConnectTimeout = 10,
                SyncTimeout = 3000,
                AbortOnConnectFail = true
            };

            Assert.Equal("localhost:6379", options.ConnectionString);
            Assert.Equal("CurrencyService", options.InstanceName);
            Assert.Equal(1, options.Database);
            Assert.Equal(10, options.ConnectTimeout);
            Assert.Equal(3000, options.SyncTimeout);
            Assert.True(options.AbortOnConnectFail);
        }

        [Fact]
        public void RedisOptions_DefaultValues_ShouldBeCorrect()
        {
            var options = new RedisOptions { ConnectionString = "test" };

            Assert.Equal(0, options.Database);
            Assert.Equal(5, options.ConnectTimeout);
            Assert.Equal(5000, options.SyncTimeout);
            Assert.False(options.AbortOnConnectFail);
        }

        [Fact]
        public void RedisOptions_SectionName_ShouldBeConstant()
        {
            Assert.Equal("Redis", RedisOptions.SectionName);
        }

        [Fact]
        public void ExchangeRateOptions_SetProperties_ShouldWorkCorrectly()
        {
            var options = new ExchangeRateOptions
            {
                CacheDurationMinutes = 60,
                RetryAttempts = 5,
                TimeoutSeconds = 60,
                FrankfurterApiUrl = "https://custom.api.url/",
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
            Assert.Equal("https://custom.api.url/", options.FrankfurterApiUrl);
            Assert.True(options.EnableDynamicPrioritization);
            Assert.Equal(20, options.MinRequestsForPrioritization);
        }

        [Fact]
        public void ExchangeRateOptions_DefaultValues_ShouldBeCorrect()
        {
            var options = new ExchangeRateOptions();

            Assert.Equal(30, options.CacheDurationMinutes);
            Assert.Equal(3, options.RetryAttempts);
            Assert.Equal(30, options.TimeoutSeconds);
            Assert.Equal("https://api.frankfurter.app/", options.FrankfurterApiUrl);
            Assert.False(options.EnableDynamicPrioritization);
            Assert.Equal(10, options.MinRequestsForPrioritization);
        }

        [Fact]
        public void ExchangeRateOptions_ProviderOrder_DefaultShouldContainProviders()
        {
            var options = new ExchangeRateOptions();

            Assert.Contains("Frankfurter", options.ProviderOrder);
            Assert.Contains("Fawazahmed", options.ProviderOrder);
        }

        [Fact]
        public void ExchangeRateOptions_SectionName_ShouldBeConstant()
        {
            Assert.Equal("ExchangeRate", ExchangeRateOptions.SectionName);
        }

        [Fact]
        public void ProviderMetrics_SetProperties_ShouldWorkCorrectly()
        {
            var metrics = new ProviderMetrics
            {
                ProviderName = "Frankfurter",
                TotalRequests = 100,
                SuccessfulRequests = 95,
                TotalResponseTimeMs = 15000,
                LastRequestAt = DateTime.UtcNow
            };

            Assert.Equal("Frankfurter", metrics.ProviderName);
            Assert.Equal(100, metrics.TotalRequests);
            Assert.Equal(95, metrics.SuccessfulRequests);
            Assert.Equal(15000, metrics.TotalResponseTimeMs);
        }

        [Fact]
        public void ProviderMetrics_CalculatedProperties_ShouldWork()
        {
            var metrics = new ProviderMetrics
            {
                ProviderName = "Frankfurter",
                TotalRequests = 100,
                SuccessfulRequests = 95,
                TotalResponseTimeMs = 15000
            };

            Assert.Equal(0.95, metrics.SuccessRate);
            Assert.Equal(0.05, metrics.ErrorRate);
            Assert.Equal(150, metrics.AverageResponseTimeMs);
        }

        [Fact]
        public void ProviderMetrics_ZeroRequests_ShouldHandleCorrectly()
        {
            var metrics = new ProviderMetrics
            {
                ProviderName = "Frankfurter",
                TotalRequests = 0,
                SuccessfulRequests = 0,
                TotalResponseTimeMs = 0
            };

            Assert.Equal(0, metrics.SuccessRate);
            Assert.Equal(0, metrics.ErrorRate);
            Assert.Equal(0, metrics.AverageResponseTimeMs);
        }
    }

    #endregion

    #region Request/Response Models

    public class ConvertCurrencyTests
    {
        [Fact]
        public void ConvertCurrencyRequest_SetProperties_ShouldWork()
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
        public void ConvertCurrencyResponse_SetProperties_ShouldWork()
        {
            var response = new ConvertCurrencyResponse
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
    }

    public class ApplicationDtosTests
    {
        [Fact]
        public void ApplicationCreateCurrencyRequest_SetProperties_ShouldWork()
        {
            var request = new Maliev.CurrencyService.Application.DTOs.Currencies.CreateCurrencyRequest
            {
                Code = "USD",
                Name = "United States Dollar",
                Symbol = "$",
                DecimalPlaces = 2,
                IsActive = true
            };

            Assert.Equal("USD", request.Code);
            Assert.Equal("United States Dollar", request.Name);
            Assert.Equal("$", request.Symbol);
            Assert.Equal(2, request.DecimalPlaces);
            Assert.True(request.IsActive);
        }

        [Fact]
        public void ApplicationCreateCurrencyRequest_DefaultValues_ShouldWork()
        {
            var request = new Maliev.CurrencyService.Application.DTOs.Currencies.CreateCurrencyRequest
            {
                Code = "THB",
                Name = "Thai Baht",
                Symbol = "฿"
            };

            Assert.Equal(2, request.DecimalPlaces);
            Assert.True(request.IsActive);
        }

        [Fact]
        public void ApplicationUpdateCurrencyRequest_SetProperties_ShouldWork()
        {
            var request = new Maliev.CurrencyService.Application.DTOs.Currencies.UpdateCurrencyRequest
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
    }

    #endregion
}
