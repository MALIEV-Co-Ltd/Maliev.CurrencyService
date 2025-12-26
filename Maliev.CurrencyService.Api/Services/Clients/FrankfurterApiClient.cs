using Maliev.CurrencyService.Api.Models.ApiResponses;
using System.Text.Json;

namespace Maliev.CurrencyService.Api.Services.Clients;

/// <summary>
/// API client for the Frankfurter currency API.
/// </summary>
public class FrankfurterApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrankfurterApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client instance.</param>
    public FrankfurterApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Retrieves the latest exchange rate between two currencies.
    /// </summary>
    /// <param name="fromCurrency">The source currency code (ISO 4217).</param>
    /// <param name="toCurrency">The target currency code (ISO 4217).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="OpenRatesModel"/> containing the exchange rate, or null if the request fails.</returns>
    public async Task<OpenRatesModel?> GetLatestRatesAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        var url = $"latest?from={fromCurrency}&to={toCurrency}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<OpenRatesModel>(json, _jsonOptions);
    }

    /// <summary>
    /// Retrieves the latest exchange rates from a base currency to multiple target currencies.
    /// </summary>
    /// <param name="fromCurrency">The base currency code (ISO 4217).</param>
    /// <param name="toCurrencies">A collection of target currency codes (ISO 4217).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="OpenRatesModel"/> containing the exchange rates, or null if the request fails.</returns>
    public async Task<OpenRatesModel?> GetLatestRatesAsync(string fromCurrency, IEnumerable<string> toCurrencies, CancellationToken cancellationToken = default)
    {
        var targets = string.Join(",", toCurrencies);
        var url = $"latest?from={fromCurrency}&to={targets}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<OpenRatesModel>(json, _jsonOptions);
    }
}
