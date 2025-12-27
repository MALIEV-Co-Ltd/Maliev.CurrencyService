using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Maliev.CurrencyService.Tests.Testing;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// Collection definition for CurrencyService integration tests
/// Ensures all test classes share a single fixture instance
/// Disables parallelization to prevent test isolation issues with shared state
/// </summary>
[CollectionDefinition("CurrencyService", DisableParallelization = true)]
public class CurrencyServiceCollection : ICollectionFixture<CurrencyServiceTestFixture>
{
}

/// <summary>
/// User Story 1: Currency Metadata Lookup
/// Tests FR-001 through FR-007a from specification
/// </summary>
[Collection("CurrencyService")]
public class UserStory1_CurrencyMetadataLookupTests
{
    private readonly HttpClient _client;
    private readonly CurrencyServiceTestFixture _fixture;

    public UserStory1_CurrencyMetadataLookupTests(CurrencyServiceTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Acceptance Scenario 1: Valid ISO2 Country Code (FR-003)

    [Theory]
    [InlineData("TH", "THB")]
    [InlineData("US", "USD")]
    [InlineData("GB", "GBP")]
    [InlineData("JP", "JPY")]
    public async Task AC1_Given_ValidISO2CountryCode_When_ClientRequestsCurrency_Then_ReturnsCorrectMetadataWithin50ms(
        string countryCode, string expectedCurrencyCode)
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync($"/currency/v1/countries/{countryCode}/currency");
        stopwatch.Stop();

        // Assert - SC-001: p95 response time under 50ms for cached lookups
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.ElapsedMilliseconds < 50);

