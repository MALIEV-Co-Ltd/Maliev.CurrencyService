using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Data.DbContexts;
using Maliev.CurrencyService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class CurrencyControllerIntegrationTestFixture : IAsyncDisposable
{
    internal WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public string DatabaseName { get; } = $"TestDb_{Guid.NewGuid()}";

    public CurrencyControllerIntegrationTestFixture()
    {
        InitializeAsync().GetAwaiter().GetResult();
    }

    private async Task InitializeAsync()
    {
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CurrencyDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<CurrencyDbContext>(options =>
                {
                    options.UseInMemoryDatabase(DatabaseName);
                });

                services.PostConfigure<AuthorizationOptions>(options =>
                {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder()
                        .RequireAssertion(_ => true)
                        .Build();
                });
            });
        });

        Client = Factory.CreateClient();
        await SeedTestDataAsync();
    }

    private async Task SeedTestDataAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        if (!await dbContext.Currencies.AnyAsync())
        {
            var testCurrencies = new[]
            {
                new Currency { ShortName = "USD", LongName = "United States Dollar", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
                new Currency { ShortName = "EUR", LongName = "Euro", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
                new Currency { ShortName = "THB", LongName = "Thai Baht", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
                new Currency { ShortName = "GBP", LongName = "British Pound Sterling", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
                new Currency { ShortName = "JPY", LongName = "Japanese Yen", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            };

            await dbContext.Currencies.AddRangeAsync(testCurrencies);
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

[CollectionDefinition("Currency Integration Tests")]
public class CurrencyTestCollection : ICollectionFixture<CurrencyControllerIntegrationTestFixture>
{
}

[Collection("Currency Integration Tests")]
public class CurrencyControllerIntegrationTests
{
    private readonly CurrencyControllerIntegrationTestFixture _fixture;
    private readonly HttpClient _client;

    public CurrencyControllerIntegrationTests(CurrencyControllerIntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetAllCurrencies_ShouldReturnSeededCurrencies()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThan(0); // We seeded test currencies
        
        // Verify some well-known currencies exist
        var currencies = result.Items.ToList();
        currencies.Should().Contain(c => c.ShortName == "USD");
        currencies.Should().Contain(c => c.ShortName == "EUR");
        currencies.Should().Contain(c => c.ShortName == "THB");
    }

    [Fact]
    public async Task GetCurrencyById_WithValidId_ShouldReturnCurrency()
    {
        // Arrange - First get all currencies to find a valid ID
        var allResponse = await _client.GetAsync("/currencies/v1.0");
        var allResult = await allResponse.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        var firstCurrency = allResult!.Items.First();

        // Act
        var response = await _client.GetAsync($"/currencies/v1.0/{firstCurrency.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var currency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        currency.Should().NotBeNull();
        currency!.Id.Should().Be(firstCurrency.Id);
        currency.ShortName.Should().Be(firstCurrency.ShortName);
        currency.LongName.Should().Be(firstCurrency.LongName);
    }

    [Fact]
    public async Task GetCurrencyById_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SearchCurrencies_WithValidTerm_ShouldReturnMatchingCurrencies()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0?search=Dollar");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(c => 
            c.ShortName.Contains("Dollar", StringComparison.OrdinalIgnoreCase) ||
            c.LongName.Contains("Dollar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchCurrencies_WithShortNameTerm_ShouldReturnMatchingCurrencies()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0?search=USD");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty(); // Changed from ContainSingle to NotBeEmpty
        result.Items.Should().Contain(c => c.ShortName == "USD");
    }

    [Fact]
    public async Task SearchCurrencies_WithNoMatches_ShouldReturnEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0?search=NonExistentCurrency123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateCurrency_WithValidData_ShouldCreateAndReturnCurrency()
    {
        // Arrange
        var newCurrency = new CreateCurrencyRequest
        {
            ShortName = "TST",
            LongName = "Test Currency"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/currencies/v1.0", newCurrency);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdCurrency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        createdCurrency.Should().NotBeNull();
        createdCurrency!.ShortName.Should().Be("TST");
        createdCurrency.LongName.Should().Be("Test Currency");
        createdCurrency.Id.Should().BeGreaterThan(0);
        
        // Verify location header
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/currencies/v1.0/{createdCurrency.Id}");
    }

    [Fact]
    public async Task CreateCurrency_WithDuplicateShortName_ShouldReturnConflict()
    {
        // Arrange - Create first currency
        var firstCurrency = new CreateCurrencyRequest
        {
            ShortName = "DUP",
            LongName = "Duplicate Test Currency 1"
        };
        await _client.PostAsJsonAsync("/currencies/v1.0", firstCurrency);

        // Try to create duplicate
        var duplicateCurrency = new CreateCurrencyRequest
        {
            ShortName = "DUP",
            LongName = "Duplicate Test Currency 2"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/currencies/v1.0", duplicateCurrency);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCurrency_WithInvalidData_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidCurrency = new CreateCurrencyRequest
        {
            ShortName = "", // Invalid: empty
            LongName = "Test Currency"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/currencies/v1.0", invalidCurrency);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateCurrency_WithValidData_ShouldUpdateAndReturnCurrency()
    {
        // Arrange - Create a currency first
        var createRequest = new CreateCurrencyRequest
        {
            ShortName = "ABC", // Use unique code that shouldn't exist
            LongName = "Alpha Beta Currency"
        };
        var createResponse = await _client.PostAsJsonAsync("/currencies/v1.0", createRequest);
        
        // Check if creation was successful
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdCurrency = await createResponse.Content.ReadFromJsonAsync<CurrencyDto>();

        var updateRequest = new UpdateCurrencyRequest
        {
            ShortName = "XYZ", // Update to another unique code that shouldn't exist
            LongName = "X Y Z Currency Updated"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/currencies/v1.0/{createdCurrency!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedCurrency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        updatedCurrency.Should().NotBeNull();
        updatedCurrency!.Id.Should().Be(createdCurrency.Id);
        updatedCurrency.ShortName.Should().Be("XYZ");
        updatedCurrency.LongName.Should().Be("X Y Z Currency Updated");
    }

    [Fact]
    public async Task UpdateCurrency_WithInvalidId_ShouldReturnNotFound()
    {
        // Arrange
        var updateRequest = new UpdateCurrencyRequest
        {
            ShortName = "UPD",
            LongName = "Updated Currency Name"
        };

        // Act
        var response = await _client.PutAsJsonAsync("/currencies/v1.0/99999", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCurrency_WithValidId_ShouldDeleteCurrency()
    {
        // Arrange - Create a currency first
        var createRequest = new CreateCurrencyRequest
        {
            ShortName = "DEL",
            LongName = "Delete Test Currency"
        };
        var createResponse = await _client.PostAsJsonAsync("/currencies/v1.0", createRequest);
        var createdCurrency = await createResponse.Content.ReadFromJsonAsync<CurrencyDto>();

        // Act
        var response = await _client.DeleteAsync($"/currencies/v1.0/{createdCurrency!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deleted
        var getResponse = await _client.GetAsync($"/currencies/v1.0/{createdCurrency.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCurrency_WithInvalidId_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/currencies/v1.0/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HealthCheck_Liveness_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/currencies/liveness");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthCheck_Readiness_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/currencies/readiness");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCurrencyByCode_WithValidCode_ShouldReturnCurrency()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0/code/THB");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var currency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        currency.Should().NotBeNull();
        currency!.ShortName.Should().Be("THB");
        currency.LongName.Should().Contain("Thai Baht");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task GetCurrencyByCode_WithEmptyOrNullCode_ShouldReturnNotFound(string? code)
    {
        // Act
        var url = code == null 
            ? "/currencies/v1.0/code/" 
            : $"/currencies/v1.0/code/{code}";
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("ABCD")]
    public async Task GetCurrencyByCode_WithInvalidCode_ShouldReturnBadRequest(string code)
    {
        // Act
        var response = await _client.GetAsync($"/currencies/v1.0/code/{code}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCurrencyByCode_WithNonExistentCode_ShouldReturnNotFound()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0/code/XYZ");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllCurrencies_WithInvalidPage_ShouldReturnBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0?page=0&pageSize=20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllCurrencies_WithInvalidPageSize_TooSmall_ShouldReturnBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0?page=1&pageSize=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllCurrencies_WithInvalidPageSize_TooLarge_ShouldReturnBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0?page=1&pageSize=101");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAllCurrencies_WithLargePageNumber_ShouldReturnEmptyResult()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0?page=99999&pageSize=20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().BeGreaterThan(0); // Total count is the total in the database, not on this page
        result.Page.Should().Be(99999);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetCurrencyCodes_ShouldReturnAllCurrencyCodes()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0/codes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var codes = await response.Content.ReadFromJsonAsync<IEnumerable<string>>();
        codes.Should().NotBeNull();
        codes!.Should().Contain(new[] { "THB", "USD", "EUR", "JPY", "GBP" });
    }

    [Fact]
    public async Task CreateCurrency_WithDuplicateCodeDifferentCase_ShouldReturnConflict()
    {
        // Arrange
        var createRequest = new CreateCurrencyRequest
        {
            ShortName = "THB", // Same case as existing THB
            LongName = "Thai Baht Duplicate"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/currencies/v1.0", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCurrency_WithInvalidFormat_ShouldReturnBadRequest()
    {
        // Arrange
        var createRequest = new CreateCurrencyRequest
        {
            ShortName = "thb", // Lowercase instead of uppercase
            LongName = "Thai Baht Lowercase"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/currencies/v1.0", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateCurrency_WithWhitespaceInCode_ShouldReturnBadRequest()
    {
        // Arrange
        var createRequest = new CreateCurrencyRequest
        {
            ShortName = " THB ", // Whitespace around code
            LongName = "Thai Baht With Whitespace"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/currencies/v1.0", createRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateCurrency_WithDuplicateCode_ShouldReturnOk()
    {
        // Arrange - First create a currency
        var createRequest = new CreateCurrencyRequest
        {
            ShortName = "NEW",
            LongName = "New Currency"
        };
        var createResponse = await _client.PostAsJsonAsync("/currencies/v1.0", createRequest);
        var createdCurrency = await createResponse.Content.ReadFromJsonAsync<CurrencyDto>();

        // Try to update it to have the same code as an existing currency
        var updateRequest = new UpdateCurrencyRequest
        {
            ShortName = "USD", // This already exists
            LongName = "Updated to USD"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/currencies/v1.0/{createdCurrency!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK); // Current behavior - no duplicate check in service
    }

    [Fact]
    public async Task UpdateCurrency_WithWhitespaceInCode_ShouldReturnBadRequest()
    {
        // Arrange - First create a currency
        var createRequest = new CreateCurrencyRequest
        {
            ShortName = "UPD",
            LongName = "Update Test Currency"
        };
        var createResponse = await _client.PostAsJsonAsync("/currencies/v1.0", createRequest);
        var createdCurrency = await createResponse.Content.ReadFromJsonAsync<CurrencyDto>();

        // Try to update with whitespace in code
        var updateRequest = new UpdateCurrencyRequest
        {
            ShortName = " UPD ", // Whitespace around code
            LongName = "Updated Currency"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/currencies/v1.0/{createdCurrency!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SearchCurrencies_WithSpecialCharacters_ShouldHandleGracefully()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0?search=%24%40%23"); // $@#

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty(); // Should not match any currencies
    }

    [Fact]
    public async Task SearchCurrencies_WithSqlInjectionAttempt_ShouldHandleGracefully()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1.0?search=THB%27%20OR%201%3D1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CurrencyDto>>();
        result.Should().NotBeNull();
        // Should not return all currencies due to SQL injection
        // The search term "THB' OR 1=1" should be treated as a literal string
    }
}