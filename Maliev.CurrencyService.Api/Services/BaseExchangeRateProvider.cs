using System.Text.Json;
using Maliev.CurrencyService.Api.Models;
using Microsoft.Extensions.Options;

namespace Maliev.CurrencyService.Api.Services;

public abstract class BaseExchangeRateProvider : IExchangeRateProvider
{
    protected readonly HttpClient _httpClient;
    protected readonly ExchangeRateOptions _options;
    protected readonly ILogger _logger;
    protected readonly JsonSerializerOptions _jsonOptions;

    public abstract string Name { get; }

    protected BaseExchangeRateProvider(
        HttpClient httpClient,
        IOptions<ExchangeRateOptions> options,
        ILogger logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public abstract Task<ExchangeRateDto?> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default);

    public abstract Task<Dictionary<string, ExchangeRateDto>?> GetMultipleRatesAsync(
        string baseCurrency,
        IEnumerable<string> targetCurrencies,
        CancellationToken cancellationToken = default);

    protected async Task<string> GetApiResponseAsync(string url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"API returned {response.StatusCode}");
        }
        
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    protected ExchangeRateDto CreateExchangeRateDto(
        string fromCurrency,
        string toCurrency,
        decimal rate,
        DateTime fetchedAt)
    {
        return new ExchangeRateDto
        {
            FromCurrency = fromCurrency.ToUpperInvariant(),
            ToCurrency = toCurrency.ToUpperInvariant(),
            Rate = rate,
            FetchedAt = fetchedAt,
            Source = Name
        };
    }

    protected DateTime ParseDate(string? dateStr)
    {
        return DateTime.Parse(dateStr ?? DateTime.UtcNow.ToString("yyyy-MM-dd"));
    }
}