using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Data.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Maliev.CurrencyService.Tests;

public class CurrencyControllerIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public CurrencyControllerIntegrationTests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("currency_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", _postgresContainer.GetConnectionString());
            
            // Override authentication for testing
            builder.ConfigureServices(services =>
            {
                // Remove the existing authentication
                var authDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Microsoft.AspNetCore.Authentication.IAuthenticationService));
                if (authDescriptor != null)
                {
                    services.Remove(authDescriptor);
                }
            });
        });

        _client = _factory.CreateClient();
        
        // Run migrations
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Maliev.CurrencyService.Data.DbContexts.CurrencyDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        _client?.Dispose();
        await _factory.DisposeAsync();
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
        result.TotalCount.Should().BeGreaterThan(150); // We seeded 153 currencies
        
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
        result!.Items.Should().ContainSingle();
        result.Items.First().ShortName.Should().Be("USD");
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
            ShortName = "UPD",
            LongName = "Update Test Currency"
        };
        var createResponse = await _client.PostAsJsonAsync("/currencies/v1.0", createRequest);
        var createdCurrency = await createResponse.Content.ReadFromJsonAsync<CurrencyDto>();

        var updateRequest = new UpdateCurrencyRequest
        {
            ShortName = "UPD",
            LongName = "Updated Test Currency Name"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/currencies/v1.0/{createdCurrency!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedCurrency = await response.Content.ReadFromJsonAsync<CurrencyDto>();
        updatedCurrency.Should().NotBeNull();
        updatedCurrency!.Id.Should().Be(createdCurrency.Id);
        updatedCurrency.ShortName.Should().Be("UPD");
        updatedCurrency.LongName.Should().Be("Updated Test Currency Name");
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
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Healthy");
    }

    [Fact]
    public async Task HealthCheck_Readiness_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/currencies/readiness");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("v1.0")]
    [InlineData("v1")]
    public async Task ApiVersioning_ShouldWorkWithDifferentVersionFormats(string version)
    {
        // Act
        var response = await _client.GetAsync($"/currencies/{version}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}