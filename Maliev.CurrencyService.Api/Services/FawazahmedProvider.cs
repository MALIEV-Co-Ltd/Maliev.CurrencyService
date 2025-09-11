using System.Text.Json;
using Maliev.CurrencyService.Api.Models;
using Microsoft.Extensions.Options;

namespace Maliev.CurrencyService.Api.Services;

public class FawazahmedProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ExchangeRateOptions _options;
    private readonly ILogger<FawazahmedProvider> _logger;

    public string Name => "Fawazahmed";

    public FawazahmedProvider(HttpClient httpClient, IOptions<ExchangeRateOptions> options, ILogger<FawazahmedProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/");
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<ExchangeRateDto?> GetExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        try
        {
            var fromLower = fromCurrency.ToLowerInvariant();
            var toLower = toCurrency.ToLowerInvariant();
            var url = $"currencies/{fromLower}.json";

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Fawazahmed API returned {StatusCode} for {From} to {To}", 
                    response.StatusCode, fromCurrency, toCurrency);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("date", out var dateElement) ||
                !root.TryGetProperty(fromLower, out var currencyRates) ||
                !currencyRates.TryGetProperty(toLower, out var rateElement))
            {
                _logger.LogWarning("Fawazahmed API response missing rate data for {From} to {To}", 
                    fromCurrency, toCurrency);
                return null;
            }

            var rate = rateElement.GetDecimal();
            var dateStr = dateElement.GetString();
            var fetchedAt = DateTime.Parse(dateStr ?? DateTime.UtcNow.ToString("yyyy-MM-dd"));

            return new ExchangeRateDto
            {
                FromCurrency = fromCurrency.ToUpperInvariant(),
                ToCurrency = toCurrency.ToUpperInvariant(),
                Rate = rate,
                FetchedAt = fetchedAt,
                Source = Name
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching exchange rate from Fawazahmed: {From} to {To}", 
                fromCurrency, toCurrency);
            return null;
        }
    }

    public async Task<Dictionary<string, ExchangeRateDto>?> GetMultipleRatesAsync(string baseCurrency, IEnumerable<string> targetCurrencies, CancellationToken cancellationToken = default)
    {
        try
        {
            var baseLower = baseCurrency.ToLowerInvariant();
            var url = $"currencies/{baseLower}.json";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Fawazahmed API returned {StatusCode} for bulk request from {Base}", 
                    response.StatusCode, baseCurrency);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("date", out var dateElement) ||
                !root.TryGetProperty(baseLower, out var rates))
            {
                _logger.LogWarning("Fawazahmed API response missing rates data for bulk request from {Base}", baseCurrency);
                return null;
            }

            var dateStr = dateElement.GetString();
            var fetchedAt = DateTime.Parse(dateStr ?? DateTime.UtcNow.ToString("yyyy-MM-dd"));
            var result = new Dictionary<string, ExchangeRateDto>();

            foreach (var target in targetCurrencies)
            {
                var targetLower = target.ToLowerInvariant();
                if (rates.TryGetProperty(targetLower, out var rateElement))
                {
                    result[target.ToUpperInvariant()] = new ExchangeRateDto
                    {
                        FromCurrency = baseCurrency.ToUpperInvariant(),
                        ToCurrency = target.ToUpperInvariant(),
                        Rate = rateElement.GetDecimal(),
                        FetchedAt = fetchedAt,
                        Source = Name
                    };
                }
            }

            return result.Any() ? result : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bulk exchange rates from Fawazahmed for {Base}", baseCurrency);
            return null;
        }
    }
}