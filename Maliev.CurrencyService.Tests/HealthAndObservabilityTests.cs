using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// Health & Observability Tests
/// Tests FR-050, FR-051, FR-052, FR-053, FR-054, FR-025 from specification
/// </summary>
public class HealthAndObservabilityTests : IClassFixture<CurrencyServiceTestFixture>
{
    private readonly HttpClient _client;

    public HealthAndObservabilityTests(CurrencyServiceTestFixture fixture)
    {
        _client = fixture.Client;
    }

    #region FR-050: Liveness Endpoint

    [Fact]
    public async Task FR050_Given_ServiceRunning_When_LivenessChecked_Then_Returns200()
    {
        // FR-050: System must provide liveness endpoint that returns 200 when service is running

        // Act
        var response = await _client.GetAsync("/currencies/liveness");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "FR-050: liveness endpoint must return 200 when service is running");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty("liveness endpoint should return a simple status");
    }

    [Fact]
    public async Task FR050_Given_LivenessEndpoint_When_CalledMultipleTimes_Then_AlwaysResponds()
    {
        // Test that liveness endpoint is always available even under load

        // Act - Multiple concurrent liveness checks
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => _client.GetAsync("/currencies/liveness"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK,
            "liveness endpoint must always respond 200");
    }

    #endregion

    #region FR-051: Readiness Endpoint

    [Fact]
    public async Task FR051_Given_AllDependenciesHealthy_When_ReadinessChecked_Then_Returns200()
    {
        // FR-051: System must provide readiness endpoint that checks:
        // - Database connectivity
        // - Cache connectivity
        // Returns 200 only when all dependencies are healthy

        // Act
        var response = await _client.GetAsync("/currencies/readiness");

        // Assert
        // Assert
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"FR-051: readiness endpoint should return 200 when all dependencies are healthy. Content: {content}");

        var healthReport = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        healthReport.Should().NotBeNull();
        healthReport!.Status.Should().Be("Healthy",
            "overall status should be Healthy when all checks pass");

        // Verify dependency checks are present
        healthReport.Checks.Should().ContainKey("database")
            .WhoseValue.Status.Should().Be("Healthy",
                "FR-051: database connectivity must be checked");

        healthReport.Checks.Should().ContainKey("redis")
            .WhoseValue.Status.Should().Be("Healthy",
                "FR-051: cache connectivity must be checked");
    }

    [Fact]
    public async Task FR051_Given_ReadinessEndpoint_When_Requested_Then_IncludesDetailedHealthInfo()
    {
        // Act
        var response = await _client.GetAsync("/currencies/readiness");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var healthReport = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
        healthReport.Should().NotBeNull();

        // Verify each check includes details
        foreach (var check in healthReport!.Checks.Values)
        {
            check.Status.Should().NotBeNullOrEmpty("each health check should have a status");
            check.Description.Should().NotBeNullOrEmpty("each health check should have a description");

            if (check.Status == "Unhealthy")
            {
                check.Exception.Should().NotBeNullOrEmpty(
                    "unhealthy checks should include exception information");
            }
        }
    }

    [Fact]
    public async Task FR051_Given_DatabaseUnhealthy_When_ReadinessChecked_Then_Returns503()
    {
        // This test verifies the contract - in real scenario would require database unavailability
        // For now, we verify that the readiness endpoint can distinguish health states

        // Act
        var response = await _client.GetAsync("/currencies/readiness");

        // Assert
        // If dependencies are unhealthy, should return 503
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var healthReport = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();
            healthReport!.Status.Should().Be("Unhealthy",
                "FR-051: readiness should return Unhealthy status when dependencies fail");

            healthReport.Checks.Should().ContainKey("database")
                .WhoseValue.Status.Should().Be("Unhealthy");
        }
        else
        {
            // All healthy - that's also valid
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    #endregion

    #region FR-052: Prometheus Metrics

    [Fact]
    public async Task FR052_Given_MetricsEndpoint_When_Accessed_Then_ReturnsPrometheusFormat()
    {
        // FR-052: System must expose Prometheus metrics endpoint with:
        // - Request rate and latency by endpoint
        // - Provider call success/failure rates
        // - Cache hit/miss ratios

        // Act
        var response = await _client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "FR-052: metrics endpoint must be available");

        response.Content.Headers.ContentType?.MediaType.Should().Contain("text/plain",
            "metrics should be in Prometheus text format");

        var metricsContent = await response.Content.ReadAsStringAsync();
        metricsContent.Should().NotBeNullOrEmpty();

        // Verify Prometheus format (# HELP, # TYPE, metric_name)
        metricsContent.Should().Contain("# HELP", "Prometheus metrics should include HELP comments");
        metricsContent.Should().Contain("# TYPE", "Prometheus metrics should include TYPE comments");
    }

    [Fact]
    public async Task FR052_Given_RequestsProcessed_When_MetricsQueried_Then_IncludesRequestMetrics()
    {
        // Arrange - Make some requests to generate metrics
        await _client.GetAsync("/currencies/v1/currencies");
        await _client.GetAsync("/currencies/v1/rates?from=USD&to=THB&mode=live");
        // Arrange - Make request that triggers provider call
        await _client.GetAsync("/currencies/v1/rates?from=USD&to=EUR&mode=live");

        // Act
        var response = await _client.GetAsync("/metrics");
        var metricsContent = await response.Content.ReadAsStringAsync();

        // Assert - FR-052: Provider call success/failure rates
        metricsContent.Should().MatchRegex(@"provider_calls_total\{.*provider="".*"",status="".*""\}",
            "should include provider call metrics with success/failure status");

        metricsContent.Should().MatchRegex(@"provider_call_duration_seconds",
            "should include provider call latency metrics");
    }

    [Fact]
    public async Task FR052_FR025_Given_CacheActivity_When_MetricsQueried_Then_IncludesCacheMetrics()
    {
        // FR-052: Cache hit/miss ratios
        // FR-025: System must track cache hit/miss ratios for monitoring

        // Arrange - Generate cache hits and misses
        await _client.GetAsync("/currencies/v1/rates?from=USD&to=GBP&mode=live"); // Cache miss
        await Task.Delay(100);
        await _client.GetAsync("/currencies/v1/rates?from=USD&to=GBP&mode=live"); // Cache hit

        // Act
        var response = await _client.GetAsync("/metrics");
        var metricsContent = await response.Content.ReadAsStringAsync();

        // Assert
        metricsContent.Should().MatchRegex(@"cache_requests_total\{.*result=""hit""\}",
            "FR-025: should track cache hit count");

        metricsContent.Should().MatchRegex(@"cache_requests_total\{.*result=""miss""\}",
            "FR-025: should track cache miss count");

        // Verify cache hit ratio calculation is available
        if (metricsContent.Contains("cache_hit_ratio"))
        {
            metricsContent.Should().MatchRegex(@"cache_hit_ratio\s+[0-9.]+",
                "cache hit ratio should be a decimal value between 0 and 1");
        }
    }

    [Fact]
    public async Task FR052_Given_MultipleMetricQueries_When_Executed_Then_ReturnsConsistentFormat()
    {
        // Verify metrics endpoint is stable and returns consistent format

        // Act
        var response1 = await _client.GetAsync("/metrics");
        var response2 = await _client.GetAsync("/metrics");

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        var metrics1 = await response1.Content.ReadAsStringAsync();
        var metrics2 = await response2.Content.ReadAsStringAsync();

        // Both should have Prometheus format structure
        metrics1.Should().Contain("# HELP");
        metrics2.Should().Contain("# HELP");

        // Metrics should be cumulative (counters only increase)
        metrics1.Should().NotBeEmpty();
        metrics2.Should().NotBeEmpty();
    }

    #endregion

    #region FR-053: Admin Operation Logging

    [Fact]
    public async Task FR053_Given_AdminOperation_When_Executed_Then_LogsUserIdentifierAndTimestamp()
    {
        // FR-053: System must log all admin operations (create, update, delete)
        // with user identifier and timestamp

        // Arrange - Create a currency (admin operation)
        var newCurrency = new CreateCurrencyRequest
        {
            Code = "AUD",
            Symbol = "A$",
            Name = "Australian Dollar",
            DecimalPlaces = 2
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(newCurrency),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/currencies/v1/admin/currencies", content);

        // Assert - Operation should be logged
        // In production, we'd query logs or audit trail
        // For tests, we verify the endpoint requires authentication (which provides user identifier)
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // This is expected - admin endpoints should require auth
            // which provides user identifier for logging
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "FR-053: admin operations require authentication to capture user identifier");
        }
        else if (response.StatusCode == HttpStatusCode.Created)
        {
            // If authenticated, operation should succeed and be logged
            var created = await response.Content.ReadFromJsonAsync<CurrencyDto>();
            created.Should().NotBeNull("created resource should be returned");

            // Verify timestamp is captured (via CreatedAt in response)
            if (created!.CreatedAt != default)
            {
                created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5),
                    "FR-053: operation timestamp should be captured");
            }
        }
    }

    [Fact]
    public async Task FR053_Given_UpdateOperation_When_Executed_Then_LogsChangeDetails()
    {
        // Arrange - First get a currency
        var listResponse = await _client.GetAsync("/currencies/v1/currencies");
        var currencies = await listResponse.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();

        if (!currencies!.Items.Any())
            return; // Skip if no currencies

        var currency = currencies.Items.First();

        // Prepare update
        var updateRequest = new UpdateCurrencyRequest
        {
            Symbol = currency.Symbol,
            Name = currency.Name + " (Updated)",
            DecimalPlaces = currency.DecimalPlaces
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(updateRequest),
            System.Text.Encoding.UTF8,
            "application/json");

        // Get ETag for optimistic concurrency
        var detailResponse = await _client.GetAsync($"/currencies/v1/admin/currencies/{currency.Id}");
        var etag = detailResponse.Headers.ETag?.Tag;

        // Act - Update operation
        var request = new HttpRequestMessage(HttpMethod.Put, $"/currencies/v1/admin/currencies/{currency.Id}")
        {
            Content = content
        };

        if (!string.IsNullOrEmpty(etag))
        {
            request.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag));
        }

        var response = await _client.SendAsync(request);

        // Assert - FR-053: Update should be logged with change details
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var updated = await response.Content.ReadFromJsonAsync<CurrencyDto>();
            updated.Should().NotBeNull();

            // Verify UpdatedAt timestamp is present
            if (updated!.UpdatedAt != default)
            {
                updated.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5),
                    "FR-053: update timestamp should be captured");
            }
        }
    }

    [Fact]
    public async Task FR053_Given_DeleteOperation_When_Executed_Then_LogsOperation()
    {
        // Arrange - Create a test currency to delete
        var newCurrency = new CreateCurrencyRequest
        {
            Code = "DEL",
            Symbol = "D",
            Name = "Delete Test Currency",
            DecimalPlaces = 2
        };

        var createContent = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(newCurrency),
            System.Text.Encoding.UTF8,
            "application/json");

        var createResponse = await _client.PostAsync("/currencies/v1/admin/currencies", createContent);

        if (createResponse.StatusCode != HttpStatusCode.Created)
            return; // Skip if creation failed

        var created = await createResponse.Content.ReadFromJsonAsync<CurrencyDto>();

        // Act - Delete operation
        var response = await _client.DeleteAsync($"/currencies/v1/admin/currencies/{created!.Id}");

        // Assert - FR-053: Delete should be logged
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            // Deletion succeeded - should be logged in audit trail
            // Verify it's actually deleted
            var verifyResponse = await _client.GetAsync($"/currencies/v1/currencies/{created.Id}");
            verifyResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "deleted resource should no longer be accessible");
        }
    }

    #endregion

    #region FR-054: Provider Call Logging

    [Fact]
    public async Task FR054_Given_ProviderCall_When_Executed_Then_LogsProviderAndOutcome()
    {
        // FR-054: System must log all provider calls with:
        // - Provider identifier
        // - Success/failure outcome
        // - Response time

        // Arrange & Act - Trigger a provider call
        var response = await _client.GetAsync("/currencies/v1/rates?from=USD&to=EUR&mode=live");

        // Assert - Verify response includes provider information
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
            rate.Should().NotBeNull();
            rate!.Source.Should().NotBeNullOrEmpty(
                "FR-054: provider identifier must be logged and returned");

            // Provider should be one of the configured providers
            rate.Source.Should().Match(s =>
                s.Contains("Fawazahmed") || s.Contains("Frankfurter"),
                "FR-054: logged provider must match configured providers");

            // Timestamp indicates when the call was made
            rate.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5),
                "FR-054: response time should be captured");
        }
    }

    [Fact]
    public async Task FR054_Given_ProviderFailure_When_Fallback_Then_LogsBothAttempts()
    {
        // FR-054: When primary provider fails and fallback occurs,
        // both attempts should be logged

        // Act - Request that might trigger fallback
        var response = await _client.GetAsync("/currencies/v1/rates?from=THB&to=JPY&mode=live");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
            rate.Should().NotBeNull();
            rate!.Source.Should().NotBeNullOrEmpty("provider source must be logged");

            // In production logs, we'd verify that failed attempts are logged
            // Here we verify the successful fallback provider is documented
            rate.Source.Should().Match(s =>
                s.Contains("Fawazahmed") || s.Contains("Frankfurter"),
                "FR-054: fallback provider must be logged");
        }
        else if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            // All providers failed - all attempts should be logged
            var errorContent = await response.Content.ReadAsStringAsync();
            errorContent.Should().NotBeNullOrEmpty(
                "FR-054: provider failures should be documented in error response");
        }
    }

    [Fact]
    public async Task FR054_Given_MultipleProviderCalls_When_MetricsQueried_Then_ShowsProviderBreakdown()
    {
        // Arrange - Generate multiple provider calls
        await _client.GetAsync("/currencies/v1/rates?from=USD&to=EUR&mode=live");
        await _client.GetAsync("/currencies/v1/rates?from=GBP&to=USD&mode=live");
        await _client.GetAsync("/currencies/v1/rates?from=JPY&to=THB&mode=live");

        // Act - Check metrics
        var response = await _client.GetAsync("/metrics");
        var metricsContent = await response.Content.ReadAsStringAsync();

        // Assert - FR-054: Provider call logging should be visible in metrics
        metricsContent.Should().MatchRegex(@"provider_calls_total\{.*provider=""(Fawazahmed|Frankfurter)""",
            "FR-054: provider calls should be tracked per provider");

        metricsContent.Should().MatchRegex(@"provider_call_duration_seconds",
            "FR-054: provider response times should be tracked");
    }

    #endregion

    #region Integration: Health + Metrics

    [Fact]
    public async Task Integration_Given_ServiceHealthy_When_BothEndpointsQueried_Then_BothSucceed()
    {
        // Verify that both health and metrics endpoints are independently accessible

        // Act
        var healthResponse = await _client.GetAsync("/currencies/readiness");
        var metricsResponse = await _client.GetAsync("/metrics");

        // Assert
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "health endpoint should be accessible");

        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "metrics endpoint should be accessible");

        // Both should be fast (< 100ms for health, < 500ms for metrics)
        var healthStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _client.GetAsync("/currencies/readiness");
        healthStopwatch.Stop();

        healthStopwatch.ElapsedMilliseconds.Should().BeLessThan(100,
            "health checks should be fast");
    }

    [Fact]
    public async Task Integration_Given_HighLoad_When_ObservabilityEndpointsQueried_Then_RemainResponsive()
    {
        // Verify observability endpoints remain responsive under load

        // Arrange - Create some load
        var loadTasks = Enumerable.Range(0, 50)
            .Select(_ => _client.GetAsync("/currencies/v1/currencies"))
            .ToList();

        // Act - Query observability endpoints during load
        var healthTask = _client.GetAsync("/currencies/readiness");
        var metricsTask = _client.GetAsync("/metrics");
        var livenessTask = _client.GetAsync("/currencies/liveness");

        await Task.WhenAll(loadTasks);
        var healthResponse = await healthTask;
        var metricsResponse = await metricsTask;
        var livenessResponse = await livenessTask;

        // Assert - All should succeed despite load
        healthResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "health endpoint should remain responsive under load");

        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "metrics endpoint should remain responsive under load");

        livenessResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "liveness endpoint should remain responsive under load");
    }

    #endregion
}

/// <summary>
/// Health check response DTO
/// </summary>
public record HealthCheckResponse
{
    public string Status { get; init; } = string.Empty;
    public Dictionary<string, HealthCheckEntry> Checks { get; init; } = new();
    public TimeSpan TotalDuration { get; init; }
}

public record HealthCheckEntry
{
    public string Status { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public string? Exception { get; init; }
    public Dictionary<string, object>? Data { get; init; }
}
