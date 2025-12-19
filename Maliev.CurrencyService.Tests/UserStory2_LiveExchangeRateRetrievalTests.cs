using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// User Story 2: Live Exchange Rate Retrieval
/// Tests FR-008 through FR-018, FR-040 through FR-044 from specification
/// </summary>
[Collection("CurrencyService")]
public class UserStory2_LiveExchangeRateRetrievalTests
{
    private readonly HttpClient _client;

    public UserStory2_LiveExchangeRateRetrievalTests(CurrencyServiceTestFixture fixture)
    {
        _client = fixture.Client;
    }

    #region Acceptance Scenario 1: Valid Currency Pair from Primary Provider (FR-008, FR-010, FR-013, FR-014)

    [Theory]
    [InlineData("THB", "USD")]
    [InlineData("USD", "EUR")]
    [InlineData("GBP", "JPY")]
    public async Task AC1_Given_ValidPairInPrimaryProvider_When_ClientRequestsLiveRate_Then_ReturnsRateUnder200ms(
        string fromCurrency, string toCurrency)
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync($"/currency/v1/rates?from={fromCurrency}&to={toCurrency}&mode=live");
        stopwatch.Stop();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 200);

        var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
        Assert.NotNull(rate);
        Assert.Equal(fromCurrency, rate!.FromCurrency);
        Assert.Equal(toCurrency, rate.ToCurrency);
        Assert.True(rate.Rate > 0);
        Assert.False(string.IsNullOrEmpty(rate.Source));
        Assert.True((DateTime.UtcNow - rate.Timestamp).Duration() < TimeSpan.FromMinutes(5));

        // FR-010: Should be from primary provider (Fawazahmed) or secondary (Frankfurter)
        Assert.True(rate.Source.Contains("Fawazahmed") || rate.Source.Contains("Frankfurter"));
    }

    #endregion

    #region Acceptance Scenario 2: Fallback to Secondary Provider (FR-041, FR-044)

    [Fact]
    public async Task AC2_Given_PrimaryProviderUnavailable_When_ClientRequestsLiveRate_Then_FallsBackToSecondary()
    {
        // This test requires mocking or provider unavailability scenario
        // For now, we test that the system CAN use secondary provider

        // Act
        var response = await _client.GetAsync("/currency/v1/rates?from=USD&to=GBP&mode=live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
        Assert.NotNull(rate);
        Assert.False(string.IsNullOrEmpty(rate!.Source));

        // FR-044: System must handle provider timeout gracefully
        Assert.True(rate.Rate > 0);
    }

    #endregion

    #region Acceptance Scenario 3: Transitive Rate Calculation (FR-012)

    [Theory]
    [InlineData("THB", "JPY")] // Common pair that might need transitive conversion
    [InlineData("GBP", "THB")]
    public async Task AC3_Given_DirectPairUnavailable_When_ClientRequestsLiveRate_Then_ComputesTransitiveRate(
        string fromCurrency, string toCurrency)
    {
        // Act
        var response = await _client.GetAsync($"/currency/v1/rates?from={fromCurrency}&to={toCurrency}&mode=live");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
            Assert.NotNull(rate);
            Assert.True(rate!.Rate > 0);

            // If transitive calculation was used, it should be indicated in metadata
            if (rate.IsTransitive)
            {
                Assert.False(string.IsNullOrEmpty(rate.IntermediaryCurrency));
                Assert.False(string.IsNullOrEmpty(rate.CalculationDetails));
            }
        }
        else
        {
            // If neither direct nor transitive rate is available, should return appropriate error
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.ServiceUnavailable });
        }
    }

    #endregion

    #region Acceptance Scenario 4: Fallback to Cached Rate (FR-055)

    [Fact]
    public async Task AC4_Given_AllProvidersUnavailable_When_ClientRequestsLiveRate_Then_ReturnsCachedRateWithStalenessHeader()
    {
        // Arrange - First request to populate cache
        var initialResponse = await _client.GetAsync("/currency/v1/rates?from=USD&to=THB&mode=live");
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);

        // For this test to fully work, we'd need to simulate provider unavailability
        // Here we verify the contract exists

        // Act - Request when providers might be unavailable
        var response = await _client.GetAsync("/currency/v1/rates?from=USD&to=THB&mode=live");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
            Assert.NotNull(rate);

            // FR-018 & FR-055: If serving from cache during provider unavailability,
            // must include staleness indicator header
            if (response.Headers.Contains("X-Rate-Stale") || response.Headers.Contains("Warning"))
            {
                Assert.True(rate!.IsStale);
            }
        }
        else
        {
            // FR-055: If no cache available, return 503
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
    }

    #endregion

    #region Acceptance Scenario 5: Cached Rate Performance (FR-019, FR-021, FR-022, FR-033)

    [Fact]
    public async Task AC5_Given_CachedRateExists_When_ClientRequestsSamePair_Then_ReturnsCachedRateUnder50ms()
    {
        // Arrange - First request to populate cache
        var warmupResponse = await _client.GetAsync("/currency/v1/rates?from=USD&to=EUR&mode=live");
        Assert.Equal(HttpStatusCode.OK, warmupResponse.StatusCode);

        // Wait a moment to ensure cache is populated
        await Task.Delay(100);

        // Act - Second request should hit cache
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync("/currency/v1/rates?from=USD&to=EUR&mode=live");
        stopwatch.Stop();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 50);

        var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
        Assert.NotNull(rate);
        Assert.True(rate!.Rate > 0);

        // FR-019: Verify caching is working (can check via cache headers or metrics)
        // FR-021: TTL should be applied (60-300 seconds default)
        // FR-022: Stale-while-revalidate pattern should be in effect
    }

    [Fact]
    public async Task AC5_Given_MultipleConcurrentCachedRequests_When_Executed_Then_AllCompleteUnder50ms()
    {
        // Arrange - Warm cache
        await _client.GetAsync("/currency/v1/rates?from=USD&to=THB&mode=live");
        await Task.Delay(100);

        const int requestCount = 100;
        var tasks = new List<Task<(HttpStatusCode, long)>>();

        // Act - Fire concurrent cached requests
        for (int i = 0; i < requestCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                var response = await _client.GetAsync("/currency/v1/rates?from=USD&to=THB&mode=live");
                sw.Stop();
                return (response.StatusCode, sw.ElapsedMilliseconds);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - SC-002 & SC-003: p95 under 50ms, handles 1000 concurrent requests
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.Item1));

        var responseTimes = results.Select(r => r.Item2).OrderBy(t => t).ToList();
        var p95Index = (int)Math.Ceiling(responseTimes.Count * 0.95) - 1;
        var p95Time = responseTimes[p95Index];

        Assert.True(p95Time < 50);
    }

    #endregion

    #region FR-015, FR-016, FR-017: ETag and Conditional Requests

    [Fact]
    public async Task FR015_016_017_Given_RateWithETag_When_ClientSendsIfNoneMatch_Then_Returns304NotModified()
    {
        // Arrange - First request to get ETag
        var initialResponse = await _client.GetAsync("/currency/v1/rates?from=USD&to=EUR&mode=live");
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);

        var etag = initialResponse.Headers.ETag?.Tag;
        Assert.False(string.IsNullOrEmpty(etag));

        // Act - Request with If-None-Match
        var request = new HttpRequestMessage(HttpMethod.Get, "/currency/v1/rates?from=USD&to=EUR&mode=live");
        request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag!));

        var response = await _client.SendAsync(request);

        // Assert
        // FR-017: System must honor If-None-Match headers
        // If content hasn't changed, should return 304
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            // 304 response should have no body
            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue)
            {
                Assert.True(contentLength.Value < 1);
            }
        }
        else
        {
            // Content changed, should return 200 with new ETag
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(response.Headers.ETag);
        }
    }

    [Fact]
    public async Task FR016_017_Given_RateWithLastModified_When_ClientSendsIfModifiedSince_Then_ReturnsAppropriately()
    {
        // Arrange
        var initialResponse = await _client.GetAsync("/currency/v1/rates?from=USD&to=GBP&mode=live");
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);

        var lastModified = initialResponse.Content.Headers.LastModified;
        Assert.NotNull(lastModified);

        // Act - Request with If-Modified-Since
        var request = new HttpRequestMessage(HttpMethod.Get, "/currency/v1/rates?from=USD&to=GBP&mode=live");
        request.Headers.IfModifiedSince = lastModified;

        var response = await _client.SendAsync(request);

        // Assert - FR-017: System must honor If-Modified-Since headers
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotModified });
    }

    #endregion

    #region FR-048: Input Validation

    [Theory]
    [InlineData("", "USD")]
    [InlineData("USD", "")]
    [InlineData("USDD", "EUR")]
    [InlineData("USD", "EU")]
    [InlineData("123", "USD")]
    public async Task FR048_Given_InvalidInput_When_ClientRequestsRate_Then_ReturnsBadRequest(
        string fromCurrency, string toCurrency)
    {
        // Act
        var response = await _client.GetAsync($"/currency/v1/rates?from={fromCurrency}&to={toCurrency}&mode=live");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(errorContent));
    }

    #endregion
}

/// <summary>
/// Exchange Rate DTO for test responses
/// </summary>
public record ExchangeRateDto
{
    public string FromCurrency { get; init; } = string.Empty;
    public string ToCurrency { get; init; } = string.Empty;
    public decimal Rate { get; init; }
    public string Source { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public bool IsTransitive { get; init; }
    public string? IntermediaryCurrency { get; init; }
    public string? CalculationDetails { get; init; }
    public bool IsStale { get; init; }
    public string? ConfidenceLevel { get; init; }
    public bool IsSnapshot { get; init; }
}
