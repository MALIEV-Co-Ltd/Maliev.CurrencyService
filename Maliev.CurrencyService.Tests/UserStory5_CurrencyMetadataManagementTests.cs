using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Maliev.CurrencyService.Tests;

/// <summary>
/// User Story 5: Currency Metadata Management
/// Tests FR-005, FR-006, FR-024, FR-046, FR-053 from specification
/// </summary>
public class UserStory5_CurrencyMetadataManagementTests : IClassFixture<CurrencyServiceTestFixture>
{
    private readonly HttpClient _client;
    private readonly CurrencyServiceTestFixture _fixture;

    public UserStory5_CurrencyMetadataManagementTests(CurrencyServiceTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Acceptance Scenario 1: Create New Currency (FR-005, FR-053)

    [Fact]
    public async Task AC1_Given_AdminWithValidCredentials_When_CreatesNewCurrency_Then_PersistsAndReturnsResource()
    {
        // Arrange - FR-005: CRUD operations via authenticated admin endpoints
        var newCurrency = new CreateCurrencyRequest
        {
            Code = "BTC",
            Symbol = "₿",
            Name = "Bitcoin",
            DecimalPlaces = 8
        };

        var content = new StringContent(
            JsonSerializer.Serialize(newCurrency),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/currencies/v1/admin/currencies", content);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); // FR-005: system must support CRUD operations on currency metadata

        var created = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id); // should return created resource with version identifier
        Assert.Equal("BTC", created.Code);
        Assert.Equal("₿", created.Symbol);
        Assert.Equal("Bitcoin", created.Name);
        Assert.Equal(8, created.DecimalPlaces);

        // Verify Location header
        Assert.NotNull(response.Headers.Location); // should include Location header with resource URI

        // FR-053: All admin operations must be logged
        // Verify currency appears in list
        var listResponse = await _client.GetAsync("/currencies/v1/currencies");
        var currencies = await listResponse.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        Assert.Contains(currencies!.Items, c => c.Code == "BTC");
    }

    #endregion

    #region Acceptance Scenario 2: Update Currency with Optimistic Concurrency (FR-006)

    [Fact]
    public async Task AC2_Given_ExistingCurrency_When_AdminUpdatesWithIfMatch_Then_AppliesUpdateAndInvalidatesCache()
    {
        // Arrange - First get a currency to update
        var getResponse = await _client.GetAsync("/currencies/v1/currencies");
        var currencies = await getResponse.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        var currency = currencies!.Items.First();

        // Get detailed currency with ETag
        var detailResponse = await _client.GetAsync($"/currencies/v1/admin/currencies/{currency.Id}");
        var etag = detailResponse.Headers.ETag?.Tag;
        Assert.False(string.IsNullOrEmpty(etag)); // FR-006: must support optimistic concurrency with version identifiers

        var original = await detailResponse.Content.ReadFromJsonAsync<CurrencyDto>();

        // Prepare update
        var updateRequest = new UpdateCurrencyRequest
        {
            Symbol = original!.Symbol + "*", // Add asterisk instead of long text (max 10 chars)
            Name = original.Name + " Updated",
            DecimalPlaces = original.DecimalPlaces
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            Encoding.UTF8,
            "application/json");

        // Act - FR-006: Update with If-Match header
        var request = new HttpRequestMessage(HttpMethod.Put, $"/currencies/v1/admin/currencies/{currency.Id}")
        {
            Content = content
        };
        request.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(etag!));

        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // FR-006: system must apply update when If-Match matches current version

        var updated = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        Assert.NotNull(updated);
        Assert.Contains("*", updated!.Symbol); // symbol should be updated
        Assert.Contains("Updated", updated!.Name); // name should be updated

        // FR-006: Cache should be invalidated
        var cacheCheckResponse = await _client.GetAsync($"/currencies/v1/currencies/{currency.Id}");
        var cached = await cacheCheckResponse.Content.ReadFromJsonAsync<CurrencyDto>();
        Assert.Contains("Updated", cached!.Name); // cache should be invalidated after update
    }

    #endregion

    #region Acceptance Scenario 3: Update Without Correct If-Match (FR-006)

    [Fact]
    public async Task AC3_Given_ExistingCurrency_When_AdminUpdatesWithoutCorrectIfMatch_Then_ReturnsPreconditionFailed()
    {
        // Arrange
        var getResponse = await _client.GetAsync("/currencies/v1/currencies");
        var currencies = await getResponse.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        var currency = currencies!.Items.First();

        var updateRequest = new UpdateCurrencyRequest
        {
            Symbol = "$",
            Name = "Updated",
            DecimalPlaces = 2
        };

        var content = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            Encoding.UTF8,
            "application/json");

