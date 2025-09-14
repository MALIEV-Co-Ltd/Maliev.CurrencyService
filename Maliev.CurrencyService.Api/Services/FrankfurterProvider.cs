using System.Text.Json;
using Maliev.CurrencyService.Api.Models;
using Maliev.CurrencyService.Api.Models.ApiResponses;
using Microsoft.Extensions.Options;

namespace Maliev.CurrencyService.Api.Services;

public class FrankfurterProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;
    private readonly ExchangeRateOptions _options;
    private readonly ILogger<FrankfurterProvider> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public string Name => "Frankfurter";

    public FrankfurterProvider(HttpClient httpClient, IOptions<ExchangeRateOptions> options, ILogger<FrankfurterProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_options.FrankfurterApiUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<ExchangeRateDto?> GetExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"latest?from={fromCurrency}&to={toCurrency}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Frankfurter API returned {StatusCode} for {From} to {To}", 
                    response.StatusCode, fromCurrency, toCurrency);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var model = JsonSerializer.Deserialize<OpenRatesModel>(json, _jsonOptions);
            
            if (model?.Rates == null || !model.Rates.ContainsKey(toCurrency))
            {
                _logger.LogWarning("Frankfurter API response missing rate data for {From} to {To}", 
                    fromCurrency, toCurrency);
                return null;
            }

            var rate = model.Rates[toCurrency];
            var dateStr = model.Date;
            var fetchedAt = DateTime.Parse(dateStr ?? DateTime.UtcNow.ToString("yyyy-MM-dd"));

            return new ExchangeRateDto
            {
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                Rate = rate,
                FetchedAt = fetchedAt,
                Source = Name
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching exchange rate from Frankfurter: {From} to {To}", 
                fromCurrency, toCurrency);
            return null;
        }
    }

    public async Task<Dictionary<string, ExchangeRateDto>?> GetMultipleRatesAsync(string baseCurrency, IEnumerable<string> targetCurrencies, CancellationToken cancellationToken = default)
    {
        try
        {
            var targets = string.Join(",", targetCurrencies);
            var url = $"latest?from={baseCurrency}&to={targets}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Frankfurter API returned {StatusCode} for bulk request from {Base}", 
                    response.StatusCode, baseCurrency);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var model = JsonSerializer.Deserialize<OpenRatesModel>(json, _jsonOptions);
            
            if (model?.Rates == null)
            {
                _logger.LogWarning("Frankfurter API response missing rates data for bulk request from {Base}", baseCurrency);
                return null;
            }

            var dateStr = model.Date;
            var fetchedAt = DateTime.Parse(dateStr ?? DateTime.UtcNow.ToString("yyyy-MM-dd"));
            var result = new Dictionary<string, ExchangeRateDto>();

            foreach (var kvp in model.Rates)
            {
                result[kvp.Key] = new ExchangeRateDto
                {
                    FromCurrency = baseCurrency,
                    ToCurrency = kvp.Key,
                    Rate = kvp.Value,
                    FetchedAt = fetchedAt,
                    Source = Name
                };
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bulk exchange rates from Frankfurter for {Base}", baseCurrency);
            return null;
        }
    }
}