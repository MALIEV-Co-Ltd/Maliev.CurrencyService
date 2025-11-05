using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Data.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maliev.CurrencyService.Api.Services.External;

/// <summary>
/// Exchange rate provider using Fawazahmed Currency API
/// </summary>
/// <remarks>
/// Per research.md decision 1: Primary provider with free, no-API-key access.
/// Base URL: https://latest.currency-api.pages.dev/v1/currencies/{from}.json
/// CDN-backed for low latency, daily updates, all currency pairs available.
/// </remarks>
public class FawazahmedProvider : IExchangeRateProvider
{
    private const string BaseUrl = "https://latest.currency-api.pages.dev/v1/currencies";
    private readonly HttpClient _httpClient;
    private readonly ILogger<FawazahmedProvider> _logger;
    private readonly CurrencyServiceMetrics _metrics;

    public string ProviderName => "Fawazahmed";

    public FawazahmedProvider(
        HttpClient httpClient,
        ILogger<FawazahmedProvider> logger,
        CurrencyServiceMetrics metrics)
    {
        _httpClient = httpClient;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<ExchangeRate?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var from = fromCurrency.ToLower();
        var to = toCurrency.ToLower();

        try
        {
            _metrics.ProviderRequests.WithLabels(ProviderName, $"{fromCurrency}:{toCurrency}").Inc();

            var url = $"{BaseUrl}/{from}.json";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Fawazahmed API returned {StatusCode} for {From}:{To}",
                    response.StatusCode, fromCurrency, toCurrency);
                _metrics.ProviderErrors.WithLabels(ProviderName, "http_error").Inc();
                _metrics.ProviderCalls.WithLabels(ProviderName, "error").Inc();
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, decimal>>>(json);

            if (data == null || !data.ContainsKey(from))
            {
                _logger.LogWarning("Fawazahmed API returned unexpected format for {From}:{To}",
                    fromCurrency, toCurrency);
                _metrics.ProviderErrors.WithLabels(ProviderName, "parse_error").Inc();
                _metrics.ProviderCalls.WithLabels(ProviderName, "error").Inc();
                return null;
            }

            var rates = data[from];
            if (!rates.ContainsKey(to))
            {
                _logger.LogDebug("Fawazahmed API does not have rate for {From}:{To}",
                    fromCurrency, toCurrency);
                return null;
            }

            var rate = rates[to];
            var now = DateTime.UtcNow;

            var exchangeRate = new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = fromCurrency.ToUpper(),
                ToCurrency = toCurrency.ToUpper(),
                Rate = rate,
                Provider = ProviderName,
                IsTransitive = false,
                FetchedAt = now,
                ExpiresAt = now.AddSeconds(300), // 5 minutes TTL per research.md
                CreatedAt = now,
                UpdatedAt = now
            };

            stopwatch.Stop();
            _metrics.ProviderLatency.WithLabels(ProviderName).Observe(stopwatch.Elapsed.TotalSeconds);
            _metrics.ProviderCallDuration.WithLabels(ProviderName).Observe(stopwatch.Elapsed.TotalSeconds);
            _metrics.ProviderCalls.WithLabels(ProviderName, "success").Inc();

            _logger.LogInformation("Fawazahmed API returned rate for {From}:{To} = {Rate} in {Elapsed}ms",
                fromCurrency, toCurrency, rate, stopwatch.ElapsedMilliseconds);

            return exchangeRate;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Fawazahmed API request failed for {From}:{To}",
                fromCurrency, toCurrency);
            _metrics.ProviderErrors.WithLabels(ProviderName, "network_error").Inc();
            _metrics.ProviderCalls.WithLabels(ProviderName, "error").Inc();
            return null;
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Fawazahmed API JSON parsing failed for {From}:{To}",
                fromCurrency, toCurrency);
            _metrics.ProviderErrors.WithLabels(ProviderName, "parse_error").Inc();
            _metrics.ProviderCalls.WithLabels(ProviderName, "error").Inc();
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Fawazahmed API unexpected error for {From}:{To}",
                fromCurrency, toCurrency);
            _metrics.ProviderErrors.WithLabels(ProviderName, "unknown_error").Inc();
            _metrics.ProviderCalls.WithLabels(ProviderName, "error").Inc();
            return null;
        }
    }

    public bool SupportsPair(string fromCurrency, string toCurrency)
    {
        // Fawazahmed supports all currency pairs (200+ currencies)
        // We'll return true optimistically and let the API call determine availability
        return true;
    }

    public async Task<IReadOnlySet<string>> GetSupportedCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        // Fawazahmed supports 200+ currencies but doesn't provide a /currencies endpoint
        // Return empty set - actual support is determined at runtime via API calls
        _logger.LogDebug("GetSupportedCurrenciesAsync not implemented for Fawazahmed (supports 200+ currencies)");
        return new HashSet<string>();
    }
}
