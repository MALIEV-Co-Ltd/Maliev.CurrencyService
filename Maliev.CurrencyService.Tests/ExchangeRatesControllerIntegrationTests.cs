using System.Net;
using System.Net.Http.Json;
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

public class ExchangeRatesControllerIntegrationTestFixture : IAsyncDisposable
{
    internal WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public string DatabaseName { get; } = $"TestDb_{Guid.NewGuid()}";

    public ExchangeRatesControllerIntegrationTestFixture()
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

        // Seed test data
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CurrencyDbContext>();
        SeedTestData(dbContext);
    }

    private static void SeedTestData(CurrencyDbContext context)
    {
        // Check if data already exists
        if (context.Currencies.Any())
            return;

        var currencies = new List<Currency>
        {
            new() { ShortName = "THB", LongName = "Thai Baht", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new() { ShortName = "USD", LongName = "US Dollar", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new() { ShortName = "EUR", LongName = "Euro", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new() { ShortName = "JPY", LongName = "Japanese Yen", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow },
            new() { ShortName = "GBP", LongName = "British Pound", CreatedDate = DateTime.UtcNow, ModifiedDate = DateTime.UtcNow }
        };

        context.Currencies.AddRange(currencies);
        context.SaveChanges();
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        Factory?.Dispose();
        GC.SuppressFinalize(this);
    }
}

[Collection("Sequential")]
public class ExchangeRatesControllerIntegrationTests : IClassFixture<ExchangeRatesControllerIntegrationTestFixture>
{
    private readonly HttpClient _client;

    public ExchangeRatesControllerIntegrationTests(ExchangeRatesControllerIntegrationTestFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetExchangeRate_WithValidCurrencies_ReturnsExchangeRate()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "THB";

        // Act
        var response = await _client.GetAsync($"/currencies/v1/exchange-rates?from={fromCurrency}&to={toCurrency}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var exchangeRate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
        exchangeRate.Should().NotBeNull();
        exchangeRate!.FromCurrency.Should().Be(fromCurrency);
        exchangeRate.ToCurrency.Should().Be(toCurrency);
        exchangeRate.Rate.Should().BeGreaterThan(0);
        exchangeRate.Source.Should().NotBeNullOrWhiteSpace();
        exchangeRate.FetchedAt.Should().BeAfter(DateTime.UtcNow.AddMonths(-3)); // Within last 3 months (accounting for test data)
    }

    [Theory]
    [InlineData("", "USD")]
    [InlineData("USD", "")]
    [InlineData(null, "USD")]
    [InlineData("USD", null)]
    public async Task GetExchangeRate_WithMissingCurrencies_ReturnsBadRequest(string? from, string? to)
    {
        // Arrange
        var queryString = "";
        if (from != null)
            queryString += $"from={from}";
        if (to != null)
            queryString += $"{(queryString.Length > 0 ? "&" : "")}to={to}";

        // Act
        var response = await _client.GetAsync($"/currencies/v1/exchange-rates?{queryString}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("US", "USD")]
    [InlineData("USD", "US")]
    [InlineData("USDD", "EUR")]
    [InlineData("EUR", "USDD")]
    public async Task GetExchangeRate_WithInvalidCurrencyLength_ReturnsBadRequest(string from, string to)
    {
        // Act
        var response = await _client.GetAsync($"/currencies/v1/exchange-rates?from={from}&to={to}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMultipleExchangeRates_WithValidCurrencies_ReturnsExchangeRates()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrencies = "THB,EUR,JPY";

        // Act
        var response = await _client.GetAsync($"/currencies/v1/exchange-rates/bulk?from={fromCurrency}&to={toCurrencies}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var exchangeRates = await response.Content.ReadFromJsonAsync<Dictionary<string, ExchangeRateDto>>();
        exchangeRates.Should().NotBeNull();
        exchangeRates!.Count.Should().BeGreaterThan(0);
        
        foreach (var (currency, rate) in exchangeRates)
        {
            rate.FromCurrency.Should().Be(fromCurrency);
            rate.ToCurrency.Should().Be(currency);
            rate.Rate.Should().BeGreaterThan(0);
            rate.Source.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData("", "USD,EUR")]
    [InlineData("USD", "")]
    [InlineData(null, "USD,EUR")]
    [InlineData("USD", null)]
    public async Task GetMultipleExchangeRates_WithMissingParameters_ReturnsBadRequest(string? from, string? to)
    {
        // Arrange
        var queryString = "";
        if (from != null)
            queryString += $"from={from}";
        if (to != null)
            queryString += $"{(queryString.Length > 0 ? "&" : "")}to={to}";

        // Act
        var response = await _client.GetAsync($"/currencies/v1/exchange-rates/bulk?{queryString}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMultipleExchangeRates_WithInvalidBaseCurrency_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/currencies/v1/exchange-rates/bulk?from=US&to=EUR,JPY");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMultipleExchangeRates_WithTooManyTargetCurrencies_ReturnsBadRequest()
    {
        // Arrange
        // Create 25 valid 3-character currency codes to exceed the 20 limit
        var tooManyCurrencies = string.Join(",", Enumerable.Range(1, 25).Select(i => $"C{i:D2}"));

        // Act
        var response = await _client.GetAsync($"/currencies/v1/exchange-rates/bulk?from=USD&to={tooManyCurrencies}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetExchangeRateByPath_WithValidCurrencies_ReturnsExchangeRate()
    {
        // Arrange
        var fromCurrency = "USD";
        var toCurrency = "THB";

        // Act
        var response = await _client.GetAsync($"/currencies/v1/exchange-rates/{fromCurrency}/{toCurrency}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var exchangeRate = await response.Content.ReadFromJsonAsync<ExchangeRateDto>();
        exchangeRate.Should().NotBeNull();
        exchangeRate!.FromCurrency.Should().Be(fromCurrency);
        exchangeRate.ToCurrency.Should().Be(toCurrency);
        exchangeRate.Rate.Should().BeGreaterThan(0);
        exchangeRate.Source.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("US", "USD")]
    [InlineData("USD", "US")]
    [InlineData("USDD", "EUR")]
    [InlineData("EUR", "USDD")]
    public async Task GetExchangeRateByPath_WithInvalidCurrencyLength_ReturnsBadRequest(string from, string to)
    {
        // Act
        var response = await _client.GetAsync($"/currencies/v1/exchange-rates/{from}/{to}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConvertCurrency_WithValidRequest_ReturnsConversionResult()
    {
        // Arrange
        var request = new ConvertCurrencyRequest
        {
            From = "USD",
            To = "THB",
            Amount = 100
        };

        // Act
        var response = await _client.PostAsJsonAsync("/currencies/v1/exchange-rates/convert", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ConvertCurrencyResponse>();
        result.Should().NotBeNull();
        result!.FromCurrency.Should().Be(request.From);
        result.ToCurrency.Should().Be(request.To);
        result.OriginalAmount.Should().Be(request.Amount);
        result.ConvertedAmount.Should().BeGreaterThan(0);
        result.ExchangeRate.Should().BeGreaterThan(0);
        result.RateTimestamp.Should().BeAfter(DateTime.UtcNow.AddMonths(-3)); // Within last 3 months (accounting for test data)
        result.Source.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ConvertCurrency_WithInvalidModel_ReturnsBadRequest()
    {
        // Arrange
        var request = new ConvertCurrencyRequest
        {
            From = "US", // Invalid length
            To = "THB",
            Amount = 100
        };

        // Act
        var response = await _client.PostAsJsonAsync("/currencies/v1/exchange-rates/convert", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConvertCurrency_WithZeroAmount_ReturnsBadRequest()
    {
        // Arrange
        var request = new ConvertCurrencyRequest
        {
            From = "USD",
            To = "THB",
            Amount = 0 // Zero amount should be rejected by model validation
        };

        // Act
        var response = await _client.PostAsJsonAsync("/currencies/v1/exchange-rates/convert", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConvertCurrency_WithNegativeAmount_ReturnsBadRequest()
    {
        // Arrange
        var request = new ConvertCurrencyRequest
        {
            From = "USD",
            To = "THB",
            Amount = -100 // Negative amount should be rejected by model validation
        };

        // Act
        var response = await _client.PostAsJsonAsync("/currencies/v1/exchange-rates/convert", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}