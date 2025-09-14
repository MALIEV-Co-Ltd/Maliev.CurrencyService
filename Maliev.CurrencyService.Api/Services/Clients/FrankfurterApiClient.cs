using Maliev.CurrencyService.Api.Models.ApiResponses;
using System.Text.Json;

namespace Maliev.CurrencyService.Api.Services.Clients;

public class FrankfurterApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public FrankfurterApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

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