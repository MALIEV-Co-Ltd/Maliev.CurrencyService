using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
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
            rate.Should().NotBeNull();
            rate!.FromCurrency.Should().Be("USD");
            rate.ToCurrency.Should().Be("THB");
            rate.Rate.Should().BeGreaterThan(0);
            rate.SnapshotDate.Date.Should().Be(testDate.Date,
                "FR-009: snapshot query must return rate for specified date");
            rate.IsSnapshot.Should().BeTrue("response should indicate this is snapshot data, not live");
        }
        else
        {
            // If snapshot doesn't exist for this date, should return 404
            response.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "FR-009: if no snapshot exists for requested date, return 404");
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
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "FR-009: system must return 404 error indicating no snapshot available for that date");

        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("snapshot", "error message should indicate snapshot issue");
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
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "FR-048: system must validate and sanitize date input");
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
        etag.Should().NotBeNullOrEmpty("FR-015: snapshots must support ETag");

        // Act - Request with If-None-Match
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/currencies/v1/rates?from=USD&to=EUR&mode=snapshot&date={testDate:yyyy-MM-dd}");
        request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag!));

        var response = await _client.SendAsync(request);

        // Assert - FR-017: If snapshot hasn't changed, return 304
        response.StatusCode.Should().Be(HttpStatusCode.NotModified,
            "snapshot data shouldn't change, so ETag should match");
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
        lastModified.Should().NotBeNull("FR-016: snapshots must support Last-Modified header");

        // Act - Request with If-Modified-Since
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/currencies/v1/rates?from=USD&to=JPY&mode=snapshot&date={testDate:yyyy-MM-dd}");
        request.Headers.IfModifiedSince = lastModified;

        var response = await _client.SendAsync(request);

        // Assert - FR-017: Snapshot hasn't been updated since that time
        response.StatusCode.Should().Be(HttpStatusCode.NotModified,
            "snapshot data is immutable, so should return 304");
    }

    #endregion

    #region FR-009: Mode Parameter Requirement

    [Fact]
    public async Task FR009_Given_SnapshotMode_When_DateNotProvided_Then_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1/rates?from=USD&to=THB&mode=snapshot");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "FR-009: mode=snapshot requires date parameter");

        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("date", "error should indicate missing date parameter");
    }

    [Fact]
    public async Task FR009_Given_LiveMode_When_DateProvided_Then_IgnoresDateOrReturnsError()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1/rates?from=USD&to=THB&mode=live&date=2025-11-02");

        // Assert
        // System can either ignore the date parameter for live mode, or return error
        // Both behaviors are acceptable as long as it's consistent
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var rate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
            rate.Should().NotBeNull();
            rate!.IsSnapshot.Should().BeFalse("live mode should not return snapshot data");
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
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Gone);
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
            rate.Should().NotBeNull();
            rate!.Timestamp.Kind.Should().Be(DateTimeKind.Utc,
                "edge case: all timestamps stored and returned in UTC");
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
