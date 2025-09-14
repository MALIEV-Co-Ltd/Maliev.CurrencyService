using System.Text.Json;

namespace Maliev.CurrencyService.Api.Services.Clients;

public class FawazahmedApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public FawazahmedApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<Dictionary<string, object>?> GetCurrencyRatesAsync(string currencyCode, CancellationToken cancellationToken = default)
    {
        var url = $"currencies/{currencyCode.ToLowerInvariant()}.json";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, _jsonOptions);
    }
}