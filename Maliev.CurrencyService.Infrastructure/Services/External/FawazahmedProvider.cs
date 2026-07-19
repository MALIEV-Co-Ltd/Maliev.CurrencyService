using Maliev.CurrencyService.Application.Interfaces;
using Maliev.CurrencyService.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace Maliev.CurrencyService.Infrastructure.Services.External;

/// <summary>
/// Exchange rate provider using Fawazahmed Currency API.
/// </summary>
/// <remarks>
/// Primary provider with free, no-API-key access.
/// Base URL: https://latest.currency-api.pages.dev/v1/currencies/{from}.json
/// CDN-backed for low latency, daily updates, all currency pairs available.
/// </remarks>
public class FawazahmedProvider : IExchangeRateProvider
{
    private const string BaseUrl = "https://latest.currency-api.pages.dev/v1/currencies";
    private readonly HttpClient _httpClient;
    private readonly ILogger<FawazahmedProvider> _logger;
    private readonly IProviderMetrics _metrics;

    /// <summary>
    /// Gets the name of the exchange rate provider.
    /// </summary>
    public string ProviderName => "Fawazahmed";

    /// <summary>
    /// Initializes a new instance of the <see cref="FawazahmedProvider"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client instance.</param>
    /// <param name="logger">The logger for this provider.</param>
    /// <param name="metrics">The metrics service.</param>
    public FawazahmedProvider(
        HttpClient httpClient,
        ILogger<FawazahmedProvider> logger,
        IProviderMetrics metrics)
    {
        _httpClient = httpClient;
        _logger = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// Asynchronously retrieves the exchange rate between two currencies from the Fawazahmed API.
    /// </summary>
    /// <param name="fromCurrency">The source currency code (e.g., "USD").</param>
    /// <param name="toCurrency">The target currency code (e.g., "EUR").</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="ExchangeRate"/> object if the rate is found, otherwise null.</returns>
    public async Task<ExchangeRate?> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var from = fromCurrency.ToLower();
        var to = toCurrency.ToLower();

        try
        {
            _metrics.RecordProviderRequest(ProviderName, $"{fromCurrency}:{toCurrency}");

            var url = $"{BaseUrl}/{from}.json";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Fawazahmed API returned {StatusCode} for {From}:{To}",
                    response.StatusCode, fromCurrency, toCurrency);
                _metrics.RecordProviderError(ProviderName, "http_error");
                _metrics.RecordProviderCall(ProviderName, "error");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty(from, out var ratesElement))
            {
                _logger.LogWarning("Fawazahmed API returned unexpected format for {From}:{To}",
                    fromCurrency, toCurrency);
                _metrics.RecordProviderError(ProviderName, "parse_error");
                _metrics.RecordProviderCall(ProviderName, "error");
                return null;
            }

            if (!ratesElement.TryGetProperty(to, out var rateElement) || !rateElement.TryGetDecimal(out var rate))
            {
                _logger.LogDebug("Fawazahmed API does not have rate for {From}:{To}", fromCurrency, toCurrency);
                return null;
            }

            var now = DateTime.UtcNow;

            var exchangeRate = new ExchangeRate
            {
                Id = Guid.NewGuid(),
                FromCurrency = fromCurrency.ToUpper(),
                ToCurrency = toCurrency.ToUpper(),
                Rate = rate,
                Provider = ProviderName,
                IsTransitive = false,
                IntermediateCurrency = null,
                FetchedAt = now,
                ExpiresAt = now.AddSeconds(300),
                CreatedAt = now,
                UpdatedAt = now
            };

            stopwatch.Stop();
            _metrics.RecordProviderLatency(ProviderName, stopwatch.Elapsed.TotalSeconds);
            _metrics.RecordProviderCallDuration(ProviderName, stopwatch.Elapsed.TotalSeconds);
            _metrics.RecordProviderCall(ProviderName, "success");

            _logger.LogInformation("Fawazahmed API returned rate for {From}:{To} = {Rate} in {Elapsed}ms",
                fromCurrency, toCurrency, rate, stopwatch.ElapsedMilliseconds);

            return exchangeRate;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Fawazahmed API request failed for {From}:{To}", fromCurrency, toCurrency);
            _metrics.RecordProviderError(ProviderName, "network_error");
            _metrics.RecordProviderCall(ProviderName, "error");
            return null;
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Fawazahmed API JSON parsing failed for {From}:{To}", fromCurrency, toCurrency);
            _metrics.RecordProviderError(ProviderName, "parse_error");
            _metrics.RecordProviderCall(ProviderName, "error");
            return null;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Fawazahmed API unexpected error for {From}:{To}", fromCurrency, toCurrency);
            _metrics.RecordProviderError(ProviderName, "unknown_error");
            _metrics.RecordProviderCall(ProviderName, "error");
            return null;
        }
    }

    /// <summary>
    /// Indicates whether the provider supports the given currency pair.
    /// </summary>
    /// <param name="fromCurrency">The source currency code.</param>
    /// <param name="toCurrency">The target currency code.</param>
    /// <returns>True if the pair is supported, false otherwise.</returns>
    public bool SupportsPair(string fromCurrency, string toCurrency)
    {
        return true;
    }

    /// <summary>
    /// Retrieves a read-only set of supported currency codes from the provider.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A <see cref="Task"/> yielding a <see cref="IReadOnlySet{T}"/> of supported currency codes.</returns>
    public Task<IReadOnlySet<string>> GetSupportedCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetSupportedCurrenciesAsync not implemented for Fawazahmed (supports 200+ currencies)");
        return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }
}