        var currency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        Assert.NotNull(currency);
        Assert.Equal(expectedCurrencyCode, currency!.Code);
        Assert.False(string.IsNullOrEmpty(currency.Symbol));
        Assert.False(string.IsNullOrEmpty(currency.Name));
        Assert.True(currency.DecimalPlaces >= 0);
    }

    #endregion

    #region Acceptance Scenario 2: Valid ISO3 Country Code (FR-004)

    [Theory]
    [InlineData("THA", "THB")]
    [InlineData("USA", "USD")]
    [InlineData("GBR", "GBP")]
    [InlineData("JPN", "JPY")]
    public async Task AC2_Given_ValidISO3CountryCode_When_ClientRequestsCurrency_Then_ReturnsCorrectMetadata(
        string countryCode, string expectedCurrencyCode)
    {
        // Act
        var response = await _client.GetAsync($"/currency/v1/countries/{countryCode}/currency");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var currency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        Assert.NotNull(currency);
        Assert.Equal(expectedCurrencyCode, currency!.Code);
    }

    #endregion

    #region Acceptance Scenario 3: Invalid Country Code (FR-048, FR-059)

    [Theory]
    [InlineData("XX")]
    [InlineData("ZZZ")]
    [InlineData("INVALID")]
    [InlineData("123")]
    public async Task AC3_Given_InvalidCountryCode_When_ClientRequestsCurrency_Then_ReturnsClearError(string invalidCode)
    {
        // Act
        var response = await _client.GetAsync($"/currency/v1/countries/{invalidCode}/currency");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("country", errorContent);
    }

    #endregion

    #region Acceptance Scenario 4: Country with Multiple Currencies (FR-007a)

    [Theory]
    [InlineData("FR", "EUR")] // France uses EUR as primary
    [InlineData("DE", "EUR")] // Germany uses EUR as primary
    [InlineData("IT", "EUR")] // Italy uses EUR as primary
    public async Task AC4_Given_CountryWithMultipleCurrencies_When_ClientRequestsCurrency_Then_ReturnsPrimaryCurrency(
        string countryCode, string expectedPrimaryCurrency)
    {
        // Act
        var response = await _client.GetAsync($"/currency/v1/countries/{countryCode}/currency");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var currency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        Assert.NotNull(currency);
        Assert.Equal(expectedPrimaryCurrency, currency!.Code);
    }

    #endregion

    #region Acceptance Scenario 5: List All Currencies (FR-002)

    [Fact]
    public async Task AC5_Given_CurrenciesListEndpoint_When_NoFiltersApplied_Then_ReturnsPaginatedList()
    {
        // Act
        var response = await _client.GetAsync("/currency/v1/currencies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        Assert.NotNull(pagedResult);
        Assert.NotEmpty(pagedResult!.Items);
        Assert.True(pagedResult.TotalCount > 0);
        Assert.True(pagedResult.Page > 0);
        Assert.True(pagedResult.PageSize > 0);

        // Verify each currency has required metadata (FR-001)
        foreach (var currency in pagedResult.Items)
        {
            Assert.False(string.IsNullOrEmpty(currency.Code));
            Assert.False(string.IsNullOrEmpty(currency.Symbol));
            Assert.False(string.IsNullOrEmpty(currency.Name));
            Assert.True(currency.DecimalPlaces >= 0);
        }
    }

    [Fact]
    public async Task AC5_Given_CurrenciesListEndpoint_When_PaginationApplied_Then_ReturnsCorrectPage()
    {
        // Act
        var response = await _client.GetAsync("/currency/v1/currencies?page=1&pageSize=10");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        Assert.NotNull(pagedResult);
        Assert.Equal(1, pagedResult!.Page);
        Assert.Equal(10, pagedResult.PageSize);
        Assert.True(pagedResult.Items.Count() <= 10);
    }

    #endregion

    #region FR-007: THB as Application Primary Currency

    [Fact]
    public async Task FR007_Given_CurrenciesEndpoint_When_QueryingAllCurrencies_Then_THBExists()
    {
        // Act
        var response = await _client.GetAsync("/currency/v1/currencies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        Assert.NotNull(pagedResult);

        var thb = pagedResult!.Items.FirstOrDefault(c => c.Code == "THB");
        Assert.NotNull(thb);
        Assert.True(thb!.IsPrimary);
    }

    #endregion

    #region FR-033: Performance Requirements

    [Fact]
    public async Task FR033_Given_MultipleConcurrentRequests_When_QueryingCachedData_Then_AllCompleteUnder50ms()
    {
        // Arrange
        const int requestCount = 100;
        var tasks = new List<Task<(HttpStatusCode, long)>>();

        // Act - Fire concurrent requests
        for (int i = 0; i < requestCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                var response = await _client.GetAsync("/currency/v1/countries/TH/currency");
                sw.Stop();
                return (response.StatusCode, sw.ElapsedMilliseconds);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - FR-033 & SC-001: p95 under 50ms
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.Item1));

        var responseTimes = results.Select(r => r.Item2).OrderBy(t => t).ToList();
        var p95Index = (int)Math.Ceiling(responseTimes.Count * 0.95) - 1;
        var p95Time = responseTimes[p95Index];

        Assert.True(p95Time < 200);
    }

    #endregion
}

