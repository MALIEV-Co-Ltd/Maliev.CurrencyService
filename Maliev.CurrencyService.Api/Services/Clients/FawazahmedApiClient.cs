using System.Text.Json;

namespace Maliev.CurrencyService.Api.Services.Clients;

/// <summary>
/// API client for the Fawazahmed currency API.
/// </summary>
public class FawazahmedApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FawazahmedApiClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client instance.</param>
    public FawazahmedApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Retrieves currency rates for a given currency code.
    /// </summary>
    /// <param name="currencyCode">The currency code (e.g., "usd").</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A dictionary of currency rates, or null if the request fails.</returns>
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
