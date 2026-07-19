using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maliev.CurrencyService.Infrastructure.Services.External;

/// <summary>
/// Exchange rate provider using Frankfurter API.
/// </summary>
/// <remarks>
/// Fallback provider with ECB (European Central Bank) rates.
/// Base URL: https://api.frankfurter.app/latest?from={from}&amp;to={to}
/// Daily ECB rates, limited to 30-40 major fiat currencies.
/// </remarks>
public class FrankfurterProvider : IExchangeRateProvider
{
    private const string BaseUrl = "https://api.frankfurter.app";
    private readonly HttpClient _httpClient;
    private readonly ILogger<FrankfurterProvider> _logger;
    private readonly IProviderMetrics _metrics;

    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "AUD", "BGN", "BRL", "CAD", "CHF", "CNY", "CZK", "DKK", "EUR", "GBP",
        "HKD", "HRK", "HUF", "IDR", "ILS", "INR", "ISK", "JPY", "KRW", "MXN",
        "MYR", "NOK", "NZD", "PHP", "PLN", "RON", "RUB", "SEK", "SGD", "THB",
        "TRY", "USD", "ZAR"
    };

    /// <summary>
    /// Gets the name of the exchange rate provider.
    /// </summary>
    public string ProviderName => "Frankfurter";

    /// <summary>
    /// Initializes a new instance of the <see cref="FrankfurterProvider"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client instance.</param>
    /// <param name="logger">The logger for this provider.</param>
    /// <param name="metrics">The metrics service.</param>
    public FrankfurterProvider(
        HttpClient httpClient,
        ILogger<FrankfurterProvider> logger,
        IProviderMetrics metrics)
    {
        _httpClient = httpClient;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Asynchronously retrieves the exchange rate between two currencies from the Frankfurter API.
    /// </summary>
    /// <param name="fromCurrency">The source currency code (e.g., "USD").</param>
    /// <param name="toCurrency">The target currency code (e.g., "EUR").</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="ExchangeRate"/> object if the rate is found, otherwise null.</returns>
    public async Task<ExchangeRate?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!SupportsPair(fromCurrency, toCurrency))
        {
            _logger.LogDebug("Frankfurter does not support pair {From}:{To}", fromCurrency, toCurrency);
            return null;
        }

        try
        {
            _metrics.RecordProviderRequest(ProviderName, $"{fromCurrency}:{toCurrency}");

            var url = $"{BaseUrl}/latest?from={fromCurrency.ToUpper()}&to={toCurrency.ToUpper()}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Frankfurter API returned {StatusCode} for {From}:{To}",
                    response.StatusCode, fromCurrency, toCurrency);
                _metrics.RecordProviderError(ProviderName, "http_error");
                _metrics.RecordProviderCall(ProviderName, "error");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<FrankfurterResponse>(json);

            if (data == null || data.Rates == null || !data.Rates.ContainsKey(toCurrency.ToUpper()))
            {
                _logger.LogWarning("Frankfurter API returned unexpected format or missing rate for {From}:{To}",
                    fromCurrency, toCurrency);
                _metrics.RecordProviderError(ProviderName, "parse_error");
                _metrics.RecordProviderCall(ProviderName, "error");
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
                ExpiresAt = now.AddSeconds(300),
                CreatedAt = now,
                UpdatedAt = now
            };

            stopwatch.Stop();
            _metrics.RecordProviderLatency(ProviderName, stopwatch.Elapsed.TotalSeconds);
            _metrics.RecordProviderCallDuration(ProviderName, stopwatch.Elapsed.TotalSeconds);
            _metrics.RecordProviderCall(ProviderName, "success");

            _logger.LogInformation("Frankfurter API returned rate for {From}:{To} = {Rate} in {Elapsed}ms",
                fromCurrency, toCurrency, rate, stopwatch.ElapsedMilliseconds);

            return exchangeRate;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Frankfurter API request failed for {From}:{To}", fromCurrency, toCurrency);
            _metrics.RecordProviderError(ProviderName, "network_error");
            _metrics.RecordProviderCall(ProviderName, "error");
            return null;
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Frankfurter API JSON parsing failed for {From}:{To}", fromCurrency, toCurrency);
            _metrics.RecordProviderError(ProviderName, "parse_error");
            _metrics.RecordProviderCall(ProviderName, "error");
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Frankfurter API unexpected error for {From}:{To}", fromCurrency, toCurrency);
            _metrics.RecordProviderError(ProviderName, "unknown_error");
            _metrics.RecordProviderCall(ProviderName, "error");
            return null;
        }
    }

    /// <summary>
    /// Checks if the Frankfurter API supports the given currency pair.
    /// </summary>
    /// <param name="fromCurrency">The source currency code.</param>
    /// <param name="toCurrency">The target currency code.</param>
    /// <returns>True if the pair is supported, false otherwise.</returns>
    public bool SupportsPair(string fromCurrency, string toCurrency)
    {
        return SupportedCurrencies.Contains(fromCurrency) &&
               SupportedCurrencies.Contains(toCurrency);
    }

    /// <summary>
    /// Retrieves a read-only set of supported currency codes from the Frankfurter API.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> yielding a <see cref="IReadOnlySet{T}"/> of supported currency codes.</returns>
    public Task<IReadOnlySet<string>> GetSupportedCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlySet<string>>(SupportedCurrencies);
    }

    /// <summary>
    /// Represents the response structure from the Frankfurter API.
    /// </summary>
    private class FrankfurterResponse
    {
        /// <summary>
        /// The amount used in the conversion (always 1 for latest rates).
        /// </summary>
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        /// <summary>
        /// The base currency of the exchange rates.
        /// </summary>
        [JsonPropertyName("base")]
        public string Base { get; set; } = string.Empty;

        /// <summary>
        /// The date for which the exchange rates are valid.
        /// </summary>
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        /// <summary>
        /// A dictionary of exchange rates where the key is the target currency code and the value is the rate.
        /// </summary>
        [JsonPropertyName("rates")]
        public Dictionary<string, decimal> Rates { get; set; } = new();
    }
}