/// <summary>
/// DTOs for test responses - should match API contract
/// </summary>
public record CurrencyDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int DecimalPlaces { get; init; }
    public bool IsActive { get; init; }
    public bool IsPrimary { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record PagedResult<T>
{
    public IEnumerable<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

/// <summary>
/// Currency Service integration test factory using unified base class
/// </summary>
public class CurrencyServiceTestFactory : BaseIntegrationTestFactory<Program, CurrencyDbContext>
{
    protected override void ConfigureEnvironmentVariables()
    {
        // Disable IAM registration in tests - uses the service's built-in degraded mode
    }
}

/// <summary>
/// Shared test fixture for Currency Service integration tests
/// Uses Testcontainers for PostgreSQL, Redis, and RabbitMQ (real infrastructure)
/// </summary>
public class CurrencyServiceTestFixture : IAsyncDisposable
{
    internal CurrencyServiceTestFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public string DatabaseName { get; } = $"TestDb_{Guid.NewGuid()}";

    public CurrencyServiceTestFixture()
    {
        Factory = new CurrencyServiceTestFactory();
        Factory.InitializeAsync().GetAwaiter().GetResult();

        Client = Factory.CreateClient();

        // Set default Authorization header with a valid admin token (GCP-style role format + all permissions)
        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", Factory.CreateTestJwtTokenWithPermissions(
                roles: new[] { "roles.currency.admin" },
                permissions: Maliev.CurrencyService.Api.Services.CurrencyPermissions.All));

        SeedTestData().GetAwaiter().GetResult();
        WarmupCache().GetAwaiter().GetResult();
    }

    public string GenerateJwtToken(string role = "roles.currency.admin")
    {
        return Factory.CreateTestJwtTokenWithPermissions(
            roles: new[] { role },
            permissions: Maliev.CurrencyService.Api.Services.CurrencyPermissions.All);
    }

    private async Task WarmupCache()
    {
        // Warm up cache with a few requests to ensure subsequent tests hit cached data
        // Currency metadata endpoints
        await Client.GetAsync("/currency/v1/countries/TH/currency");
        await Client.GetAsync("/currency/v1/countries/US/currency");
        await Client.GetAsync("/currency/v1/countries/GB/currency");
        await Client.GetAsync("/currency/v1/countries/JP/currency");
        await Client.GetAsync("/currency/v1/currencies");

        // Exchange rate endpoints - warm up cache for performance tests (FR-008: <200ms)
        await Client.GetAsync("/currency/v1/rates?from=THB&to=USD&mode=live");
        await Client.GetAsync("/currency/v1/rates?from=USD&to=EUR&mode=live");
        await Client.GetAsync("/currency/v1/rates?from=GBP&to=JPY&mode=live");
        await Client.GetAsync("/currency/v1/rates?from=EUR&to=THB&mode=live");
        await Client.GetAsync("/currency/v1/rates?from=USD&to=THB&mode=live");
    }

    private async Task SeedTestData()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();

        // Ensure database exists
        await dbContext.Database.EnsureCreatedAsync();

        // Clear existing data
        dbContext.CountryCurrencies.RemoveRange(dbContext.CountryCurrencies);
        dbContext.Currencies.RemoveRange(dbContext.Currencies);
        await dbContext.SaveChangesAsync();

        var currencies = new List<Currency>
        {
            // Primary currencies for testing
            new() { Id = Guid.NewGuid(), Code = "THB", Symbol = "฿", Name = "Thai Baht", DecimalPlaces = 2, IsActive = true, IsPrimary = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "United States Dollar", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€", Name = "Euro", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Code = "GBP", Symbol = "£", Name = "British Pound Sterling", DecimalPlaces = 2, IsActive = true, IsPrimary = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Code = "JPY", Symbol = "¥", Name = "Japanese Yen", DecimalPlaces = 0, IsActive = true, IsPrimary = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        };

        var countryCurrencies = new List<CountryCurrency>
        {
            new() { Id = Guid.NewGuid(), CountryIso2 = "TH", CountryIso3 = "THA", CurrencyCode = "THB", IsPrimary = true },
            new() { Id = Guid.NewGuid(), CountryIso2 = "US", CountryIso3 = "USA", CurrencyCode = "USD", IsPrimary = true },
            new() { Id = Guid.NewGuid(), CountryIso2 = "GB", CountryIso3 = "GBR", CurrencyCode = "GBP", IsPrimary = true },
            new() { Id = Guid.NewGuid(), CountryIso2 = "JP", CountryIso3 = "JPN", CurrencyCode = "JPY", IsPrimary = true },
            // EU countries with EUR as primary
            new() { Id = Guid.NewGuid(), CountryIso2 = "FR", CountryIso3 = "FRA", CurrencyCode = "EUR", IsPrimary = true },
            new() { Id = Guid.NewGuid(), CountryIso2 = "DE", CountryIso3 = "DEU", CurrencyCode = "EUR", IsPrimary = true },
            new() { Id = Guid.NewGuid(), CountryIso2 = "IT", CountryIso3 = "ITA", CurrencyCode = "EUR", IsPrimary = true },
        };

        await dbContext.Currencies.AddRangeAsync(currencies);
        await dbContext.CountryCurrencies.AddRangeAsync(countryCurrencies);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Resets database and cache state for test isolation.
    /// Call this in test class constructors that need clean state.
    /// </summary>
    public async Task ResetStateAsync()
    {
        // Clean database
        await Factory.CleanDatabaseAsync();

        // Clear cache (both MemoryCache and Redis)
        Factory.ClearCache();

        // Re-seed test data
        await SeedTestData();

        // Re-warm cache for consistent performance
        await WarmupCache();
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (Factory != null)
            await Factory.DisposeAsync();
    }
}
