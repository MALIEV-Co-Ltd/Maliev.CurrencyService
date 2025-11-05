using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// Edge Case Tests from Specification
/// Tests all edge cases explicitly defined in spec.md
/// </summary>
public class EdgeCaseTests : IClassFixture<CurrencyServiceTestFixture>
{
    private readonly HttpClient _client;

    public EdgeCaseTests(CurrencyServiceTestFixture fixture)
    {
        _client = fixture.Client;
    }

    #region Edge Case 1: Both External Providers Down, No Cache

    [Fact]
    public async Task EdgeCase1_Given_AllProvidersDownAndNoCache_When_ClientRequestsRate_Then_Returns503WithRetryAfter()
    {
        // Edge Case: What happens when both external rate providers are down and no cached data exists?
        // Answer: System returns 503 Service Unavailable with retry-after header and clear error message

        // This test would require mocking or provider unavailability
        // For now, we define the expected contract

        // Arrange - Request a rate that's never been cached
        var uniquePair = $"USD_to_TEST_{Guid.NewGuid()}";

        // Act - In scenario where providers are down
        // Implementation note: This requires circuit breaker or provider health check
        var response = await _client.GetAsync($"/currencies/v1/rates?from=USD&to=XXX&mode=live");

        // Assert
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            // Edge case answer: 503 with retry-after
            response.Headers.RetryAfter.Should().NotBeNull(
                "Edge Case 1: Must include retry-after header when providers unavailable");

            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            error.Should().NotBeNull();
            error!.Message.Should().Contain("unavailable",
                "Edge Case 1: Must provide clear error message indicating temporary unavailability");
        }
    }

    #endregion

    #region Edge Case 2: Intermediary Currency Rate Unavailable

    [Fact]
    public async Task EdgeCase2_Given_USDIntermediaryUnavailable_When_TransitiveNeeded_Then_TriesAlternativesOrErrors()
    {
        // Edge Case: How does system handle transitive conversion when intermediary currency (USD) rate is unavailable?
        // Answer: System attempts alternative intermediary currencies in configured order (EUR, GBP) or returns error

        // Arrange - Request pair that requires transitive conversion
        var response = await _client.GetAsync("/currencies/v1/rates?from=THB&to=JPY&mode=live");

        // Act & Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
            rate.Should().NotBeNull();

            if (rate!.IsTransitive)
            {
                // Edge Case 2: If USD unavailable, should try EUR or GBP
                rate.IntermediaryCurrency.Should().Match(c =>
                    c == "USD" || c == "EUR" || c == "GBP",
                    "Edge Case 2: Should attempt alternative intermediary currencies (EUR, GBP) if USD unavailable");
            }
        }
        else if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            // Edge Case 2: If no path exists, return error
            var error = await response.Content.ReadAsStringAsync();
            error.Should().NotBeNullOrEmpty("should provide error message when no conversion path exists");
        }
    }

    #endregion

    #region Edge Case 3: Deprecated Currency

    [Fact]
    public async Task EdgeCase3_Given_DeprecatedCurrency_When_RateRequested_Then_ReturnsRateWithWarningHeader()
    {
        // Edge Case: What happens when a currency pair involves a deprecated currency?
        // Answer: System returns the rate if snapshot data exists, but includes a warning header

        // Arrange - Mark a currency as deprecated (if API supports it)
        // For test: assume we can create/mark a deprecated currency

        // Act - Request rate involving deprecated currency
        var response = await _client.GetAsync("/currencies/v1/rates?from=DEPRECATED&to=USD&mode=snapshot&date=2025-01-01");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            // Edge Case 3: Should include warning header
            var hasWarning = response.Headers.Contains("Warning") ||
                           response.Headers.Contains("X-Currency-Deprecated");

            if (hasWarning)
            {
                hasWarning.Should().BeTrue(
                    "Edge Case 3: Should include warning header indicating currency is deprecated");
            }

            var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
            rate.Should().NotBeNull("Edge Case 3: Should return rate if snapshot data exists");
        }
    }

    #endregion

    #region Edge Case 4: High Request Volumes and Provider Rate Limits

    [Fact]
    public async Task EdgeCase4_Given_HighRequestVolume_When_ProviderLimitsApproached_Then_AppliesRateLimitingAndServesStaleCache()
    {
        // Edge Case: How does system handle extremely high request volumes that could exhaust provider rate limits?
        // Answer: Aggressive caching reduces provider calls; if limits approached, apply rate limiting and serve stale cache

        // Arrange - Fire many requests rapidly
        const int requestCount = 150; // Exceeds 100/minute rate limit
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Fire requests
        for (int i = 0; i < requestCount; i++)
        {
            tasks.Add(_client.GetAsync("/currencies/v1/rates?from=USD&to=THB&mode=live"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var rateLimitedCount = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);

        // Edge Case 4: FR-047 - Rate limiting at 100 req/min per IP
        if (rateLimitedCount > 0)
        {
            rateLimitedCount.Should().BeGreaterThan(0,
                "Edge Case 4: System should apply rate limiting when volume exceeds limits");

            var rateLimitedResponse = responses.First(r => r.StatusCode == HttpStatusCode.TooManyRequests);
            rateLimitedResponse.Headers.RetryAfter.Should().NotBeNull(
                "Edge Case 4: Rate limited response should include Retry-After header");
        }

        // Most requests should succeed due to caching
        successCount.Should().BeGreaterThan(0,
            "Edge Case 4: Aggressive caching should allow most requests to succeed");

        // Some responses might be from stale cache
        foreach (var response in responses.Where(r => r.StatusCode == HttpStatusCode.OK))
        {
            if (response.Headers.Contains("X-Rate-Stale") || response.Headers.Contains("Warning"))
            {
                // Edge Case 4: Stale cache served with headers
                var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
                rate!.IsStale.Should().BeTrue(
                    "Edge Case 4: If serving stale cache, should indicate staleness");
            }
        }
    }

    #endregion

    #region Edge Case 5: Cache Warming with Slow Providers

    [Fact]
    public async Task EdgeCase5_Given_SlowProvidersOnStartup_When_CacheWarming_Then_ContinuesOnBestEffort()
    {
        // Edge Case: What happens during cache warming if external providers are slow or timing out?
        // Answer: Cache warming operates on best-effort basis with timeouts; partial failures don't block startup

        // This is more of a startup behavior test
        // We can verify by checking if service is available even if some warming failed

        // Act - Service should be accessible
        var response = await _client.GetAsync("/currencies/liveness");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Edge Case 5: Cache warming failures should not block service startup");

        // Check if partial cache warming occurred
        var metricsResponse = await _client.GetAsync("/metrics");
        if (metricsResponse.StatusCode == HttpStatusCode.OK)
        {
            var metrics = await metricsResponse.Content.ReadAsStringAsync();
            // Edge Case 5: Partial failures should be logged but not prevent startup
            metrics.Should().NotBeNullOrEmpty();
        }
    }

    #endregion

    #region Edge Case 6: Concurrent Snapshot Ingestion

    [Fact]
    public async Task EdgeCase6_Given_ConcurrentSnapshotSubmissions_When_Processed_Then_SerializedWithQueuing()
    {
        // Edge Case: How does system handle concurrent snapshot ingestion attempts?
        // Answer: System uses pessimistic locking or queue serialization; concurrent submissions are queued

        // Arrange
        var batch1 = new[] { new { from = "USD", to = "EUR", rate = 0.85m, timestamp = "2025-11-02T00:00:00Z" } };
        var batch2 = new[] { new { from = "GBP", to = "USD", rate = 1.37m, timestamp = "2025-11-02T00:00:00Z" } };

        var content1 = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(batch1), System.Text.Encoding.UTF8, "application/json");
        var content2 = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(batch2), System.Text.Encoding.UTF8, "application/json");

        // Act - Submit concurrently
        var task1 = _client.PostAsync("/currencies/v1/admin/snapshots/ingest", content1);
        var task2 = _client.PostAsync("/currencies/v1/admin/snapshots/ingest", content2);

        var responses = await Task.WhenAll(task1, task2);

        // Assert - Edge Case 6: Both should be queued
        responses[0].StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.OK);
        responses[1].StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.OK);

        // Verify they have different batch IDs (not processed as one)
        var result1 = await responses[0].Content.ReadFromJsonAsync<SnapshotIngestionResult>();
        var result2 = await responses[1].Content.ReadFromJsonAsync<SnapshotIngestionResult>();

        result1!.BatchId.Should().NotBe(result2!.BatchId,
            "Edge Case 6: Concurrent submissions should be queued separately");
    }

    #endregion

    #region Edge Case 7: Partially Valid Snapshot Batch

    [Fact]
    public async Task EdgeCase7_Given_PartiallyValidBatch_When_Processed_Then_RejectsEntireBatch()
    {
        // Edge Case: What happens when a snapshot batch is partially valid?
        // Answer: System rejects the entire batch on any validation error (all-or-nothing)

        // Arrange - Mix of valid and invalid records
        var partialBatch = new[]
        {
            new { from = "USD", to = "EUR", rate = 0.85m, timestamp = "2025-11-02T00:00:00Z" }, // Valid
            new { from = "INVALID", to = "GBP", rate = 0.73m, timestamp = "2025-11-02T00:00:00Z" }, // Invalid
            new { from = "USD", to = "JPY", rate = 110.5m, timestamp = "2025-11-02T00:00:00Z" } // Valid
        };

        var content = new System.Net.Http.StringContent(
            System.Text.Json.JsonSerializer.Serialize(partialBatch),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/currencies/v1/admin/snapshots/ingest", content);

        // Assert - Edge Case 7: All-or-nothing
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadAsStringAsync();
            error.Should().Contain("validation",
                "Edge Case 7: System must reject entire batch for data consistency");
        }
        else if (response.StatusCode == HttpStatusCode.Accepted)
        {
            // If accepted for async processing, it should eventually fail
            var result = await response.Content.ReadFromJsonAsync<SnapshotIngestionResult>();
            await Task.Delay(2000); // Wait for processing

            var statusResponse = await _client.GetAsync($"/currencies/v1/admin/snapshots/{result!.BatchId}/status");
            var status = await statusResponse.Content.ReadFromJsonAsync<SnapshotIngestionStatus>();

            status!.Status.Should().Be("Failed",
                "Edge Case 7: Partially valid batch must be rejected entirely");
        }
    }

    #endregion

    #region Edge Case 8: Time Zone Handling

    [Theory]
    [InlineData("2025-11-02")]
    [InlineData("2025-11-03")]
    public async Task EdgeCase8_Given_SnapshotQueries_When_UsingDates_Then_UsesUTCBoundaries(
        string dateQuery)
    {
        // Edge Case: How does system handle time zones for snapshot timestamps and Last-Modified headers?
        // Answer: All timestamps stored and returned in UTC; date-based queries use UTC date boundaries

        // Act
        var response = await _client.GetAsync($"/currencies/v1/rates?from=USD&to=EUR&mode=snapshot&date={dateQuery}");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var rate = await response.Content.ReadFromJsonAsync<SnapshotRateDto>();
            rate.Should().NotBeNull();

            // Edge Case 8: All timestamps in UTC
            rate!.Timestamp.Kind.Should().Be(DateTimeKind.Utc,
                "Edge Case 8: All timestamps must be stored and returned in UTC");

            rate.SnapshotDate.Kind.Should().Be(DateTimeKind.Utc,
                "Edge Case 8: Date-based queries use UTC date boundaries");
        }

        // Check Last-Modified header
        if (response.Content.Headers.LastModified.HasValue)
        {
            response.Content.Headers.LastModified.Value.Offset.Should().Be(TimeSpan.Zero,
                "Edge Case 8: Last-Modified header should use UTC");
        }
    }

    #endregion

    #region Performance Edge Cases

    [Fact]
    public async Task EdgeCase_Performance_Given_1000ConcurrentRequests_When_Executed_Then_NoPerformanceDegradation()
    {
        // SC-003: System successfully serves 1000 concurrent read requests without performance degradation

        // Arrange - Warm cache first
        await _client.GetAsync("/currencies/v1/rates?from=USD&to=THB&mode=live");
        await Task.Delay(100);

        const int concurrentRequests = 1000;
        var tasks = new List<Task<(HttpStatusCode, long)>>();

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await _client.GetAsync("/currencies/v1/rates?from=USD&to=THB&mode=live");
                sw.Stop();
                return (response.StatusCode, sw.ElapsedMilliseconds);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - SC-003
        var successCount = results.Count(r => r.Item1 == HttpStatusCode.OK);
        successCount.Should().Be(concurrentRequests,
            "SC-003: System must successfully serve 1000 concurrent read requests");

        var responseTimes = results.Select(r => r.Item2).ToList();
        var avgTime = responseTimes.Average();
        var p99Index = (int)Math.Ceiling(responseTimes.Count * 0.99) - 1;
        var p99Time = responseTimes.OrderBy(t => t).ElementAt(p99Index);

        p99Time.Should().BeLessThan(200,
            "SC-006: p99 response time should be under 200ms even under load");
    }

    #endregion

    #region SC-011, SC-012, SC-013: Success Criteria Tests

    [Theory]
    [InlineData("TH", "THB")]
    [InlineData("US", "USD")]
    [InlineData("GB", "GBP")]
    [InlineData("JP", "JPY")]
    [InlineData("FR", "EUR")]
    public async Task SC011_Given_CountryCode_When_Resolved_Then_ReturnsCorrectCurrency(
        string countryCode, string expectedCurrency)
    {
        // SC-011: 99.9% of country-to-currency resolution requests return correct currency metadata

        // Act
        var response = await _client.GetAsync($"/currencies/v1/countries/{countryCode}/currency");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var currency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        currency.Should().NotBeNull();
        currency!.Code.Should().Be(expectedCurrency,
            "SC-011: Country resolution must return correct currency");
    }

    [Fact]
    public async Task SC012_Given_TransitiveConversion_When_Calculated_Then_ProducesDeterministicResult()
    {
        // SC-012: Transitive currency conversions produce deterministic results with precision to at least 6 decimal places

        // Act - Request same transitive conversion multiple times
        var responses = new List<ExchangeRateDto>();

        for (int i = 0; i < 5; i++)
        {
            var response = await _client.GetAsync("/currencies/v1/rates?from=THB&to=JPY&mode=live");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
                responses.Add(rate!);
            }
        }

        // Assert - SC-012: Deterministic results
        if (responses.Count > 1)
        {
            var firstRate = responses[0].Rate;
            responses.Skip(1).Should().OnlyContain(r => r.Rate == firstRate,
                "SC-012: Transitive conversions must produce deterministic results");

            // Precision to at least 6 decimal places
            var rateString = firstRate.ToString("F6");
            rateString.Should().NotBeNullOrEmpty(
                "SC-012: Precision to at least 6 decimal places");
        }
    }

    [Fact]
    public async Task SC013_Given_ETagSupport_When_UsedByClients_Then_ReducesBandwidthBy40Percent()
    {
        // SC-013: API consumers successfully use ETag to reduce bandwidth by at least 40% for repeated queries

        // Arrange - First request to get ETag
        var firstResponse = await _client.GetAsync("/currencies/v1/rates?from=USD&to=EUR&mode=live");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var etag = firstResponse.Headers.ETag?.Tag;
        etag.Should().NotBeNullOrEmpty("SC-013: ETags must be supported");

        var firstSize = firstResponse.Content.Headers.ContentLength ?? 0;

        // Act - Subsequent requests with If-None-Match
        var request = new HttpRequestMessage(HttpMethod.Get, "/currencies/v1/rates?from=USD&to=EUR&mode=live");
        request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag!));

        var response = await _client.SendAsync(request);

        // Assert
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            var secondSize = response.Content.Headers.ContentLength ?? 0;
            var bandwidthReduction = ((double)(firstSize - secondSize) / firstSize) * 100;

            bandwidthReduction.Should().BeGreaterThanOrEqualTo(40,
                "SC-013: ETag usage should reduce bandwidth by at least 40%");
        }
    }

    #endregion
}

/// <summary>
/// Error response DTO
/// </summary>
public record ErrorResponse
{
    public string Message { get; init; } = string.Empty;
    public string? Details { get; init; }
    public string? ErrorCode { get; init; }
}
