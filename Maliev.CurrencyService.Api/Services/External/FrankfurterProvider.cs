using Maliev.CurrencyService.Api.Metrics;
using Maliev.CurrencyService.Data.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maliev.CurrencyService.Api.Services.External;

/// <summary>
/// Exchange rate provider using Frankfurter API
/// </summary>
/// <remarks>
/// Per research.md decision 1: Fallback provider with ECB (European Central Bank) rates.
/// Base URL: https://api.frankfurter.app/latest?from={from}&to={to}
/// Daily ECB rates, limited to 30-40 major fiat currencies.
/// </remarks>
public class FrankfurterProvider : IExchangeRateProvider
{
    private const string BaseUrl = "https://api.frankfurter.app";
    private readonly HttpClient _httpClient;
    private readonly ILogger<FrankfurterProvider> _logger;
    private readonly CurrencyServiceMetrics _metrics;

    // Frankfurter supports ~30-40 major fiat currencies
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "AUD", "BGN", "BRL", "CAD", "CHF", "CNY", "CZK", "DKK", "EUR", "GBP",
        "HKD", "HRK", "HUF", "IDR", "ILS", "INR", "ISK", "JPY", "KRW", "MXN",
        "MYR", "NOK", "NZD", "PHP", "PLN", "RON", "RUB", "SEK", "SGD", "THB",
        "TRY", "USD", "ZAR"
    };

    public string ProviderName => "Frankfurter";

    public FrankfurterProvider(
        HttpClient httpClient,
        ILogger<FrankfurterProvider> logger,
        CurrencyServiceMetrics metrics)
    {
        _httpClient = httpClient;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<ExchangeRate?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Check if pair is supported before making API call
        if (!SupportsPair(fromCurrency, toCurrency))
        {
            _logger.LogDebug("Frankfurter does not support pair {From}:{To}",
                fromCurrency, toCurrency);
            return null;
        }

        try
        {
            _metrics.ProviderRequests.WithLabels(ProviderName, $"{fromCurrency}:{toCurrency}").Inc();

            var url = $"{BaseUrl}/latest?from={fromCurrency.ToUpper()}&to={toCurrency.ToUpper()}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Frankfurter API returned {StatusCode} for {From}:{To}",
                    response.StatusCode, fromCurrency, toCurrency);
                _metrics.ProviderErrors.WithLabels(ProviderName, "http_error").Inc();
                _metrics.ProviderCalls.WithLabels(ProviderName, "error").Inc();
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<FrankfurterResponse>(json);

            if (data == null || data.Rates == null || !data.Rates.ContainsKey(toCurrency.ToUpper()))
            {
                _logger.LogWarning("Frankfurter API returned unexpected format or missing rate for {From}:{To}",
                    fromCurrency, toCurrency);
                _metrics.ProviderErrors.WithLabels(ProviderName, "parse_error").Inc();
                _metrics.ProviderCalls.WithLabels(ProviderName, "error").Inc();
                return null;
            }

            var rate = data.Rates[toCurrency.ToUpper()];
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

            _logger.LogInformation("Frankfurter API returned rate for {From}:{To} = {Rate} in {Elapsed}ms",
                fromCurrency, toCurrency, rate, stopwatch.ElapsedMilliseconds);

            return exchangeRate;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Frankfurter API request failed for {From}:{To}",
                fromCurrency, toCurrency);
            _metrics.ProviderErrors.WithLabels(ProviderName, "network_error").Inc();
            _metrics.ProviderCalls.WithLabels(ProviderName, "error").Inc();
            return null;
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Frankfurter API JSON parsing failed for {From}:{To}",
                fromCurrency, toCurrency);
            _metrics.ProviderErrors.WithLabels(ProviderName, "parse_error").Inc();
            _metrics.ProviderCalls.WithLabels(ProviderName, "error").Inc();
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Frankfurter API unexpected error for {From}:{To}",
                fromCurrency, toCurrency);
            _metrics.ProviderErrors.WithLabels(ProviderName, "unknown_error").Inc();
            _metrics.ProviderCalls.WithLabels(ProviderName, "error").Inc();
            return null;
        }
    }

    public bool SupportsPair(string fromCurrency, string toCurrency)
    {
        return SupportedCurrencies.Contains(fromCurrency) &&
               SupportedCurrencies.Contains(toCurrency);
    }

    public Task<IReadOnlySet<string>> GetSupportedCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlySet<string>>(SupportedCurrencies);
    }

    private class FrankfurterResponse
    {
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("base")]
        public string Base { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("rates")]
        public Dictionary<string, decimal> Rates { get; set; } = new();
    }
}
