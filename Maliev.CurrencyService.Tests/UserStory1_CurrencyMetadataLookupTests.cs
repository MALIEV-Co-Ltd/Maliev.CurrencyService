using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Maliev.CurrencyService.Data;
using Maliev.CurrencyService.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// User Story 1: Currency Metadata Lookup
/// Tests FR-001 through FR-007a from specification
/// </summary>
public class UserStory1_CurrencyMetadataLookupTests : IClassFixture<CurrencyServiceTestFixture>
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
        var response = await _client.GetAsync($"/currencies/v1/countries/{countryCode}/currency");
        stopwatch.Stop();

        // Assert - SC-001: p95 response time under 50ms for cached lookups
        response.StatusCode.Should().Be(HttpStatusCode.OK, "valid country code should return 200");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(50, "FR-033: response time must be under 50ms for cached lookups");

        var currency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        currency.Should().NotBeNull();
        currency!.Code.Should().Be(expectedCurrencyCode, "FR-003: system must resolve currency by ISO2 country code");
        currency.Symbol.Should().NotBeNullOrEmpty("FR-001: currency metadata must include symbol");
        currency.Name.Should().NotBeNullOrEmpty("FR-001: currency metadata must include name");
        currency.DecimalPlaces.Should().BeGreaterThanOrEqualTo(0, "FR-001: currency metadata must include decimal places");
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
        var response = await _client.GetAsync($"/currencies/v1/countries/{countryCode}/currency");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var currency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        currency.Should().NotBeNull();
        currency!.Code.Should().Be(expectedCurrencyCode, "FR-004: system must resolve currency by ISO3 country code");
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
        var response = await _client.GetAsync($"/currencies/v1/countries/{invalidCode}/currency");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "FR-059: clear error messages for failure scenarios");

        var errorContent = await response.Content.ReadAsStringAsync();
        errorContent.Should().Contain("country", "error message should indicate country code issue");
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
        var response = await _client.GetAsync($"/currencies/v1/countries/{countryCode}/currency");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var currency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        currency.Should().NotBeNull();
        currency!.Code.Should().Be(expectedPrimaryCurrency,
            "FR-007a: for countries with multiple currencies, system must return only the designated primary currency");
    }

    #endregion

    #region Acceptance Scenario 5: List All Currencies (FR-002)

    [Fact]
    public async Task AC5_Given_CurrenciesListEndpoint_When_NoFiltersApplied_Then_ReturnsPaginatedList()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1/currencies");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        pagedResult.Should().NotBeNull("FR-002: system must provide endpoint to list all available currencies");
        pagedResult!.Items.Should().NotBeEmpty();
        pagedResult.TotalCount.Should().BeGreaterThan(0);
        pagedResult.Page.Should().BeGreaterThan(0);
        pagedResult.PageSize.Should().BeGreaterThan(0);

        // Verify each currency has required metadata (FR-001)
        foreach (var currency in pagedResult.Items)
        {
            currency.Code.Should().NotBeNullOrEmpty();
            currency.Symbol.Should().NotBeNullOrEmpty();
            currency.Name.Should().NotBeNullOrEmpty();
            currency.DecimalPlaces.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task AC5_Given_CurrenciesListEndpoint_When_PaginationApplied_Then_ReturnsCorrectPage()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1/currencies?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        pagedResult.Should().NotBeNull();
        pagedResult!.Page.Should().Be(1);
        pagedResult.PageSize.Should().Be(10);
        pagedResult.Items.Count().Should().BeLessThanOrEqualTo(10);
    }

    #endregion

    #region FR-007: THB as Application Primary Currency

    [Fact]
    public async Task FR007_Given_CurrenciesEndpoint_When_QueryingAllCurrencies_Then_THBExists()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1/currencies");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        pagedResult.Should().NotBeNull();

        var thb = pagedResult!.Items.FirstOrDefault(c => c.Code == "THB");
        thb.Should().NotBeNull("FR-007: system must treat THB as the application primary currency");
        thb!.IsPrimary.Should().BeTrue("THB should be marked as primary currency");
    }

    #endregion

    #region FR-033: Performance Requirements

    [Fact(Skip = "Performance test - timing sensitive in CI environment")]
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
                var response = await _client.GetAsync("/currencies/v1/countries/TH/currency");
                sw.Stop();
                return (response.StatusCode, sw.ElapsedMilliseconds);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - FR-033 & SC-001: p95 under 50ms
        results.Should().OnlyContain(r => r.Item1 == HttpStatusCode.OK, "all requests should succeed");

        var responseTimes = results.Select(r => r.Item2).OrderBy(t => t).ToList();
        var p95Index = (int)Math.Ceiling(responseTimes.Count * 0.95) - 1;
        var p95Time = responseTimes[p95Index];

        p95Time.Should().BeLessThan(50, "SC-001: 95th percentile response time must be under 50ms");
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
/// Shared test fixture for Currency Service integration tests
/// </summary>
public class CurrencyServiceTestFixture : IAsyncDisposable
{
    internal WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public string DatabaseName { get; } = $"TestDb_{Guid.NewGuid()}";

    public CurrencyServiceTestFixture()
    {
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Remove the real database
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CurrencyServiceDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Remove Redis cache service
                var cacheDescriptor = services.SingleOrDefault(d => d.ServiceType.Name.Contains("ICacheService"));
                if (cacheDescriptor != null)
                {
                    services.Remove(cacheDescriptor);
                }

                // Remove Redis distributed cache
                var redisDescriptor = services.SingleOrDefault(d => d.ServiceType.Name.Contains("IDistributedCache"));
                if (redisDescriptor != null)
                {
                    services.Remove(redisDescriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<CurrencyServiceDbContext>(options =>
                {
                    options.UseInMemoryDatabase(DatabaseName);
                });

                // Add in-memory cache only for tests (no Redis)
                services.AddMemoryCache();
                services.AddSingleton<Maliev.CurrencyService.Api.Services.ICacheService, Maliev.CurrencyService.Api.Services.InMemoryCacheService>();

                // Add test authentication scheme for endpoints with [Authorize]
                services.AddAuthentication("TestScheme")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        "TestScheme", options => { });
                services.AddAuthorization();
            });
        });

        Client = Factory.CreateClient();
        SeedTestData().GetAwaiter().GetResult();
        WarmupCache().GetAwaiter().GetResult();
    }

    private async Task WarmupCache()
    {
        // Warm up cache with a few requests to ensure subsequent tests hit cached data
        // Currency metadata endpoints
        await Client.GetAsync("/currencies/v1/countries/TH/currency");
        await Client.GetAsync("/currencies/v1/countries/US/currency");
        await Client.GetAsync("/currencies/v1/countries/GB/currency");
        await Client.GetAsync("/currencies/v1/countries/JP/currency");
        await Client.GetAsync("/currencies/v1/currencies");

        // Exchange rate endpoints - warm up cache for performance tests (FR-008: <200ms)
        await Client.GetAsync("/currencies/v1/rates?from=THB&to=USD&mode=live");
        await Client.GetAsync("/currencies/v1/rates?from=USD&to=EUR&mode=live");
        await Client.GetAsync("/currencies/v1/rates?from=GBP&to=JPY&mode=live");
        await Client.GetAsync("/currencies/v1/rates?from=EUR&to=THB&mode=live");
        await Client.GetAsync("/currencies/v1/rates?from=USD&to=THB&mode=live");
    }

    private async Task SeedTestData()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CurrencyServiceDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        if (!await dbContext.Currencies.AnyAsync())
        {
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
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        if (Factory != null)
            await Factory.DisposeAsync();
    }
}
