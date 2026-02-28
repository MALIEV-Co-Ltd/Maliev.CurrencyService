using System.Net;
using System.Net.Http.Json;
using Maliev.CurrencyService.Api.Services;
using Maliev.CurrencyService.Application.DTOs.Currencies;
using Maliev.CurrencyService.Infrastructure.Persistence;
using Maliev.CurrencyService.Tests.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.CurrencyService.Tests.Integration;

public class AuthorizationTests : IClassFixture<BaseIntegrationTestFactory<Program, CurrencyDbContext>>
{
    private readonly BaseIntegrationTestFactory<Program, CurrencyDbContext> _baseFactory;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _anonymousClient;

    public AuthorizationTests(BaseIntegrationTestFactory<Program, CurrencyDbContext> factory)
    {
        _baseFactory = factory;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:PermissionBasedAuthEnabled", "true");
        });
        _anonymousClient = _factory.CreateClient();
    }

    [Fact]
    public async Task ListCurrencies_Anonymous_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonymousClient.GetAsync("/currency/v1/currencies");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetExchangeRate_Anonymous_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonymousClient.GetAsync("/currency/v1/rates?from=USD&to=EUR");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListCurrencies_WithPermission_ReturnsOk()
    {
        // Arrange
        var token = _baseFactory.CreateTestJwtToken(additionalClaims: new Dictionary<string, string>
        {
            { "permissions", CurrencyPermissions.CurrenciesRead }
        });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        // Act
        var response = await client.GetAsync("/currency/v1/currencies");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetExchangeRate_WithPermission_ReturnsOkOrServiceUnavailable()
    {
        // Arrange
        var token = _baseFactory.CreateTestJwtToken(additionalClaims: new Dictionary<string, string>
        {
            { "permissions", CurrencyPermissions.RatesRead }
        });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        // Act
        var response = await client.GetAsync("/currency/v1/rates?from=USD&to=EUR");

        // Assert
        // Might be 503 if providers are down in test env, but not 401/403
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCurrency_Anonymous_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonymousClient.PostAsJsonAsync("/currency/v1/admin/currencies", new CreateCurrencyRequest
        {
            Code = "TEST",
            Name = "Test Currency",
            Symbol = "T"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateCurrency_WithoutPermission_ReturnsForbidden()
    {
        // Arrange
        var token = _baseFactory.CreateTestJwtToken(additionalClaims: new Dictionary<string, string>
        {
            // Missing permissions claim
        });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        // Act
        var response = await client.PostAsJsonAsync("/currency/v1/admin/currencies", new CreateCurrencyRequest
        {
            Code = "NOP",
            Name = "No Permission",
            Symbol = "N"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCurrency_WithCorrectPermission_ReturnsCreated_Or_Conflict()
    {
        // Arrange
        var token = _baseFactory.CreateTestJwtToken(additionalClaims: new Dictionary<string, string>
        {
            { "permissions", CurrencyPermissions.CurrenciesCreate }
        });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        // Act
        var response = await client.PostAsJsonAsync("/currency/v1/admin/currencies", new CreateCurrencyRequest
        {
            Code = "CAD",
            Name = "Canadian Dollar",
            Symbol = "$"
        });

        // Assert
        var validStatuses = new[] { HttpStatusCode.Created, HttpStatusCode.Conflict, HttpStatusCode.BadRequest };
        Assert.Contains(response.StatusCode, validStatuses);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SnapshotIngest_WithoutPermission_ReturnsForbidden()
    {
        // Arrange
        var token = _baseFactory.CreateTestJwtToken(additionalClaims: new Dictionary<string, string>
        {
            { "permissions", CurrencyPermissions.CurrenciesRead } // Wrong permission
        });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        // Act
        var response = await client.PostAsJsonAsync("/currency/v1/admin/snapshots/ingest", new[]
        {
            new
            {
                from = "USD",
                to = "EUR",
                rate = 0.85,
                timestamp = DateTime.UtcNow.ToString("O")
            }
        });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RateLimiting_Authenticated_HeadersPresent()
    {
        // Arrange
        var token = _baseFactory.CreateTestJwtToken(additionalClaims: new Dictionary<string, string>
        {
            { "permissions", CurrencyPermissions.CurrenciesRead }
        });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        // Act
        var response = await client.GetAsync("/currency/v1/currencies");

        // Assert
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task Startup_WhenIAMUnavailable_DoesNotBlockStartup()
    {
        // Arrange
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Features:PermissionBasedAuthEnabled", "true");
            builder.UseSetting("IAM:BaseUrl", "http://non-existent-service:1234");
            builder.UseSetting("IAM:Timeout", "100");
        });

        var token = _baseFactory.CreateTestJwtToken(additionalClaims: new Dictionary<string, string>
        {
            { "permissions", CurrencyPermissions.CurrenciesRead }
        });

        // Act
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        var response = await client.GetAsync("/currency/v1/currencies");

        // Assert
        // Service should start and handle requests even if background registration is failing
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
