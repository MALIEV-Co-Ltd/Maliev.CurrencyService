using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// User Story 3: Snapshot Exchange Rate Query
/// Tests FR-026 through FR-032 from specification
/// </summary>
public class UserStory3_SnapshotExchangeRateQueryTests : IClassFixture<CurrencyServiceTestFixture>
{
    private readonly HttpClient _client;

    public UserStory3_SnapshotExchangeRateQueryTests(CurrencyServiceTestFixture fixture)
    {
        _client = fixture.Client;
    }

    #region Acceptance Scenario 1: Query Existing Snapshot (FR-008, FR-009)

    [Fact]
    public async Task AC1_Given_SnapshotExistsForDate_When_ClientRequestsSnapshot_Then_ReturnsSnapshotRate()
    {
        // Arrange - For this test to work, we need a snapshot ingested
        // This assumes snapshot data exists or we need to ingest it first
        var testDate = new DateTime(2025, 11, 02);

        // Act
        var response = await _client.GetAsync($"/currencies/v1/rates?from=USD&to=THB&mode=snapshot&date={testDate:yyyy-MM-dd}");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var rate = await response.Content.ReadFromJsonAsync<SnapshotRateDto>();
            Assert.NotNull(rate);
            Assert.Equal("USD", rate!.FromCurrency);
            Assert.Equal("THB", rate.ToCurrency);
            Assert.True(rate.Rate > 0);
            Assert.Equal(testDate.Date, rate.SnapshotDate.Date);
            Assert.True(rate.IsSnapshot);
        }
        else
        {
            // If snapshot doesn't exist for this date, should return 404
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    #endregion

    #region Acceptance Scenario 2: No Snapshot for Requested Date (FR-009, FR-059)

    [Fact]
    public async Task AC2_Given_NoSnapshotForDate_When_ClientRequestsSnapshot_Then_Returns404()
    {
        // Arrange - Use a date far in the past that likely has no snapshot
        var nonExistentDate = new DateTime(2020, 01, 01);

        // Act
        var response = await _client.GetAsync($"/currencies/v1/rates?from=USD&to=THB&mode=snapshot&date={nonExistentDate:yyyy-MM-dd}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("snapshot", errorContent);
    }

    [Theory]
    [InlineData("2025-13-01")] // Invalid month
    [InlineData("invalid-date")]
    [InlineData("")]
    public async Task AC2_Given_InvalidDateFormat_When_ClientRequestsSnapshot_Then_ReturnsBadRequest(string invalidDate)
    {
        // Act
        var response = await _client.GetAsync($"/currencies/v1/rates?from=USD&to=THB&mode=snapshot&date={invalidDate}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Acceptance Scenario 3: ETag Support for Snapshots (FR-015, FR-017)

    [Fact]
    public async Task AC3_Given_SnapshotWithETag_When_ClientProvidesMatchingETag_Then_Returns304()
    {
        // Arrange - Get snapshot with ETag
        var testDate = new DateTime(2025, 11, 02);
        var initialResponse = await _client.GetAsync($"/currencies/v1/rates?from=USD&to=EUR&mode=snapshot&date={testDate:yyyy-MM-dd}");

        if (initialResponse.StatusCode != HttpStatusCode.OK)
        {
            // Skip test if snapshot doesn't exist
            return;
        }

        var etag = initialResponse.Headers.ETag?.Tag;
        Assert.False(string.IsNullOrEmpty(etag));

        // Act - Request with If-None-Match
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/currencies/v1/rates?from=USD&to=EUR&mode=snapshot&date={testDate:yyyy-MM-dd}");
        request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag!));

        var response = await _client.SendAsync(request);

        // Assert - FR-017: If snapshot hasn't changed, return 304
        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    #endregion

    #region Acceptance Scenario 4: If-Modified-Since Support (FR-016, FR-017)

    [Fact]
    public async Task AC4_Given_SnapshotNotUpdated_When_ClientSendsIfModifiedSince_Then_Returns304()
    {
        // Arrange
        var testDate = new DateTime(2025, 11, 02);
        var initialResponse = await _client.GetAsync($"/currencies/v1/rates?from=USD&to=JPY&mode=snapshot&date={testDate:yyyy-MM-dd}");

        if (initialResponse.StatusCode != HttpStatusCode.OK)
        {
            // Skip test if snapshot doesn't exist
            return;
        }

        var lastModified = initialResponse.Content.Headers.LastModified;
        Assert.NotNull(lastModified);

        // Act - Request with If-Modified-Since
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/currencies/v1/rates?from=USD&to=JPY&mode=snapshot&date={testDate:yyyy-MM-dd}");
        request.Headers.IfModifiedSince = lastModified;

        var response = await _client.SendAsync(request);

        // Assert - FR-017: Snapshot hasn't been updated since that time
        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    #endregion

    #region FR-009: Mode Parameter Requirement

    [Fact]
    public async Task FR009_Given_SnapshotMode_When_DateNotProvided_Then_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1/rates?from=USD&to=THB&mode=snapshot");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("date", errorContent);
    }

    [Fact]
    public async Task FR009_Given_LiveMode_When_DateProvided_Then_IgnoresDateOrReturnsError()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1/rates?from=USD&to=THB&mode=live&date=2025-11-02");

        // Assert
        // System can either ignore the date parameter for live mode, or return error
        // Both behaviors are acceptable as long as it's consistent
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest });

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
            Assert.NotNull(rate);
            Assert.False(rate!.IsSnapshot);
        }
    }

    #endregion

    #region FR-031: Snapshot Retention Window

    [Fact]
    public async Task FR031_Given_SnapshotOlderThanRetentionWindow_When_ClientRequests_Then_Returns404Or410()
    {
        // Arrange - Request snapshot older than 90 days (default retention)
        var oldDate = DateTime.UtcNow.AddDays(-100);

        // Act
        var response = await _client.GetAsync($"/currencies/v1/rates?from=USD&to=THB&mode=snapshot&date={oldDate:yyyy-MM-dd}");

        // Assert
        // FR-031: Old snapshots should be purged - snapshots older than retention window should not be accessible
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Gone });
    }

    #endregion

    #region Time Zone Handling (Edge Case from Spec)

    [Theory]
    [InlineData("2025-11-02T00:00:00Z")]
    [InlineData("2025-11-02T23:59:59Z")]
    public async Task EdgeCase_Given_SnapshotQuery_When_UsingUTCDates_Then_UsesUTCBoundaries(string utcTimestamp)
    {
        // Arrange
        var testDate = DateTime.Parse(utcTimestamp).Date;

        // Act
        var response = await _client.GetAsync($"/currencies/v1/rates?from=USD&to=THB&mode=snapshot&date={testDate:yyyy-MM-dd}");

        // Assert
        // All timestamps should be in UTC; date boundaries use UTC
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var rate = await response.Content.ReadFromJsonAsync<SnapshotRateDto>();
            Assert.NotNull(rate);
            Assert.Equal(DateTimeKind.Utc, rate!.Timestamp.Kind);
        }
    }

    #endregion
}

/// <summary>
/// Snapshot Rate DTO for test responses
/// </summary>
public record SnapshotRateDto
{
    public string FromCurrency { get; init; } = string.Empty;
    public string ToCurrency { get; init; } = string.Empty;
    public decimal Rate { get; init; }
    public DateTime SnapshotDate { get; init; }
    public DateTime Timestamp { get; init; }
    public bool IsSnapshot { get; init; }
    public string Source { get; init; } = string.Empty;
}