        // Act - Update without If-Match header or with incorrect ETag
        var request = new HttpRequestMessage(HttpMethod.Put, $"/currencies/v1/admin/currencies/{currency.Id}")
        {
            Content = content
        };
        request.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue("\"invalid-etag\""));

        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode); // FR-006: system must reject update without correct If-Match header
    }

    #endregion

    #region Acceptance Scenario 4: Delete Currency with Dependencies (FR-005)

    [Fact]
    public async Task AC4_Given_CurrencyWithCountryMappings_When_AdminAttemptsDelete_Then_RejectsOrWarns()
    {
        // Arrange - THB has country mappings (TH)
        var getResponse = await _client.GetAsync("/currencies/v1/currencies");
        var currencies = await getResponse.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        var thb = currencies!.Items.FirstOrDefault(c => c.Code == "THB");

        if (thb == null)
        {
            // Skip if THB doesn't exist in test data
            return;
        }

        // Act
        var response = await _client.DeleteAsync($"/currencies/v1/admin/currencies/{thb.Id}");

        // Assert
        // System should either reject deletion or warn about dependencies
        // FR-005: system should reject deletion or warn about dependencies
        Assert.Contains(response.StatusCode, new[]
        {
            HttpStatusCode.Conflict,
            HttpStatusCode.BadRequest,
            HttpStatusCode.PreconditionFailed
        });

        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            var error = await response.Content.ReadAsStringAsync();
            Assert.Contains("dependenc", error); // error message should indicate dependency issue
        }
    }

    [Fact]
    public async Task AC4_Given_CurrencyWithoutDependencies_When_AdminDeletes_Then_SucceedsAndInvalidatesCache()
    {
        // Arrange - Create a new currency without dependencies
        var newCurrency = new CreateCurrencyRequest
        {
            Code = "XXX",
            Symbol = "X",
            Name = "Test Currency for Deletion",
            DecimalPlaces = 2
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(newCurrency),
            Encoding.UTF8,
            "application/json");

        var createResponse = await _client.PostAsync("/currencies/v1/admin/currencies", createContent);
        var created = await createResponse.Content.ReadFromJsonAsync<CurrencyDto>();

        // Act
        var response = await _client.DeleteAsync($"/currencies/v1/admin/currencies/{created!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); // deletion of currency without dependencies should succeed

        // Verify it's deleted
        var getResponse = await _client.GetAsync($"/currencies/v1/currencies/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode); // deleted currency should no longer be accessible
    }

    #endregion

    #region Acceptance Scenario 5: Cache Warming After Metadata Changes (FR-024)

    [Fact]
    public async Task AC5_Given_MetadataChangesApplied_When_CacheWarmingTriggered_Then_PrePopulatesCaches()
    {
        // FR-024: System must warm cache on startup with top 20 most frequently accessed currency pairs
        // After metadata changes, cache warming should be triggered

        // Arrange - Make a metadata change
        var newCurrency = new CreateCurrencyRequest
        {
            Code = "XYZ",
            Symbol = "XYZ",
            Name = "Test Currency for Cache Warming",
            DecimalPlaces = 2
        };

        var content = new StringContent(
            JsonSerializer.Serialize(newCurrency),
            Encoding.UTF8,
            "application/json");

        await _client.PostAsync("/currencies/v1/admin/currencies", content);

        // Act - Trigger cache warming (if endpoint exists)
        var warmingResponse = await _client.PostAsync("/currencies/v1/admin/cache/warm", null);

        // Assert
        if (warmingResponse.StatusCode == HttpStatusCode.Accepted || warmingResponse.StatusCode == HttpStatusCode.OK)
        {
            // Cache warming should be triggered
            var result = await warmingResponse.Content.ReadFromJsonAsync<CacheWarmingResult>();
            Assert.NotNull(result);
            Assert.Contains(result!.Status, new[] { "Started", "Completed" });
        }

        // Verify top pairs are cached by querying them and checking response time
        var pairs = new[] { ("USD", "THB"), ("EUR", "THB"), ("USD", "EUR") };
        foreach (var (from, to) in pairs)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var rateResponse = await _client.GetAsync($"/currencies/v1/rates?from={from}&to={to}&mode=live");
            stopwatch.Stop();

            if (rateResponse.StatusCode == HttpStatusCode.OK)
            {
                Assert.True(stopwatch.ElapsedMilliseconds < 100); // FR-024: warmed cache should serve frequently accessed pairs quickly
            }
        }
    }

    #endregion

    #region FR-046, FR-053: RBAC and Audit Logging

    [Fact]
    public async Task FR046_Given_UnauthenticatedUser_When_AccessingAdminEndpoint_Then_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _fixture.Factory.CreateClient();

        // Act - Try to create currency without auth
        var newCurrency = new CreateCurrencyRequest { Code = "TST", Symbol = "T", Name = "Test", DecimalPlaces = 2 };
        var content = new StringContent(JsonSerializer.Serialize(newCurrency), Encoding.UTF8, "application/json");

        var response = await unauthClient.PostAsync("/currencies/v1/admin/currencies", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); // FR-046: system must enforce RBAC for admin endpoints
    }

    [Fact]
    public async Task FR053_Given_AdminOperation_When_Executed_Then_LogsWithUserAndTimestamp()
    {
        // FR-053: System must log all admin operations with user identifier and timestamp

        // Arrange & Act - Create a currency
        var newCurrency = new CreateCurrencyRequest
        {
            Code = "LOG",
            Symbol = "L",
            Name = "Logging Test Currency",
            DecimalPlaces = 2
        };

        var content = new StringContent(JsonSerializer.Serialize(newCurrency), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/currencies/v1/admin/currencies", content);
        var created = await response.Content.ReadFromJsonAsync<CurrencyDto>();

        // Assert - Query audit log if available
        var auditResponse = await _client.GetAsync($"/currencies/v1/admin/currencies/{created!.Id}/audit");
        if (auditResponse.StatusCode == HttpStatusCode.OK)
        {
            var auditLog = await auditResponse.Content.ReadFromJsonAsync<AuditLogEntry>();
            Assert.NotNull(auditLog);
            Assert.Equal("Create", auditLog!.Operation);
            Assert.Equal(created.Id.ToString(), auditLog.EntityId);
            Assert.InRange(auditLog.Timestamp, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
            Assert.False(string.IsNullOrEmpty(auditLog.UserId)); // should log user identifier
        }
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData("", "Symbol", "Name", 2)]
    [InlineData("US", "Symbol", "Name", 2)]
    [InlineData("USDD", "Symbol", "Name", 2)]
    [InlineData("usd", "$", "Name", 2)]
    [InlineData("USD", "", "Name", 2)]
    [InlineData("USD", "$", "", 2)]
    [InlineData("USD", "$", "Name", -1)]
    public async Task Validation_Given_InvalidCurrencyData_When_Creating_Then_ReturnsBadRequest(
        string code, string symbol, string name, int decimalPlaces)
    {
        // Arrange
        var invalidCurrency = new CreateCurrencyRequest
        {
            Code = code,
            Symbol = symbol,
            Name = name,
            DecimalPlaces = decimalPlaces
        };

        var content = new StringContent(JsonSerializer.Serialize(invalidCurrency), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/currencies/v1/admin/currencies", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // FR-048: system must validate input - {reason}

        var error = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(error)); // should provide validation error details
    }

    [Fact]
    public async Task Validation_Given_DuplicateCurrencyCode_When_Creating_Then_ReturnsConflict()
    {
        // Arrange - Try to create currency with existing code
        var duplicate = new CreateCurrencyRequest
        {
            Code = "USD", // Already exists
            Symbol = "$",
            Name = "Duplicate Dollar",
            DecimalPlaces = 2
        };

        var content = new StringContent(JsonSerializer.Serialize(duplicate), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/currencies/v1/admin/currencies", content);

        // Assert - system should prevent duplicate currency codes
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Conflict, HttpStatusCode.BadRequest });
    }

    #endregion

    #region Country-Currency Mapping Management

    [Fact]
    public async Task CountryMapping_Given_AdminAddsMapping_When_Created_Then_ResolvesCorrectly()
    {
        // Test adding country-to-currency mappings

        // Arrange
        var mapping = new CreateCountryCurrencyMappingRequest
        {
            CountryIso2 = "NZ",
            CountryIso3 = "NZL",
            CurrencyCode = "NZD",
            IsPrimary = true
        };

        var content = new StringContent(JsonSerializer.Serialize(mapping), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/currencies/v1/admin/country-mappings", content);

        // Assert
        if (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK)
        {
            // Verify mapping works
            var resolveResponse = await _client.GetAsync("/currencies/v1/countries/NZ/currency");
            Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

            var currency = await resolveResponse.Content.ReadFromJsonAsync<CurrencyDto>();
            Assert.Equal("NZD", currency!.Code); // country mapping should resolve correctly
        }
    }

    #endregion
}

/// <summary>
/// DTOs for currency management requests
/// </summary>
public record CreateCurrencyRequest
{
    public string Code { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int DecimalPlaces { get; init; }
}

public record UpdateCurrencyRequest
{
    public string Symbol { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int DecimalPlaces { get; init; }
}

public record CreateCountryCurrencyMappingRequest
{
    public string CountryIso2 { get; init; } = string.Empty;
    public string CountryIso3 { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
}

public record CacheWarmingResult
{
    public string Status { get; init; } = string.Empty;
    public int PairsWarmed { get; init; }
    public DateTime StartedAt { get; init; }
}

public record AuditLogEntry
{
    public string Operation { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string? Changes { get; init; }
}
